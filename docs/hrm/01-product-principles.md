# Product and Simplicity Principles

## Simplicity standard

Simple does not mean removing enterprise capability. It means revealing the right capability at the right time.

### The ten-second test

Within ten seconds, a user should be able to answer:

- Where am I?
- What needs my attention?
- What is the primary action?
- What information is required?
- What will happen after I submit?

### Interaction rules

- Present one primary action per screen or step.
- Keep common tasks within three navigation decisions from Home.
- Prefer short guided steps over one large form.
- Show advanced and uncommon options only when requested or triggered.
- Reuse the same status names, colors and timeline structure across HRM.
- Save long forms as drafts automatically.
- Explain calculations and eligibility in plain language.
- Preserve user input after recoverable errors.
- Confirm destructive, irreversible and high-impact actions.
- Provide undo where the business process permits reversal.
- Keep policy explanations beside the affected decision.

## Progressive disclosure

Use three layers:

1. **Summary:** essential value, current status and primary action.
2. **Details:** fields required to understand or complete the task.
3. **Advanced:** audit, calculation, integration and configuration details.

Do not place routine user actions beside system-administration controls.

## Navigation grouping threshold

Distinguish two kinds of navigation:

- **Persistent rail** (the always-visible top-level workspace sections, e.g. Home, People,
  Lifecycle, Recruitment...): learned by spatial position over repeated use, not re-scanned each
  time. Up to around 12 items is acceptable here because the user stops reading labels after the
  first few visits.
- **Scanned list** (anything a user opens and reads before choosing — a section's sub-items, a
  dropdown, a configuration list): must be re-read every time, so it is capped at roughly 8-9
  flat items. Past that, split into labeled sub-groups rather than leaving one long list, even if
  every item is individually justified. This applies recursively: a sub-group that itself grows
  past the same threshold gets split again.

The goal is that a user never has to scan more than about 8-9 items to find what they want at any
single scanned level, while the persistent rail can carry more because it isn't scanned, it's
recognized.

## Page-density rules

- Default list views show no more than seven essential columns.
- Additional columns belong in a configurable column picker.
- Use tabs only for stable peer sections; do not hide sequential steps in tabs.
- Avoid nested tabs.
- Use cards for summaries, not for every field.
- Use tables for comparison and operational queues, not prose.
- Do not use “More” as an unstructured feature warehouse.

## Forms

- Group fields by user intent.
- Mark optional fields explicitly instead of marking every required field.
- Use conditional fields only after the triggering choice.
- Validate locally when safe and again on submission.
- Place the error message beside the field and summarize errors at the top.
- Explain why sensitive information is requested.
- Show who can view a sensitive field.
- Display effective dates for changes that affect history or payroll.

## Status and trust

Every transactional record must show:

- Current status
- Submitted date
- Current owner or queue
- Next expected action
- Expected due date where applicable
- Approval or processing timeline
- Comments and evidence, subject to permission
- Available actions

## Accessibility

- Target WCAG 2.2 AA.
- All actions must be keyboard operable.
- Focus order follows the visual workflow.
- Status is never communicated by color alone.
- Modal dialogs are reserved for short, contained decisions.
- Long processes use full pages or drawers with stable URLs.
- Error summaries move focus and link to invalid fields.
- Charts have text summaries and accessible data alternatives.
- Motion respects reduced-motion preferences.

## Responsive behavior

- Mobile prioritizes employee and manager tasks.
- Primary actions remain reachable without horizontal scrolling.
- Operational tables collapse into structured record cards.
- Long forms become step-based flows.
- Check-in experiences show source, time, location policy and sync status.
- Sensitive information is protected from shoulder-surfing through masking and deliberate reveal.

## Configuration-first language

The UI must support configurable:

- Labels and terminology
- Fields and sections
- Workflows and approvals
- Policies and formulas
- Roles and data scopes
- Statuses where the domain permits configuration
- Templates and communications
- Country and industry packs

Configuration must not weaken entity isolation, authorization, audit integrity, historical truth or transactional safety.

