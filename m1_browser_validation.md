# Milestone 1 browser validation evidence

Date: 2026-08-22

The deployed page `https://erp.newworldcargo.com/hrm/time/operations` loaded successfully after the session check. The page title remained **Time operations** and displayed a production overtime review queue with the text: “Overtime is derived from persisted attendance and shift rules. Only approved hours enter payroll; released payroll marks the source attendance as paid.”

The queue rendered **“No derived overtime records found.”** against the live tenant dataset, which is expected because no new attendance record with derived overtime has been created yet. The page did not show mock overtime rows, a release-gated “Not in this release” placeholder, or a runtime error. The decision-reason input and operational controls were present. The page returned HTTP 200 through the production domain.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-22_21-12-12_7678.webp`.

This is UI availability evidence only; it is not overtime functional UAT evidence. Functional UAT must use a correction-safe synthetic attendance record and verify pending -> approved/rejected -> payroll inclusion/idempotency -> paid linkage.


## Post-UAT browser revalidation

After the live UAT import, decision, and payroll release, the same production route rendered two real records: `UAT Eunice · 09/04/2026 · 2.00 hour(s) · ×1.50 · total 10.00h · rejected` with the recorded rejection reason, and `UAT Eunice · 09/03/2026 · 3.00 hour(s) · ×1.50 · total 11.00h · paid`. The Recent operational history also displayed `m1-overtime-uat.csv · completed · 2 imported · 0 rejected`. No mock rows or runtime error were visible.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-22_21-20-13_1911.webp`.


## Milestone 1A workflow-led UI revalidation — 2026-08-23

The redesigned production page now opens as **Overtime & attendance** with the purpose stated in user language: review attendance-derived overtime, make safe decisions, and hand approved hours to payroll. A four-step workflow strip—Import, Review, Approve, Payroll—makes the lifecycle visible. Queue summary cards show Needs review, Approved, Rejected, and Paid in payroll counts and hours.

The live browser showed 3 real records: one Needs review row for UAT Alice with an inline decision note field and `Approve hours` action, one rejected row with its reason, and one paid row with a payroll run and line reference. The queue tabs correctly filtered Approved (empty state), then Paid (historical payroll-linked record), without navigation or runtime errors. The primary `Import attendance` action is visible at the page header; attendance and leave tools are moved into a progressive-disclosure section rather than competing with the review queue. The page contains no visible mock/demo rows.

Screenshots: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_05-41-47_2374.webp`, `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_05-41-54_9604.webp`, and `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_05-42-02_6103.webp`.


## Primary action interaction test — 2026-08-23

The header-level **Import attendance** action was activated from the top of the page. It opened the collapsed **Attendance and leave tools** section and scrolled the browser to the import form, confirming that the primary action is connected to the correct next step rather than acting as a decorative button. The tools section preserves clear grouping between attendance import, shift setup, leave operations, and escalation.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_05-42-25_6418.webp`.
