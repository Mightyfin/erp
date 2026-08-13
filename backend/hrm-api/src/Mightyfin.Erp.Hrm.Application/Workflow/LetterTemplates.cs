namespace Mightyfin.Erp.Hrm.Application.Workflow;

/// <summary>Built-in HR letter templates (UI-XPR-002). Templates are plain
/// strings with {placeholder} tokens; the same template is used for on-screen
/// rendering and PDF export later.</summary>
public sealed class LetterTemplatesImpl : ILetterTemplates
{
    public string Render(string letterType, LetterMergeContext ctx)
    {
        var body = letterType switch
        {
            "employment-confirmation" => EmploymentConfirmation(),
            "salary-confirmation" => SalaryConfirmation(),
            "reference" => Reference(),
            "visa" => Visa(),
            "bank" => Bank(),
            "leave-confirmation" => LeaveConfirmation(),
            "service-certificate" => ServiceCertificate(),
            _ => "{purpose}", // custom letter: purpose becomes the whole body
        };
        return Merge(body, ctx);
    }

    private static string Merge(string template, LetterMergeContext ctx) =>
        template
            .Replace("{workerFullName}", ctx.WorkerFullName)
            .Replace("{employeeNo}", ctx.EmployeeNo)
            .Replace("{jobTitle}", ctx.JobTitle ?? "n/a")
            .Replace("{grade}", ctx.Grade ?? "n/a")
            .Replace("{startDate}", ctx.StartDate?.ToString("d MMMM yyyy") ?? "n/a")
            .Replace("{legalEntityName}", ctx.LegalEntityName ?? "the Company")
            .Replace("{addressee}", ctx.Addressee)
            .Replace("{purpose}", ctx.Purpose)
            .Replace("{dateText}", ctx.DateText)
            .Replace("{verificationCode}", ctx.VerificationCode)
            .Replace("{basicSalaryMonthly}", ctx.BasicSalaryMonthly?.ToString("N2") ?? "as per payroll records")
            .Replace("{referenceText}", ctx.ReferenceText ?? "HRM/" + ctx.EmployeeNo);

    private static string EmploymentConfirmation() =>
        "{dateText}\n\nTo whom it may concern,\n\n"
        + "RE: CONFIRMATION OF EMPLOYMENT \u2014 {workerFullName} ({employeeNo})\n\n"
        + "This letter serves to confirm that {workerFullName} is employed by "
        + "{legalEntityName} in the capacity of {jobTitle} (Grade {grade}). "
        + "Their employment commenced on {startDate} and is ongoing.\n\n"
        + "This confirmation is issued at the request of the employee for the "
        + "purpose stated below and carries verification code {verificationCode}.\n\n"
        + "Purpose: {purpose}\n\nYours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string SalaryConfirmation() =>
        "{dateText}\n\nTo whom it may concern,\n\n"
        + "RE: CONFIRMATION OF SALARY \u2014 {workerFullName} ({employeeNo})\n\n"
        + "We confirm that {workerFullName} is employed by {legalEntityName} as "
        + "{jobTitle}, and that their basic monthly salary is {basicSalaryMonthly} "
        + "before statutory deductions.\n\n"
        + "This confirmation is issued for the following purpose and may be verified "
        + "using code {verificationCode}.\n\n"
        + "Purpose: {purpose}\n\nYours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string Reference() =>
        "{dateText}\n\nTo whom it may concern,\n\n"
        + "RE: EMPLOYMENT REFERENCE \u2014 {workerFullName} ({employeeNo})\n\n"
        + "{workerFullName} has been employed by {legalEntityName} since "
        + "{startDate} as {jobTitle}. During this time they have performed "
        + "their duties satisfactorily and we recommend them without reservation.\n\n"
        + "This reference is issued at their request (reference {referenceText}) "
        + "and carries verification code {verificationCode}.\n\n"
        + "Yours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string Visa() =>
        "{dateText}\n\nTo the relevant consular authority,\n\n"
        + "RE: VISA SUPPORT LETTER \u2014 {workerFullName} ({employeeNo})\n\n"
        + "This letter confirms that {workerFullName} is a bona fide employee of "
        + "{legalEntityName}, employed as {jobTitle} since {startDate}. "
        + "They are travelling for the following purpose: {purpose}.\n\n"
        + "Their employment and salary obligations with us remain unaffected during "
        + "the intended travel. Verification code: {verificationCode}.\n\n"
        + "Yours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string Bank() =>
        "{dateText}\n\nTo the relevant banking institution,\n\n"
        + "RE: EMPLOYMENT CONFIRMATION FOR BANKING PURPOSES \u2014 {workerFullName} ({employeeNo})\n\n"
        + "This letter confirms that {workerFullName} ({employeeNo}) is "
        + "employed by {legalEntityName} as {jobTitle} since {startDate}. "
        + "Their monthly basic salary is {basicSalaryMonthly} before deductions.\n\n"
        + "Purpose: {purpose}. Verification code: {verificationCode}.\n\n"
        + "Yours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string LeaveConfirmation() =>
        "{dateText}\n\nTo whom it may concern,\n\n"
        + "RE: LEAVE CONFIRMATION \u2014 {workerFullName} ({employeeNo})\n\n"
        + "This letter confirms that {workerFullName}, employed as "
        + "{jobTitle} with {legalEntityName}, has been granted approved "
        + "leave for the purpose of {purpose}.\n\n"
        + "The approval is recorded under reference {referenceText} and this "
        + "document carries verification code {verificationCode}.\n\n"
        + "Yours faithfully,\nHuman Resources\n{legalEntityName}";

    private static string ServiceCertificate() =>
        "{dateText}\n\nCERTIFICATE OF SERVICE \u2014 {workerFullName} ({employeeNo})\n\n"
        + "This certifies that {workerFullName} rendered service to "
        + "{legalEntityName} from {startDate} in the capacity of "
        + "{jobTitle} (Grade {grade}).\n\n"
        + "Purpose stated on request: {purpose}.\n\n"
        + "Issued with verification code {verificationCode}.\n\n"
        + "Human Resources\n{legalEntityName}";
}
