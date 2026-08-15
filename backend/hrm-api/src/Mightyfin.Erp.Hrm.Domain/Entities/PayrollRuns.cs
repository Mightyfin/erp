namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>J-groups 01,18-22: The payroll run — the core payroll transaction.
/// Lifecycle enforces segregation of duties: the person who calculates/releases
/// cannot be the only approver; payments are a separate stage from calculation.</summary>
public class PayrollRun : Entity
{
    public Guid PayPeriodId { get; set; }
    public PayPeriod? PayPeriod { get; set; }
    public Guid PayGroupId { get; set; }
    public string Status { get; set; } = "draft";
    // draft | calculating | calculated | validating | in-review | approved | released | closed | reversed

    // Control totals (computed) — the payroll work centre shows these
    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerCost { get; set; }

    public bool IsReversal { get; set; }
    public Guid? ReversesRunId { get; set; }
    public Guid? CalcJobId { get; set; }              // separation for future worker process
    public int ExceptionCount { get; set; }
    public string? CalcVersion { get; set; }          // pinned engine + rule version snapshot
    public string? ApprovalNote { get; set; }
}

/// <summary>One line per employee per run; the line carries its own component
/// breakdown so every payslip is self-explaining and historically exact.</summary>
public class PayrollRunLine : Entity
{
    public Guid RunId { get; set; }
    public PayrollRun? Run { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerCost { get; set; }
    public bool HasException { get; set; }
    public string? ExceptionReason { get; set; }      // negative-net | missing-profile | variance | missing-bank
    public int ComponentCount { get; set; }
    public string RuleVersionSnapshot { get; set; } = ""; // json snapshot of rule versions used for this line
    public ICollection<PayrollLineComponent> Components { get; set; } = new List<PayrollLineComponent>();
}

/// <summary>Component-level breakdown of one run line (the explainable piece):
/// which rule version, which formula, what input, what result.</summary>
public class PayrollLineComponent : Entity
{
    public Guid RunLineId { get; set; }
    public PayrollRunLine? RunLine { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string ComponentType { get; set; } = null!; // earning | deduction | employer-contribution | tax
    public decimal Amount { get; set; }
    public string Explanation { get; set; } = "";      // human-readable: "15% of Basic (K12,000.00) capped at NAPSA ceiling"
    public int RuleVersionId { get; set; }             // ties to the exact rule version snapshot
    public bool IsStatutory { get; set; }
}

/// <summary>J-group 21: Payslip generated from a released run line. Corrected
/// versions are linked; originals are never overwritten (J.1 historical behaviour).</summary>
public class Payslip : Entity
{
    public Guid RunLineId { get; set; }
    public PayrollRunLine? RunLine { get; set; }
    public Guid WorkerId { get; set; }
    public string PayslipNo { get; set; } = null!;
    public int Version { get; set; } = 1;
    public Guid? SupersedesId { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string? YtdGross { get; set; }
    public string? YtdTax { get; set; }
    public string? YtdNet { get; set; }
    public string Status { get; set; } = "final";       // final | corrected | voided | superseded
    public string? DocumentUrl { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }

    // M24: statutory identity pack captured at payment time. The payslip is a
    // historical record, so the worker's NRC/TPIN/NAPSA/NHIMA values at the
    // moment of release are snapshotted here rather than re-read later.
    public string? WorkerNrc { get; set; }
    public string? WorkerTpin { get; set; }
    public string? WorkerNapsaNumber { get; set; }
    public string? WorkerNhimaNumber { get; set; }

    public ICollection<PayslipAccessLog> AccessLogs { get; set; } = new List<PayslipAccessLog>();
}

public class PayslipAccessLog : Entity
{
    public Guid PayslipId { get; set; }
    public string AccessedBy { get; set; } = null!;   // subject id of the viewer
    public string AccessReason { get; set; } = "self-service";
    public DateTimeOffset AccessedAt { get; set; }
}

/// <summary>Append-only audit: every mutation on any entity records before/after
/// JSON, actor, and correlation id.</summary>
public class AuditEntry : Entity
{
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;       // create | update | delete | approve | reject | return | release
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string ActorSubjectId { get; set; } = null!;
    public string? CorrelationId { get; set; }
}

/// <summary>HRM-003: Entitlement and permission reading surface. Capabilities are
/// enableable feature flags; roles carry a JSON permission matrix evaluated
/// against tenant + scope.</summary>
public class CapabilityConfig : Entity
{
    public string FeatureKey { get; set; } = null!;   // time-and-attendance | payroll | recruitment | performance
    public string Tier { get; set; } = "essentials";  // essentials | advanced | enterprise
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
}
