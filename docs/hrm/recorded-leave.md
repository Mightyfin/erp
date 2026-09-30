# Admin-recorded leave

HR administrators and HR operations can use `/hrm/leave/recorded` to record an already approved absence without creating an employee request or an approval workflow. The employee selector is independent of the administrator's own worker link.

Select employee, active leave type, start/end dates and a required approval reference or reason. Check days before saving. Leave must already have started; an end date after today represents approved leave still in progress. Non-consecutive dates need separate records. This initial form supports full days only.

Days exclude weekends and holidays using the assigned shift calendar, then branch calendar, then the default calendar. Missing calendars, inactive leave types, dates outside employment, overlapping requests, insufficient balances and locked payroll inputs are rejected. Evidence confirmation is required for types configured to require it. The existing employee request workflow is separate and unchanged.

For an open pay period, HR may record, edit or cancel leave while the payroll run is draft, locked or calculated. A change to a calculated run returns it to locked with an audit event, so payroll must be calculated again before approval. Runs in review or later and closed periods still block changes. Attendance entry retains its separate date-lock policy.

Save writes an approved leave request and a permanent taken-days ledger row marked `admin-recorded-leave` in a serializable transaction. There is no approval queue item or notification event. Edit recalculates the ledger deduction; cancel sets the deduction to zero and retains the cancelled record. Explicit audit entries retain the actor and before/after values. Records created through the employee workflow cannot be edited through this screen. There is no hard-delete action.

This screen does not allocate entitlements, configure missing leave types, or recalculate payroll. Use the dedicated screens for those tasks. Verify the quoted workday count against the employee's calendar before saving.

Validation covers create, quote, view, edit, cancel, balance restoration, concurrent duplicate prevention and payroll locks.
