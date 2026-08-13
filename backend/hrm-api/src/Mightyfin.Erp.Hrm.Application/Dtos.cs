using System;
using System.Collections.Generic;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

// ===================== Workers =====================

public sealed record WorkerListFilters(
    string? Search, string? Status, Guid? OrgUnitId, Guid? LocationId,
    string? WorkerType, string? Grade, int Page = 1, int PageSize = 25);

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
public sealed record WorkQueueItemDto(Guid RequestId, string WorkflowType, string Status,
    Guid? SubjectWorkerId, string? SubjectName, string? CurrentApproverName,
    DateTimeOffset? DueAt, bool IsOverdue, DateTimeOffset CreatedAt);

// ===================== HR requests & letters =====================

public sealed record HrRequestCreate(string Category, string Subject, string Body, string Confidentiality = "normal");
public sealed record HrRequestMessageCreate(string Body, bool IsInternalNote = false);
public sealed record HrLetterCreate(string LetterType, string Addressee, string Purpose);

// ===================== Speak up =====================

public sealed record ProtectedDisclosureCreate(string Category, string Severity, string Description);
public sealed record ProtectedDisclosureStatusResponse(string CaseReference, string Status,
    DateTimeOffset? LastUpdatedAt, string? NextStep);

// ===================== Payroll =====================

public sealed record PayrollRunCreate(Guid PayPeriodId, Guid PayGroupId);
public sealed record PayrollRunDto(Guid Id, string Status, string PeriodLabel, int EmployeeCount,
    decimal TotalGross, decimal TotalDeductions, decimal TotalNet, decimal TotalEmployerCost,
    int ExceptionCount, string? CalcVersion, DateTimeOffset CreatedAt);
public sealed record PayrollRunLineDto(Guid Id, Guid WorkerId, string WorkerName, string EmployeeNo,
    decimal GrossPay, decimal TotalDeductions, decimal NetPay, decimal EmployerCost,
    bool HasException, string? ExceptionReason, List<PayrollLineComponentDto> Components);
public sealed record PayrollLineComponentDto(string ComponentCode, string ComponentName,
    string ComponentType, decimal Amount, string Explanation, bool IsStatutory);
public sealed record PayslipDto(Guid Id, string PayslipNo, int Version, decimal GrossPay,
    decimal TotalDeductions, decimal NetPay, string? YtdGross, string? YtdTax, string? YtdNet,
    string Status, string? DocumentUrl, DateTimeOffset? ReleasedAt, Guid? SupersedesId);

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
