# Frontend-Only Build Contract

## Scope

The initial implementation builds the HRM user interface and interaction behavior only.

Included:

- Routes and layouts
- Responsive components
- Role-based presentation
- Forms and validation behavior
- Mock workflows and state transitions
- Accessible interaction
- Mock data and deterministic scenarios
- Loading, empty, error and restricted states

Not included:

- Production authentication
- Database writes
- Payroll calculation engine
- Real notifications
- Real file storage
- Real statutory submissions
- Production integrations

## Architecture boundary

Frontend code consumes interfaces such as:

- `IdentityClient`
- `PeopleClient`
- `WorkflowClient`
- `TimeClient`
- `PayrollClient`
- `DocumentClient`
- `ReportingClient`
- `ProtectedDisclosureClient`

Mock implementations return realistic states, delays and failures. UI components must not import mock data directly.

## State model

Each workflow mock supports:

- Initial/loading
- Empty
- Draft
- Valid and invalid form
- Submitted
- Pending approval
- Returned
- Rejected
- Approved
- Processing
- Completed
- Failed/recoverable
- Restricted/not found

## Mock-data rules

- Use fictional people and organizations.
- Do not use real contact, payroll or identity data.
- Include multiple entities, branches, departments and worker types.
- Include permission differences.
- Include long names, missing optional data and localization variations.
- Include historical, future-effective and corrected records.

## Component inventory

### Shell

- Application frame
- Workspace switcher
- Organization context switcher
- Global search
- Command palette
- Notification panel

### Data display

- Metric summary
- Work queue
- Filterable list
- Responsive record card
- Record header
- Status badge
- Timeline
- Change comparison
- Calculation explanation

### Input

- Guided step flow
- Effective-date control
- Organization selector
- Person selector
- Evidence upload
- Policy acknowledgement
- Approval decision
- Sensitive reveal

### Feedback

- Inline validation
- Error summary
- Empty state
- Restricted state
- Degraded-service state
- Progress and resumable processing
- Toast for low-risk confirmation only

## Acceptance gates

### Usability

- Primary tasks are reachable within three navigation decisions.
- Every page has one clear primary action.
- Users can identify status and next step without opening audit details.
- Long forms save drafts and restore progress.
- Returning from a record preserves list context.

### Accessibility

- Keyboard-only completion is possible.
- Focus is visible and predictable.
- Forms have programmatic labels and described errors.
- Dialog focus is trapped and restored.
- Status does not rely on color.
- Responsive layouts work at 320 CSS pixels.

### Permission presentation

- Unauthorized actions are absent or clearly disabled with an appropriate explanation.
- Restricted record existence is not disclosed.
- Sensitive fields remain masked in lists and notifications.
- Workspace switching never expands permission.

### Frontend quality

- Routes are stable and deep-linkable.
- Components are shared across workflows.
- Mock services are replaceable.
- Loading and error states are implemented, not left as TODOs.
- No backend secrets or credentials exist in frontend code.

## First build milestone

Build a coherent vertical slice:

1. Initial HR admin setup guide
2. Application shell
3. Employee and manager workspaces
4. Work queue
5. Employee profile
6. Leave request
7. Leave approval
8. Attendance correction
9. HR request
10. Payslip view
11. Anonymous protected-disclosure intake

This slice exercises the admin's day-one setup path, navigation, forms, permissions, approvals, status, sensitive data, responsive behavior and mock service boundaries.

