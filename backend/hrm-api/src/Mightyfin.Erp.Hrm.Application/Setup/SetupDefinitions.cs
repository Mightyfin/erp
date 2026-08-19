namespace Mightyfin.Erp.Hrm.Application.Setup;
/// <summary>M49: catalog of setup wizard steps and their gating rules. The list
/// order defines display order; the Mandatory property defines the minimum
/// viable gate (organisation + structure + employment basics + payroll
/// confirmation are required before payroll usage; the rest are optional and
/// surface in the after-onboarding checklist). M49 lessons doc: never force
/// the operator to complete every step — gating is the prefix only.</summary>
public static class SetupDefinitions
{
    public const string StatusPending = "pending";
    public const string StatusComplete = "complete";
    public const string StatusResetting = "resetting";

    public record StepDef(string Key, string Label, string Description, bool Mandatory);

    /// <summary>Step order matches the natural HR dependency chain validated
    /// in the M49 lessons document: Organisation → Structure → Employment →
    /// Working time → Leave → Payroll → Policies → Roles → Employees.</summary>
    public static readonly IReadOnlyList<StepDef> Steps =
    [
        new("organisation",  "Organisation",      "Name, trading name, registration number, TPIN, contact details",  true),
        new("structure",     "Structure",         "Branches, departments, cost centres and work locations",          true),
        new("employment",    "Employment",        "Job grades, positions and reporting lines",                       true),
        new("working-time",  "Working time",      "Working days, hours, shifts and public holidays",                 false),
        new("leave",         "Leave",             "Leave types, entitlements and approval workflow",                 true),
        new("payroll",       "Payroll",           "Pay cycle, components and Zambian statutory confirmation",        true),
        new("policies",      "Policies",          "Onboarding, probation, transfers and exit rules",                 false),
        new("roles",         "Roles & access",    "HR administrators and branch access assignments",                 true),
        new("employees",     "Employees",         "Add your first employees manually or by import",                  true),
    ];

    /// <summary>Steps that are allowed to complete only after the mandatory
    /// prefix before them has finished. Optional steps may complete at any
    /// time. This is order gating, not lockout — it keeps the wizard linear
    /// without blocking operators who only need the essentials.</summary>
    public static bool MayComplete(string stepKey, IReadOnlySet<string> completed)
    {
        var index = Enumerable.Range(0, Steps.Count).FirstOrDefault(i => Steps[i].Key == stepKey, -1);
        if (index < 0) return false;
        if (!Steps[index].Mandatory) return true;
        for (var i = 0; i < index; i++)
            if (Steps[i].Mandatory && !completed.Contains(Steps[i].Key))
                return false;
        return true;
    }

    public static double CompletionPercent(IReadOnlySet<string> completed)
    {
        if (Steps.Count == 0) return 0;
        var done = Steps.Count(s => completed.Contains(s.Key));
        return Math.Round(100.0 * done / Steps.Count, 0);
    }
}
