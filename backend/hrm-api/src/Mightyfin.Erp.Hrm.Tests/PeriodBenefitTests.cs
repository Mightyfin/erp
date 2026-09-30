using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Benefits;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
namespace Mightyfin.Erp.Hrm.Tests;
public class PeriodBenefitTests
{
    [Fact]
    public async Task CalculationAndRecalculationUseExactPeriodAmountOnceInsteadOfRecurring()
    {
        var (payroll,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType { Code="trip",Name="Trip Allowance",IncludeInPayroll=true,IsTaxable=true,AnnualCap=50000 };
        db.BenefitTypes.Add(type);db.WorkerBenefitAllowances.Add(new WorkerBenefitAllowance { WorkerId=stack.Profile.WorkerId,BenefitType=type,Year=stack.P2.StartDate.Year,AnnualAmount=1200 });await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        await svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,2750,"11 trips x K250")]),default);
        Assert.Equal(2750,(await svc.GetAsync(stack.P2.Id,type.Id,default)).Rows.Single().Amount);
        Assert.Null((await svc.GetAsync(stack.P1.Id,type.Id,default)).Rows.Single().Amount);
        var run=await payroll.CreateRunAsync(new(stack.P2.Id,stack.Group.Id),default);await payroll.LockRunAsync(run.Id,default);await payroll.CalculateRunAsync(run.Id,default);
        var line=await db.PayrollRunLines.Include(l=>l.Components).SingleAsync(l=>l.RunId==run.Id);
        Assert.Equal(32750,line.GrossPay);Assert.Equal(2750,line.Components.Single(c=>c.ComponentCode=="benefit-trip").Amount);
        var net=line.NetPay;await payroll.CalculateRunAsync(run.Id,default);
        line=await db.PayrollRunLines.Include(l=>l.Components).SingleAsync(l=>l.RunId==run.Id);
        Assert.Equal(net,line.NetPay);Assert.Single(line.Components,c=>c.ComponentCode=="benefit-trip");
        await svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,3000,"")]),default);
        Assert.Equal("locked",(await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status);
        await Assert.ThrowsAsync<DomainException>(()=>payroll.ApproveRunAsync(run.Id,null,default));
        await payroll.CalculateRunAsync(run.Id,default);
        Assert.Equal(33000,(await db.PayrollRunLines.SingleAsync(l=>l.RunId==run.Id)).GrossPay);
        foreach(var status in new[]{"calculating","in-review","approved","released","closed"}) {
            (await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status=status;await db.SaveChangesAsync();
            Assert.False((await svc.GetAsync(stack.P2.Id,type.Id,default)).Editable);
            await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,4000,"")]),default));
        }
    }
    [Fact]
    public async Task ZeroSuppressesRecurringAndClearRestoresItInPreview()
    {
        var (payroll,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);stack.P2.StartDate=new(today.Year,today.Month,1);stack.P2.EndDate=stack.P2.StartDate.AddMonths(1).AddDays(-1);
        var type=new BenefitType { Code="trip",Name="Trip",IncludeInPayroll=true };
        db.BenefitTypes.Add(type);db.WorkerBenefitAllowances.Add(new WorkerBenefitAllowance { WorkerId=stack.Profile.WorkerId,BenefitType=type,Year=today.Year,AnnualAmount=1200 });await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        await svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,0,"")]),default);
        Assert.Equal(30000,(await payroll.PreviewWorkerPayslipAsync(stack.Profile.WorkerId,default)).Line!.GrossPay);
        await svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,null,"")]),default);
        Assert.Equal(30100,(await payroll.PreviewWorkerPayslipAsync(stack.Profile.WorkerId,default)).Line!.GrossPay);
        Assert.Empty(await db.PeriodBenefits.Where(b=>!b.IsArchived).ToListAsync());
    }
    [Fact]
    public async Task InvalidBatchAndWrongBranchSaveNothing()
    {
        var (_,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType {Code="trip",Name="Trip",IncludeInPayroll=true};db.BenefitTypes.Add(type);await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,500,""),new(Guid.NewGuid(),300,"")]),default));
        var scoped=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext {LocationId=Guid.NewGuid()});
        Assert.Empty((await scoped.GetAsync(stack.P2.Id,type.Id,default)).Rows);
        await Assert.ThrowsAsync<DomainException>(()=>scoped.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,500,"")]),default));
        Assert.Empty(await db.PeriodBenefits.ToListAsync());
    }
    [Fact]
    public async Task AnnualCapAndNonPayrollTypesRejectAssignments()
    {
        var (_,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType {Code="trip",Name="Trip",IncludeInPayroll=true,AnnualCap=500};db.BenefitTypes.Add(type);await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        await svc.SaveAsync(stack.P1.Id,type.Id,new([new(stack.Profile.WorkerId,300,"")]),default);
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,300,"")]),default));
        type.IncludeInPayroll=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,100,"")]),default));
        Assert.Single(await db.PeriodBenefits.ToListAsync());
    }

    [Fact]
    public async Task PayrollClaimCreatesListedRecordAndAddsToSelectedMonthExactlyOnce()
    {
        var (payroll,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType {Code="trip",Name="Trip",IncludeInPayroll=true,IsTaxable=true,AnnualCap=5000};db.BenefitTypes.Add(type);await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        var benefits=new Mightyfin.Erp.Hrm.Application.Benefits.BenefitServiceImpl(new Mightyfin.Erp.Hrm.Infrastructure.Benefits.BenefitRepository(db),new PermissiveAuthz(),new Mightyfin.Erp.Hrm.Infrastructure.WorkerRepository(db),periodBenefits:svc);
        var claim=await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",2750,stack.Group.Currency,"11 trips",true,stack.P2.Id),default);
        Assert.Equal("approved",claim.Status);Assert.Equal(stack.P2.Id,claim.PayPeriodId);Assert.True(claim.EvidenceAttached);
        await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",250,stack.Group.Currency,"One more trip",false,stack.P2.Id),default);
        Assert.Equal(3000,(await svc.GetAsync(stack.P2.Id,type.Id,default)).Rows.Single().Amount);
        Assert.Null((await svc.GetAsync(stack.P1.Id,type.Id,default)).Rows.Single().Amount);
        Assert.Equal(2,await db.BenefitClaims.CountAsync());
        await Assert.ThrowsAsync<DomainException>(()=>benefits.PayClaimAsync(claim.Id,default));
        var run=await payroll.CreateRunAsync(new(stack.P2.Id,stack.Group.Id),default);await payroll.LockRunAsync(run.Id,default);await payroll.CalculateRunAsync(run.Id,default);
        var line=await db.PayrollRunLines.Include(l=>l.Components).SingleAsync(l=>l.RunId==run.Id);
        Assert.Equal(33000,line.GrossPay);Assert.Equal(3000,line.Components.Single(c=>c.ComponentCode=="benefit-trip").Amount);
        await payroll.CalculateRunAsync(run.Id,default);
        Assert.Single((await db.PayrollRunLines.Include(l=>l.Components).SingleAsync(l=>l.RunId==run.Id)).Components,c=>c.ComponentCode=="benefit-trip");
        await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",100,stack.Group.Currency,null,false,stack.P2.Id),default);
        Assert.Equal("locked",(await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status);
        await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",100,stack.Group.Currency,null,false,stack.P2.Id),default);
        await payroll.CalculateRunAsync(run.Id,default);
        Assert.Equal(33200,(await db.PayrollRunLines.SingleAsync(l=>l.RunId==run.Id)).GrossPay);
        Assert.Equal(4,await db.BenefitClaims.CountAsync());
        (await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status="approved";await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(()=>benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",100,stack.Group.Currency,null,false,stack.P2.Id),default));
        Assert.Equal(4,await db.BenefitClaims.CountAsync());
    }
    [Fact]
    public async Task InvalidPayrollClaimsDoNotCreateClaimsOrAmounts()
    {
        var (_,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType {Code="trip",Name="Trip",IncludeInPayroll=true,AnnualCap=500,RequiresEvidence=true};db.BenefitTypes.Add(type);await db.SaveChangesAsync();
        var svc=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        BenefitClaimCreateRequest good=new(stack.Profile.WorkerId,"trip",100,stack.Group.Currency,null,true,stack.P2.Id);
        foreach(var bad in new[] {good with {PayPeriodId=null},good with {AmountClaimed=0},good with {AmountClaimed=1.123m},good with {AmountClaimed=501},good with {Currency="WRONG"},good with {EvidenceAttached=false},good with {WorkerId=Guid.NewGuid()}})
            await Assert.ThrowsAsync<DomainException>(()=>svc.SubmitClaimAsync(type.Id,bad,default));
        var scoped=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext {LocationId=Guid.NewGuid()});
        await Assert.ThrowsAsync<DomainException>(()=>scoped.SubmitClaimAsync(type.Id,good,default));
        Assert.Empty(await db.BenefitClaims.ToListAsync());Assert.Empty(await db.PeriodBenefits.ToListAsync());
    }
    [Fact]
    public async Task EditingAndDeletingOneOfTwoClaimsReconcilesPayrollAndRequiresRecalculation()
    {
        var (payroll,db)=PayrollEngineTests.Build();var stack=await PayrollEngineTests.SeedStackAsync(db);
        var type=new BenefitType {Code="trip",Name="Trip",IncludeInPayroll=true,IsTaxable=true,AnnualCap=10000};db.BenefitTypes.Add(type);await db.SaveChangesAsync();
        var assignment=new PeriodBenefitService(db,new PermissiveAuthz(),new ShellContext());
        var benefits=new BenefitServiceImpl(new Mightyfin.Erp.Hrm.Infrastructure.Benefits.BenefitRepository(db),new PermissiveAuthz(),new Mightyfin.Erp.Hrm.Infrastructure.WorkerRepository(db),periodBenefits:assignment);
        var first=await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",2750,stack.Group.Currency,"11 trips",true,stack.P2.Id),default);
        var duplicate=await benefits.CreateClaimAsync(new(stack.Profile.WorkerId,"trip",2750,stack.Group.Currency,"duplicate",false,stack.P2.Id),default);
        Assert.True((await assignment.GetAsync(stack.P2.Id,type.Id,default)).Rows.Single().ClaimManaged);
        await Assert.ThrowsAsync<DomainException>(()=>assignment.SaveAsync(stack.P2.Id,type.Id,new([new(stack.Profile.WorkerId,1,"manual")]),default));
        var run=await payroll.CreateRunAsync(new(stack.P2.Id,stack.Group.Id),default);await payroll.LockRunAsync(run.Id,default);await payroll.CalculateRunAsync(run.Id,default);
        Assert.Equal(35500,(await db.PayrollRunLines.SingleAsync(l=>l.RunId==run.Id)).GrossPay);
        await benefits.UpdateClaimAsync(first.Id,new(2500,stack.Group.Currency,"corrected",true),default);
        Assert.Equal("locked",(await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status);
        Assert.Equal(5250,(await assignment.GetAsync(stack.P2.Id,type.Id,default)).Rows.Single().Amount);
        await benefits.DeleteClaimAsync(duplicate.Id,default);
        Assert.Equal(2500,(await assignment.GetAsync(stack.P2.Id,type.Id,default)).Rows.Single().Amount);
        Assert.Single((await benefits.ListClaimsAsync(stack.Profile.WorkerId,null,1,50,default)).Items);
        await payroll.CalculateRunAsync(run.Id,default);
        var line=await db.PayrollRunLines.Include(l=>l.Components).SingleAsync(l=>l.RunId==run.Id);
        Assert.Equal(32500,line.GrossPay);Assert.Single(line.Components,c=>c.ComponentCode=="benefit-trip"&&c.Amount==2500);
        (await db.PayrollRuns.SingleAsync(r=>r.Id==run.Id)).Status="approved";await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(()=>benefits.DeleteClaimAsync(first.Id,default));
    }
}
