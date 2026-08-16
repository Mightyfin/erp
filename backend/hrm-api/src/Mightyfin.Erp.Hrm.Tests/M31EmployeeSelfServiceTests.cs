using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M31: self-service data is always resolved from the OIDC subject,
/// never from a worker id supplied by the browser.</summary>
public sealed class M31EmployeeSelfServiceTests
{
    [Fact]
    public async Task NotificationInbox_IsSubjectScopedAndReadStateIsOwned()
    {
        await using var db = TestDbContextFactory.Create("m31-notifications");
        var mine = Notification("subject-mine", HrmEventTypes.PayslipReleased);
        var theirs = Notification("subject-theirs", HrmEventTypes.RequestDecided);
        db.OutboxMessages.AddRange(mine, theirs);
        await db.SaveChangesAsync();
        var service = new EmployeeNotificationService(db, new PermissiveAuthz());

        var inbox = await service.ListAsync("subject-mine", CancellationToken.None);

        Assert.Equal(1, inbox.UnreadCount);
        Assert.Equal(mine.Id, Assert.Single(inbox.Items).Id);
        await service.MarkReadAsync(mine.Id, "subject-mine", CancellationToken.None);
        Assert.True((await service.ListAsync("subject-mine", CancellationToken.None)).Items.Single().IsRead);
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            service.MarkReadAsync(theirs.Id, "subject-mine", CancellationToken.None));
        Assert.Equal("notification-not-owned", error.Code);
    }

    [Fact]
    public async Task RequestHistory_HidesInternalNotesAndRejectsForeignReplies()
    {
        await using var db = TestDbContextFactory.Create("m31-requests");
        var mine = Worker("M31-001", "subject-mine");
        var other = Worker("M31-002", "subject-other");
        db.Workers.AddRange(mine, other);
        var request = new HrRequest
        {
            WorkerId = mine.Id, Category = "payroll", Subject = "Allowance query",
            Body = "Please explain this allowance.", Status = "open", Confidentiality = "normal",
        };
        var foreign = new HrRequest
        {
            WorkerId = other.Id, Category = "benefits", Subject = "Private request",
            Body = "Private body.", Status = "open", Confidentiality = "confidential",
        };
        db.HrRequests.AddRange(request, foreign);
        db.HrRequestMessages.AddRange(
            new HrRequestMessage { RequestId = request.Id, From = "hr", Body = "Visible response", IsInternalNote = false },
            new HrRequestMessage { RequestId = request.Id, From = "hr", Body = "Private HR assessment", IsInternalNote = true });
        await db.SaveChangesAsync();
        var service = Experience(db);

        var detail = await service.GetMyRequestAsync(request.Id, "subject-mine", CancellationToken.None);

        Assert.Equal("Please explain this allowance.", detail.Body);
        Assert.Equal("Visible response", Assert.Single(detail.Messages).Body);
        Assert.DoesNotContain(detail.Messages, x => x.IsInternalNote);
        var readError = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetMyRequestAsync(foreign.Id, "subject-mine", CancellationToken.None));
        Assert.Equal("hr-request-not-owned", readError.Code);
        var replyError = await Assert.ThrowsAsync<DomainException>(() =>
            service.AddMessageAsync(foreign.Id, mine.Id, "employee", new HrRequestMessageCreate("intrusion"), CancellationToken.None));
        Assert.Equal("hr-request-not-owned", replyError.Code);
    }

    [Fact]
    public async Task PersonalDocuments_ExcludeRestrictedAndRejectAnotherWorkerFile()
    {
        await using var db = TestDbContextFactory.Create("m31-documents");
        var mine = Worker("M31-D01", "subject-docs");
        var other = Worker("M31-D02", "subject-other");
        db.Workers.AddRange(mine, other);
        var ownPath = Path.GetTempFileName();
        var otherPath = Path.GetTempFileName();
        try
        {
            var own = Document(mine.Id, ownPath, "internal");
            var restricted = Document(mine.Id, ownPath, "restricted");
            var foreign = Document(other.Id, otherPath, "internal");
            db.WorkerDocuments.AddRange(own, restricted, foreign);
            await db.SaveChangesAsync();
            var service = Documents(db);

            var list = await service.ListMyDocumentsAsync("subject-docs", CancellationToken.None);

            Assert.Equal(own.Id, Assert.Single(list.Items).Id);
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                service.GetMyDocumentStreamAsync(foreign.Id, "subject-docs", CancellationToken.None));
            Assert.Equal("document-not-owned", error.Code);
            var categoryError = await Assert.ThrowsAsync<DomainException>(() =>
                service.UploadMyDocumentAsync("subject-docs", "contract", "Contract", "contract.pdf", "application/pdf", 1, ownPath, CancellationToken.None));
            Assert.Equal("document-self-category-forbidden", categoryError.Code);
        }
        finally
        {
            File.Delete(ownPath);
            File.Delete(otherPath);
        }
    }

    [Fact]
    public async Task Letters_AreCreatedForSignedInWorkerAndForeignDownloadIsRejected()
    {
        await using var db = TestDbContextFactory.Create("m31-letters");
        var mine = Worker("M31-L01", "subject-letters");
        var other = Worker("M31-L02", "subject-other");
        db.Workers.AddRange(mine, other);
        var foreign = new HrLetter
        {
            WorkerId = other.Id, LetterType = "bank", Status = "generated",
            Addressee = "Bank", Purpose = "Account", TemplateBody = "Private letter",
        };
        db.HrLetters.Add(foreign);
        await db.SaveChangesAsync();
        var service = Experience(db);

        var created = await service.CreateMyLetterAsync("subject-letters",
            new HrLetterCreate("employment-confirmation", "Bank", "Account opening", other.Id), CancellationToken.None);

        Assert.Equal(mine.Id, created.WorkerId);
        Assert.Single((await service.GetMyLettersAsync("subject-letters", null, CancellationToken.None)).Items);
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetMyLetterAsync(foreign.Id, "subject-letters", CancellationToken.None));
        Assert.Equal("letter-not-owned", error.Code);
    }

    private static ExperienceServiceImpl Experience(HrmDbContext db) => new(
        new ExperienceRepository(db), new PermissiveAuthz(),
        new WorkflowServiceImpl(new WorkflowRepository(db), new PermissiveAuthz(), new NoOpEffects()),
        new LetterTemplatesImpl(), new FakeMergeDataProvider(),
        new WorkerServiceImpl(new WorkerRepository(db), new PermissiveAuthz(), new UlidIdProvider()));

    private static DocumentsServiceImpl Documents(HrmDbContext db) => new(
        new DocumentsRepository(db), new ConfigRepository(db), new PermissiveAuthz(),
        new WorkerServiceImpl(new WorkerRepository(db), new PermissiveAuthz(), new UlidIdProvider()));

    private static Worker Worker(string employeeNo, string subjectId) => new()
    {
        EmployeeNo = employeeNo, FirstName = "Self", LastName = employeeNo,
        WorkerType = "employee", Status = "active", SubjectId = subjectId,
    };

    private static WorkerDocument Document(Guid workerId, string path, string classification) => new()
    {
        WorkerId = workerId, Category = "certificate", Title = "Certificate",
        FileName = "certificate.pdf", ContentType = "application/pdf", SizeBytes = 1,
        StoragePath = path, Classification = classification,
    };

    private static OutboxMessage Notification(string subjectId, string eventType) => new()
    {
        PublicId = Guid.NewGuid().ToString("N"), EventType = eventType,
        Environment = "test", SubjectId = subjectId, CorrelationId = Guid.NewGuid().ToString("N"),
        PayloadJson = "{}", Status = "published",
    };
}
