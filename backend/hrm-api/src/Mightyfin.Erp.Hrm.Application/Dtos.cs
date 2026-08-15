using System;
using System.Collections.Generic;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

// ===================== Workers =====================

public sealed record WorkerListFilters(
    string? Search, string? Status, Guid? OrgUnitId, Guid? LocationId,
    string? WorkerType, string? Grade, bool IncludeArchived = false,
    int Page = 1, int PageSize = 25);

public sealed record WorkerCreateRequest(
    string EmployeeNo, string FirstName, string LastName,
    string? MiddleName = null, string? PreferredName = null, string? Email = null,
    string? Phone = null, string? Nrc = null, string? PassportNo = null,
    string? Tpin = null, string? NapsaNumber = null, string? NhimaNumber = null,
    string? Nationality = null, string? DateOfBirth = null,
    Guid? OrgUnitId = null, Guid? LocationId = null, Guid? ManagerId = null,
    string? Grade = null, string? JobTitle = null, string? StartDate = null,
    string WorkerType = "employee",
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);

public sealed record EmergencyContactCreate(string Relationship, string FullName, string? Phone, bool IsPrimary);

// M15 self-service: the fields a worker may edit on their own record. SubjectId
// is filled server-side from the token, never from client input.
public sealed record WorkerSubjectUpdateRequest(
    string SubjectId,
    string? PreferredName = null, string? Email = null, string? Phone = null,
    string? Nrc = null, string? PassportNo = null, string? Tpin = null,
    string? NapsaNumber = null, string? NhimaNumber = null,
    string? Nationality = null, string? DateOfBirth = null,
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);
public sealed record WorkerBankDetailCreate(string BankName, string BranchCode, string AccountNumber, string AccountName, bool IsPrimary, string PaymentMethod = "bank", string? MobileMoneyNumber = null);

public sealed record WorkerUpdateRequest(
    string? FirstName = null, string? MiddleName = null, string? LastName = null,
    string? PreferredName = null, string? Email = null, string? Phone = null,
    string? Nrc = null, string? PassportNo = null, string? Tpin = null,
    string? NapsaNumber = null, string? NhimaNumber = null, string? Nationality = null,
    string? DateOfBirth = null, Guid? OrgUnitId = null, Guid? LocationId = null,
    Guid? ManagerId = null, string? Grade = null, string? JobTitle = null,
    string? Status = null, string? EndDate = null,
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);

