# M31 — Employee self-service expansion

M31 completes the routine employee workspace and makes the OIDC subject—not a browser-supplied worker ID—the ownership boundary for every personal record.

## Delivered

- A unified **My HR workspace** summarising requests, leave, payslips, documents, letters, profile linkage, and unread HR notifications.
- A subject-scoped notification inbox backed by the HRM outbox, with individual and mark-all-read state.
- Ownership-protected payslip detail and document generation/download.
- Full personal HR-request history and employee replies, with internal HR notes removed from employee responses.
- Subject-scoped letter requests, history, and generated-letter downloads.
- A persistent personal-document area for identity, qualification, medical, and certificate uploads and downloads.
- Subject-scoped profile and leave links consolidated into the self-service workspace.
- Role hardening on shared HR request, letter, payslip-generation, and document APIs so employee callers cannot use administrative paths.

## Ownership rules

Every `/hrm/me/*` operation resolves the worker from the authenticated OIDC subject. Any worker ID supplied in an employee request is ignored. Foreign notification, request, payslip, letter, or document IDs are rejected. Restricted documents and internal HR notes are never returned through self-service APIs.

Personal document files are stored on the persistent `hrm_employee_documents` production volume. Failed validation removes the temporary file so rejected uploads do not accumulate.

## Verification

- Backend tests cover notification ownership and read state, private request-note filtering, foreign request/reply denial, personal-document filtering, foreign document denial, allowed upload categories, and letter ownership.
- The frontend production bundle and targeted lint must pass.
- Playwright exercises the deployed employee workspace, notification read state, personal document list, and upload journey.