public sealed record WorkerDto(
    Guid Id, string EmployeeNo, string FirstName, string? MiddleName, string LastName,
    string FullName, string? PreferredName, string? Email, string? Phone, string? PhotoUrl,
    string? Nrc, string? PassportNo, string? Tpin, string? NapsaNumber, string? NhimaNumber,
    string? Nationality, string? DateOfBirth, string? SubjectId, string WorkerType, string Status,
    Guid? OrgUnitId, string? OrgUnitName, Guid? LocationId, string? LocationName,
    Guid? ManagerId, string? ManagerName, string? Grade, string? JobTitle,
    string? StartDate, string? EndDate,
    List<EmergencyContactDto> EmergencyContacts, List<WorkerBankDetailDto> BankDetails,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record EmergencyContactDto(Guid Id, string Relationship, string FullName, string? Phone, bool IsPrimary);
public sealed record WorkerBankDetailDto(Guid Id, string BankName, string BranchCode, string AccountNumber, string AccountName, string PaymentMethod, string? MobileMoneyNumber, bool IsPrimary);

// ===================== Assignments & Movements =====================

public sealed record AssignmentCreateRequest(
    Guid WorkerId, Guid LegalEntityId, Guid OrgUnitId, Guid LocationId, string StartDate,
    Guid? ManagerId = null, string? JobTitle = null, string? Grade = null,
    string? PositionNo = null, string ContractType = "permanent",
    string WorkPattern = "full-time", int ProbationMonths = 3, int NoticeDays = 30,
    string? EndDate = null);

public sealed record MovementCreateRequest(
    Guid WorkerId, string MovementType, string Reason, string EffectiveDate,
    Guid? ToOrgUnitId, string? ToJobTitle, string? ToGrade, Guid? ToLocationId,
    Guid? ToManagerId, decimal? SalaryChange);

// ===================== Leave =====================

public sealed record LeaveRequestCreate(
    Guid WorkerId, string LeaveTypeCode, string StartDate, string EndDate,
    bool IsPartialDay = false, string? StartTime = null, string? EndTime = null,
    string? Reason = null, bool EvidenceAttached = false);

public sealed record LeaveBalanceDto(string LeaveTypeCode, string LeaveTypeName,
    decimal Accrued, decimal Taken, decimal Reserved, decimal Expired, decimal Available);

// ===================== Attendance =====================

public sealed record AttendanceCorrectionCreate(
    Guid WorkerId, string WorkDate, string IssueType, string? ProposedClockIn,
    string? ProposedClockOut, string? ProposedStatus, string Reason);

// ===================== Workflows =====================

public sealed record WorkflowDecisionRequest(string Action, string? Reason = null, Guid? DelegatedToId = null);
public sealed record WorkflowRequestDto(Guid Id, string WorkflowType, Guid? SubjectWorkerId, string? SubjectName, string Status, string? PayloadJson,
    string? RejectionReason, string? ReturnNote, Guid? CurrentApproverId, string? CurrentApproverName,
    DateTimeOffset? DueAt, DateTimeOffset? EscalatedAt, DateTimeOffset CreatedAt, List<WorkflowDecisionDto> Decisions);
public sealed record WorkflowDecisionDto(Guid Id, Guid RequestId, Guid ActorId, string? ActorName, string Action, string? Reason, Guid? DelegatedToId, string? DelegatedToName, DateTimeOffset CreatedAt);
public sealed record WorkflowEscalateRequest(Guid ActorId);
public sealed record WorkQueueItemDto(Guid RequestId, string WorkflowType, string Status,
    Guid? SubjectWorkerId, string? SubjectName, string? CurrentApproverName,
    DateTimeOffset? DueAt, bool IsOverdue, DateTimeOffset CreatedAt);

// ===================== HR requests & letters =====================

public sealed record HrRequestCreate(string Category, string Subject, string Body, string Confidentiality = "normal", Guid? WorkerId = null);
public sealed record HrRequestMessageCreate(string Body, bool IsInternalNote = false);
public sealed record HrLetterCreate(string LetterType, string Addressee, string Purpose, Guid? WorkerId = null);

// ===================== Speak up =====================

public sealed record ProtectedDisclosureCreate(string Category, string Severity, string Description);
public sealed record ProtectedDisclosureStatusResponse(string CaseReference, string Status,
    DateTimeOffset? LastUpdatedAt, string? NextStep);

// ===================== Payroll =====================

// ---------- M21: salary structure administration ----------
public sealed record SalaryStructureItemUpsert(Guid ComponentId, decimal? DefaultAmount = null, bool? IsOptional = null, int? Order = null);
public sealed record SalaryStructureDto(Guid Id, string Code, string Name, int Version, bool IsActive,
    List<SalaryStructureItemDto> Items);
public sealed record SalaryStructureItemDto(Guid Id, Guid ComponentId, string ComponentCode, string ComponentName,
    decimal? DefaultAmount, bool IsOptional, int Order);
public sealed record SalaryStructureCreateRequest(string Code, string Name, List<SalaryStructureItemUpsert> Items);
public sealed record SalaryStructureUpdateRequest(string? Name = null, bool? IsActive = null,
    List<SalaryStructureItemUpsert>? Items = null);

public sealed record PayrollRunCreate(Guid PayPeriodId, Guid PayGroupId);
public sealed record WorkerPayrollProfileCreate(Guid WorkerId, Guid PayGroupId, string EffectiveFrom,
    List<WorkerComponentValueCreate> Values);
public sealed record WorkerComponentValueCreate(Guid ComponentId, string? ComponentCode = null, decimal Amount = 0);
public sealed record WorkerPayrollProfileDto(Guid Id, Guid WorkerId, string? WorkerName, Guid PayGroupId, string? PayGroupName, string EffectiveFrom, List<WorkerComponentValueDto> Values);
public sealed record WorkerComponentValueDto(Guid ComponentId, string ComponentCode, string ComponentName, decimal Amount);
public sealed record PayrollRunDto(Guid Id, string Status, string PeriodLabel, int EmployeeCount,
    decimal TotalGross, decimal TotalDeductions, decimal TotalNet, decimal TotalEmployerCost,
    int ExceptionCount, string? CalcVersion, DateTimeOffset CreatedAt,
    bool IsReversal = false, Guid? ReversesRunId = null);
public sealed record PayrollRunLineDto(Guid Id, Guid WorkerId, string WorkerName, string EmployeeNo,
    decimal GrossPay, decimal TotalDeductions, decimal NetPay, decimal EmployerCost,
    bool HasException, string? ExceptionReason, List<PayrollLineComponentDto> Components);
public sealed record PayrollLineComponentDto(string ComponentCode, string ComponentName,
    string ComponentType, decimal Amount, string Explanation, bool IsStatutory);
public sealed record PayslipDto(Guid Id, string PayslipNo, int Version, decimal GrossPay,
    decimal TotalDeductions, decimal NetPay, string? YtdGross, string? YtdTax, string? YtdNet,
    string Status, string? DocumentUrl, DateTimeOffset? ReleasedAt, Guid? SupersedesId,
    // M24: statutory identity pack snapshotted at payment time (appended so existing callers stay binary-compatible)
    string? WorkerNrc = null, string? WorkerTpin = null,
    string? WorkerNapsaNumber = null, string? WorkerNhimaNumber = null);
public sealed record PayrollRunReverseCreate(string? Reason = null);

/// <summary>Aggregated statutory liability for one released payroll period
/// (M23): PAYE, NAPSA and NHIMA split by employee/employer share, plus the
/// employer's registration references carried on every statutory filing.</summary>
public sealed record StatutorySummaryDto(string PeriodLabel, int WorkerCount,
    decimal TotalGross, decimal TotalPaye, decimal TotalNapsaEe, decimal TotalNapsaEr,
    decimal TotalNhimaEe, decimal TotalNhimaEr, decimal TotalNet,
    string EmployerName, string EmployerTpin, string NapsaEmployerRef,
    string NhimaEmployerRef, string Currency);
public sealed record EmployerLiabilityRow(string ComponentCode, string ComponentName, string Payer,
    decimal TotalAmount, int WorkerCount);

/// <summary>M24: per-worker statutory identity readiness for every line in a
/// payroll run. The run cannot be released while any worker has a missing ref.</summary>
public sealed record StatutoryReadinessDto(Guid RunId, string? PeriodLabel, bool IsReady,
    int WorkerCount, List<WorkerStatutoryItemDto> Workers);
public sealed record WorkerStatutoryItemDto(Guid WorkerId, string EmployeeNo, string FullName,
    bool HasNrc, bool HasTpin, bool HasNapsaNumber, bool HasNhimaNumber, bool Ready);
public sealed record EmployerLiabilityReportDto(string PeriodLabel, string TaxYear,
    List<EmployerLiabilityRow> Rows, decimal TotalStatutory, DateTimeOffset GeneratedAt);
public sealed record PayslipGenerateRequest(Guid PayslipId);

// ===================== Admin config =====================

public sealed record AdminConfigDto(
    List<LegalEntityDto> LegalEntities, List<WorkLocationDto> Locations, List<OrgUnitDto> OrgUnits,
    List<WorkCalendarDto> Calendars, List<LeaveTypeDto> LeaveTypes,
    List<CapabilityDto> Capabilities, List<PayGroupDto> PayGroups);

public sealed record LegalEntityDto(Guid Id, string Code, string RegisteredName, string? TradingName, string Currency);
public sealed record WorkLocationDto(Guid Id, string Code, string Name, Guid LegalEntityId, string? Type);
public sealed record OrgUnitDto(Guid Id, string Code, string Name, Guid? ParentId, string? UnitType, string Status, string? ManagerName);
public sealed record WorkCalendarDto(Guid Id, string Name, int StandardWeeklyHours, string WeekendDays, int HolidayCount);
public sealed record LeaveTypeDto(Guid Id, string Code, string Name, string Category, int DefaultDaysPerYear, bool IsActive);
public sealed record CapabilityDto(string FeatureKey, string Tier, bool IsEnabled);
public sealed record PayGroupDto(Guid Id, string Code, string Name, string Frequency, string Currency, int CalendarDayOfMonth);

// ===================== Recruitment (M7) =====================

public sealed record VacancyCreate(Guid OrgUnitId, string JobTitle, string Grade, string? Description, string Status = "draft");
public sealed record CandidateCreate(Guid VacancyId, string FullName, string? Email = null, string? Phone = null, string? Source = null, string? Notes = null);
public sealed record CandidateAdvanceRequest(string Stage, string? Score = null, string? Notes = null); // stage: screening | shortlisted | interviewed | offered | hired | rejected
public sealed record OfferCreate(Guid CandidateId, decimal BaseSalary, string? StartDate = null, string ContractType = "permanent", int ProbationMonths = 3, int NoticeDays = 30, string? Notes = null);

// ===================== Relations (M7) =====================

public sealed record RelationsCaseCreate(Guid? SubjectWorkerId, string CaseType, string Category, string Severity, string Summary, string Description);

// ===================== Documents & reports (M8) =====================

public sealed record WorkerDocumentCreate(Guid WorkerId, string Category, string Title, string FileName, string ContentType, string Classification = "internal");
public sealed record ReportQuery(string ReportType, string? FromDate = null, string? ToDate = null, Guid? OrgUnitId = null, Guid? LocationId = null);
public sealed record ReportDto(string ReportType, string GeneratedAt, Dictionary<string, object?> Summary, List<Dictionary<string, object?>> Rows);

// ===================== Organization & config (M1) =====================

// ---------- Legal entities ----------
public sealed record LegalEntityCreateRequest(
    string Code, string RegisteredName, string? TradingName = null,
    string? PacraNumber = null, string? Tpin = null, string? NapsaEmployerRef = null,
    string? NhimaEmployerRef = null, string? WcfcbEmployerRef = null,
    string Currency = "ZMW", string CountryCode = "ZM", bool IsDefault = false);
public sealed record LegalEntityUpdateRequest(
    string? RegisteredName = null, string? TradingName = null, string? PacraNumber = null,
    string? Tpin = null, string? NapsaEmployerRef = null, string? NhimaEmployerRef = null,
    string? WcfcbEmployerRef = null, string? Currency = null, bool? IsDefault = null);
public sealed record LegalEntityDtoFull(
    Guid Id, string Code, string RegisteredName, string? TradingName, string? PacraNumber,
    string? Tpin, string? NapsaEmployerRef, string? NhimaEmployerRef, string? WcfcbEmployerRef,
    string Currency, string CountryCode, bool IsDefault, DateTimeOffset CreatedAt);

// ---------- Work locations ----------
public sealed record WorkLocationCreateRequest(
    string Code, string Name, Guid LegalEntityId, string? AddressLine = null,
    string? Province = null, string? District = null, string? City = null,
    string Type = "branch", Guid? DefaultCalendarId = null);
public sealed record WorkLocationUpdateRequest(
    string? Name = null, string? AddressLine = null, string? Province = null,
    string? District = null, string? City = null, string? Type = null,
    Guid? DefaultCalendarId = null);
public sealed record WorkLocationDtoFull(
    Guid Id, string Code, string Name, Guid LegalEntityId, string? LegalEntityName,
    string? AddressLine, string? Province, string? District, string? City,
    string Type, Guid? DefaultCalendarId, string? CalendarName, DateTimeOffset CreatedAt);

// ---------- Org units ----------
public sealed record OrgUnitCreateRequest(
    string Code, string Name, Guid LegalEntityId, Guid? ParentId = null,
    string UnitType = "department", string? CostCentreRef = null, Guid? ManagerId = null,
    string EffectiveFrom = null!, string? EffectiveTo = null);
public sealed record OrgUnitUpdateRequest(
    string? Name = null, Guid? ParentId = null, string? UnitType = null,
    string? CostCentreRef = null, Guid? ManagerId = null,
    string? EffectiveTo = null, string? Status = null);
public sealed record OrgUnitDtoFull(
    Guid Id, string Code, string Name, Guid LegalEntityId, string? LegalEntityName,
    Guid? ParentId, string? UnitType, string? CostCentreRef, Guid? ManagerId,
    string? ManagerName, string EffectiveFrom, string? EffectiveTo, string Status,
    DateTimeOffset CreatedAt);
public sealed record OrgUnitCloseRequest(string EffectiveDate, string? Reason = null);
public sealed record OrgUnitTreeDto(
    Guid Id, string Code, string Name, string? UnitType, string Status,
    Guid? ManagerId, string? ManagerName, string EffectiveFrom, string? EffectiveTo,
    List<OrgUnitTreeDto> Children);

// ---------- Work calendars ----------
public sealed record WorkCalendarCreateRequest(
    string Name, Guid LegalEntityId, string CountryCode = "ZM",
    int StandardWeeklyHours = 45, string WeekendDays = "sat,sun", bool IsDefault = false);
public sealed record WorkCalendarUpdateRequest(
    string? Name = null, int? StandardWeeklyHours = null, string? WeekendDays = null,
    bool? IsDefault = null);
public sealed record WorkCalendarDtoFull(
    Guid Id, string Name, Guid LegalEntityId, string? LegalEntityName, string CountryCode,
    int StandardWeeklyHours, string WeekendDays, bool IsDefault, int HolidayCount,
    List<PublicHolidayDto> Holidays, DateTimeOffset CreatedAt);

// ---------- Public holidays ----------
public sealed record PublicHolidayCreateRequest(
    Guid CalendarId, string Name, string HolidayDate, string? ObservedOn = null,
    bool IsRecurring = false, string? Description = null);
public sealed record PublicHolidayUpdateRequest(
    string? Name = null, string? HolidayDate = null, string? ObservedOn = null,
    bool? IsRecurring = null, string? Description = null);
public sealed record PublicHolidayDto(
    Guid Id, Guid CalendarId, string Name, string HolidayDate, string? ObservedOn,
    bool IsRecurring, string? Description);

// ---------- Leave types ----------
public sealed record LeaveTypeCreateRequest(
    string Code, string Name, string Category = "paid", int DefaultDaysPerYear = 24,
    decimal MaxConsecutiveDays = 999, bool RequiresEvidence = false, int MinNoticeDays = 0,
    bool AllowsPartialDays = false, int CarryForwardDays = 0,
    int CarryForwardExpiryMonths = 0, bool AllowNegative = false,
    string EffectiveFrom = null!, string? EffectiveTo = null);
public sealed record LeaveTypeUpdateRequest(
    string? Name = null, string? Category = null, int? DefaultDaysPerYear = null,
    decimal? MaxConsecutiveDays = null, bool? RequiresEvidence = null,
    int? MinNoticeDays = null, bool? AllowsPartialDays = null, int? CarryForwardDays = null,
    int? CarryForwardExpiryMonths = null, bool? AllowNegative = null,
    string? EffectiveTo = null, bool? IsActive = null);
public sealed record LeaveTypeDtoFull(
    Guid Id, string Code, string Name, string Category, int DefaultDaysPerYear,
    decimal MaxConsecutiveDays, bool RequiresEvidence, int MinNoticeDays,
    bool AllowsPartialDays, int CarryForwardDays, int CarryForwardExpiryMonths,
    bool AllowNegative, string EffectiveFrom, string? EffectiveTo, bool IsActive,
    DateTimeOffset CreatedAt);

// ---------- Capabilities ----------
public sealed record CapabilityUpdateRequest(string? Tier = null, bool? IsEnabled = null, string? Description = null);

// ===================== Worker lifecycle (M2) =====================

// ---------- Shared shapes (used by routes & tests) ----------
public sealed record EmergencyContactRequest(string Relationship, string FullName, string? Phone = null, bool IsPrimary = false);
public sealed record BankDetailRequest(string BankName, string BranchCode, string AccountNumber,
    string AccountName, string PaymentMethod = "bank", string? MobileMoneyNumber = null, bool IsPrimary = false);
public sealed record AssignmentUpdateRequest(
    Guid? OrgUnitId = null, Guid? LocationId = null, Guid? ManagerId = null,
    string? JobTitle = null, string? Grade = null, string? PositionNo = null,
    string? ContractType = null, string? WorkPattern = null, int? ProbationMonths = null,
    int? NoticeDays = null, string? EndDate = null, string? EffectiveTo = null, string? Status = null);
// AssignmentDto and MovementDto are defined in Workers/WorkerService.cs (shared surface).
// M2 lifecycle adds its own richer projections below.
public sealed record OnboardingPlanDto(Guid WorkerId, bool IsOnboarded, int TasksCompleted, int TasksTotal);
public sealed record MovementImpactDto(string Field, string From, string To);
public sealed record MovementDetailDto(
    Guid Id, Guid WorkerId, string MovementType, string Status, string EffectiveDate,
    string Reason, Guid? FromOrgUnitId, string? FromOrgUnitName, string? FromJobTitle,
    string? FromGrade, Guid? ToOrgUnitId, string? ToOrgUnitName, string? ToJobTitle,
    string? ToGrade, Guid? ToLocationId, Guid? ToManagerId, decimal? SalaryChange,
    DateTimeOffset CreatedAt);
public sealed record OffboardingResultDto(bool Cleared, string[] OpenItems);

// ===================== Time — attendance, roster, decisions (M3) =====================

/// <summary>Clock punch response with the derived worker state.</summary>
public sealed record PunchResultDto(Guid Id, Guid WorkerId, string WorkDate, string ClockIn, string ClockOut,
    string Source, string DerivedStatus, decimal TotalHours, string State);

public sealed record AttendanceRecordDto(Guid Id, Guid WorkerId, string WorkerName, string WorkDate,
    string? ClockIn, string? ClockOut, string Source, string DerivedStatus, decimal TotalHours);

/// <summary>Roster day for the worker: expected shift, attendance, exceptions, cutoff.</summary>
public sealed record RosterDayDto(string Date, string DayLabel, bool IsWorkingDay, string? ClockIn, string? ClockOut,
    string? Status, string? ShiftName, string? ShiftStart, string? ShiftEnd, string? CalendarName,
    bool IsPublicHoliday, string? PublicHolidayName, string? PayrollCutoff, string? CorrectionRef);

/// <summary>Decision on a leave request or attendance correction (submitted via
/// /time/leave/{id}/decide or /time/corrections/{id}/decide).</summary>
public sealed record TimeDecisionRequest(string Action, string? Reason = null);
