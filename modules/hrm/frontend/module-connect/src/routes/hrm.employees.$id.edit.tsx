import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Info } from "lucide-react";
import { employeeProfileApi } from "@/mock/employeeprofile";
import { branchOptions, departmentOptions, gradeOptions, workLocationOptions } from "@/mock/reference";
import { EMPLOYMENT_TYPES } from "@/mock/types";
import { api } from "@/mock/service";
import { ApiError } from "@/platform/api-client";
import { adaptWorkerProfile, adaptWorkers, realApi } from "@/platform/use-api";
import type { Employee } from "@/mock/types";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { EditPage } from "@/platform/components/EditPage";
import type { EditSection } from "@/platform/components/EditPage";
import { LoadingState, RestrictedState } from "@/platform/components/States";
import { feedback } from "@/platform/feedback";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/employees/$id/edit")({
  head: () => ({
    meta: [
      { title: "Edit employee — Mightyfin ERP HRM" },
      { name: "description", content: "Edit an employee record: personal details, job and grade, where they work, and the pay details payroll relies on." },
      { property: "og:title", content: "Edit employee — Mightyfin ERP HRM" },
      { property: "og:description", content: "Edit personal details, job, location and pay details on an employee record." },
    ],
  }),
  component: EditEmployee,
});


/** Zambian NRC: six digits, two digits, one digit — 123456/78/9. */
const NRC = /^\d{6}\/\d{2}\/\d$/;
const SALUTATIONS = ["Mr", "Mrs", "Ms", "Dr", "Prof"];
const GENDERS = ["Female", "Male", "Prefer not to say"];
const MARITAL = ["Single", "Married", "Divorced", "Widowed", "Prefer not to say"];
const BANKS = ["Zanaco", "Stanbic", "FNB", "Absa", "Indo Zambia", "Access Bank"];
const PAYMENT_METHODS = ["Bank transfer", "Mobile money", "Cash", "Paid through accounts payable, not payroll"];
const BLOOD_GROUPS = ["A+", "A−", "B+", "B−", "AB+", "AB−", "O+", "O−"];
const RELATIONSHIPS = ["Spouse", "Parent", "Sibling", "Child", "Friend", "Other"];
const SHIFTS = ["Day shift, Monday to Friday", "Rotating shift", "Night shift", "Site roster — 14 on 7 off"];

function EditEmployee() {
  const { id } = Route.useParams();
  const navigate = useNavigate();
  // Real backend: the employee list carries the same shape as the mock
  // employee, so one hook covers both modes (real falls back to mock shape
  // after adaptation when switched on).
  const realState = useMock(
    () =>
      realApi.employees().then((page) => {
        const workers = adaptWorkers(page);
        // Mock ids are `w-NNNN`; in real mode the route id is the mock id
        // `w-1001`, which maps to employee number SMK001 on the seeded row.
        const match = workers.find((w) => w.id === id || w.employeeNo === id);
        return match ?? null;
      }),
    [id],
  );
  const state = USE_REAL ? realState : useMock(() => api.employee(id), [id]);
  const profileState = useMock(
    () => (USE_REAL ? realApi.worker(id).then(adaptWorkerProfile) : employeeProfileApi.profile(id)),
    [id],
  );

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={4}>
        {(employee) => {
          if (!employee) return <RestrictedState />;
          // The form seeds its state once, on mount, so both records must be
          // in hand before it renders — otherwise it opens with blank fields.
          if (profileState.loading) return <LoadingState rows={4} />;
          const pr = profileState.data;

          // Edit mode: a field that the seeded record has never had cannot be
          // required — otherwise editing an existing record is blocked by the
          // same validators used for a brand-new hire. Keep the validators
          // for correctness when a value is entered, but relax "required"
          // on fields that are blank in the current record.
          const sections: EditSection[] = [
            {
              id: "identity",
              title: "Identity",
              description: "As it appears on the NRC. Payroll and the bank both check the legal name against it.",
              fields: [
                { name: "salutation", label: "Salutation", type: "select", options: SALUTATIONS, required: true },
                { name: "fullName", label: "Full legal name", required: true, hint: "Must match the NRC." },
                { name: "preferredName", label: "Preferred name", hint: "Used everywhere except legal documents." },
                { name: "gender", label: "Gender", type: "select", options: GENDERS, required: true },
                { name: "maritalStatus", label: "Marital status", type: "select", options: MARITAL, required: true },
                { name: "nationality", label: "Nationality", required: true },
                { name: "homeTown", label: "Home town" },
                {
                  name: "dateOfBirth",
                  label: "Date of birth",
                  type: "date",
                  required: !!pr?.dateOfBirth,
                  validate: (v) => {
                    if (!v) return null;
                    const age = (Date.now() - new Date(v).getTime()) / 31_557_600_000;
                    if (age < 15) return "Below the minimum working age in Zambia.";
                    if (age > 75) return "Check this date — it gives an age over 75.";
                    return null;
                  },
                },
                {
                  name: "nationalId",
                  label: "NRC number",
                  required: !!employee.nationalId,
                  hint: "Format 123456/78/9.",
                  validate: (v) => (v && !NRC.test(v) ? "An NRC looks like 123456/78/9." : null),
                },
                { name: "passportNo", label: "Passport number" },
                {
                  name: "passportExpiry",
                  label: "Passport expires",
                  type: "date",
                  validate: (v, all) =>
                    v && !all.passportNo ? "Give the passport number as well, or clear this date." : null,
                },
              ],
            },
            {
              id: "contact",
              title: "Contact",
              description: "Where to reach this person, at work and outside it.",
              fields: [
                {
                  name: "email",
                  label: "Work email",
                  required: !!employee.email,
                  validate: (v) => (v && !v.includes("@") ? "Enter a complete email address." : null),
                },
                {
                  name: "personalEmail",
                  label: "Personal email",
                  hint: "Used for payslips after someone leaves, when the work address is closed.",
                  validate: (v) => (v && !v.includes("@") ? "Enter a complete email address." : null),
                },
                {
                  name: "phone",
                  label: "Mobile",
                  required: !!employee.phone,
                  validate: (v) => (v && !v.startsWith("+260") ? "Use the full international format, starting +260." : null),
                },
                { name: "alternatePhone", label: "Alternate phone" },
                { name: "residentialAddress", label: "Residential address", type: "textarea", required: !!pr?.residentialAddress },
                { name: "postalAddress", label: "Postal address", type: "textarea" },
              ],
            },
            {
              id: "kin",
              title: "Next of kin",
              description: "Who is called first if something happens at work. This should never be blank.",
              fields: [
                { name: "emergencyName", label: "Name", required: !!pr?.emergency[0]?.name },
                { name: "emergencyRelationship", label: "Relationship", type: "select", options: RELATIONSHIPS, required: false },
                {
                  name: "emergencyPhone",
                  label: "Phone",
                  required: !!pr?.emergency[0]?.phone,
                  validate: (v, all) =>
                    v && v === all.phone
                      ? "This is the employee's own number. An emergency contact has to be someone else."
                      : null,
                },
              ],
            },
            {
              id: "job",
              title: "Job and grade",
              description: "Grade drives the pay range, so a grade change is a pay change and goes for approval.",
              fields: [
                { name: "jobTitle", label: "Job title", required: true },
                { name: "department", label: "Department", type: "select", options: departmentOptions, required: true },
                { name: "grade", label: "Grade", type: "select", options: gradeOptions, required: !!employee.grade },
                { name: "employmentType", label: "Employment type", type: "select", options: [...EMPLOYMENT_TYPES], required: true },
                { name: "reportsTo", label: "Reports to", required: !!pr?.reportsTo },
                { name: "costCentre", label: "Cost centre", required: !!pr?.costCentre, hint: "Where this person's cost lands in the ledger." },
                {
                  name: "noticePeriodDays",
                  label: "Notice period (days)",
                  type: "number",
                  required: true,
                  validate: (v) =>
                    v && Number(v) < 30 ? "Zambian law requires at least 30 days for a permanent contract." : null,
                },
                { name: "probationEndsOn", label: "Probation ends", type: "date" },
              ],
            },
            {
              id: "place",
              title: "Where they work",
              description: "Branch decides the calendar and the public holidays that count as non-working days.",
              fields: [
                { name: "branch", label: "Branch", type: "select", options: branchOptions, required: !!employee.branch },
                { name: "location", label: "Work location", type: "select", options: workLocationOptions, required: !!employee.location },
                { name: "shiftPattern", label: "Shift pattern", type: "select", options: SHIFTS, required: !!pr?.shiftPattern },
                { name: "holidayCalendar", label: "Holiday calendar", required: !!pr?.holidayCalendar },
                { name: "leavePolicy", label: "Leave policy", required: !!pr?.leavePolicy },
                { name: "attendanceDeviceId", label: "Attendance device ID" },
              ],
            },
            {
              id: "pay",
              title: "Pay and bank",
              description: "Payroll reads these directly. A wrong account number is the most common cause of a failed payment.",
              fields: [
                { name: "payGroup", label: "Pay group", required: !!pr?.payGroup },
                { name: "paymentMethod", label: "Payment method", type: "select", options: PAYMENT_METHODS, required: true },
                { name: "bankName", label: "Bank", type: "select", options: BANKS, required: true },
                { name: "bankBranch", label: "Branch", required: !!pr?.bankBranch },
                {
                  name: "bankAccount",
                  label: "Account number",
                  required: !!(pr?.bankAccount ?? employee.bankAccount),
                  hint: "Verified against the bank before the next payment run.",
                  validate: (v) =>
                    v && v.replace(/\s/g, "").length < 10
                      ? "A Zambian account number is at least 10 digits. Check it against the bank letter."
                      : null,
                },
              ],
            },
            {
              id: "statutory",
              title: "Statutory registrations",
              description: "A missing NAPSA or NHIMA number stops this employee being included in a pay run.",
              fields: [
                { name: "tpin", label: "TPIN (ZRA)", required: !!pr?.tpin },
                { name: "napsaNumber", label: "NAPSA number", required: !!pr?.napsaNumber },
                { name: "nhimaNumber", label: "NHIMA number", required: !!pr?.nhimaNumber },
              ],
            },
            {
              id: "support",
              title: "Support and adjustments",
              description: "Recorded only where it changes how someone is supported at work.",
              fields: [
                { name: "bloodGroup", label: "Blood group", type: "select", options: BLOOD_GROUPS },
                { name: "workplaceAdjustments", label: "Workplace adjustments", type: "textarea" },
                { name: "dietaryRequirements", label: "Dietary requirements" },
              ],
            },
            {
              id: "effective",
              title: "When it takes effect",
              description: "A record is a history, not a current state. Every change is dated so a past payslip stays reproducible.",
              fields: [
                {
                  name: "effectiveFrom",
                  label: "Effective from",
                  type: "date",
                  required: true,
                  validate: (v) =>
                    v && v < "2026-08-01"
                      ? "July 2026 payroll is already released, so a change cannot start before 1 August."
                      : null,
                },
                {
                  name: "reason",
                  label: "Reason for the change",
                  type: "textarea",
                  hint: "Whoever approves this, and anyone auditing it later, reads this line.",
                },
              ],
            },
          ];
          const liveFields = new Set([
            "fullName", "preferredName", "nationality", "dateOfBirth", "nationalId",
            "passportNo", "email", "phone", "jobTitle", "grade", "tpin",
            "napsaNumber", "nhimaNumber",
          ]);
          const visibleSections = USE_REAL
            ? sections
                .map((section) => ({
                  ...section,
                  description: "These fields are stored on the live worker record.",
                  fields: section.fields?.filter((field) => liveFields.has(field.name)),
                }))
                .filter((section) => section.fields?.length)
            : sections;

          return (
            <EditPage
              title={employee.fullName}
              reference={employee.employeeNo}
              description={USE_REAL ? "Only fields backed by the live worker record are editable here." : "Changes are dated and go into the employee's history. Anything affecting pay is approved before it reaches a run."}
              sections={visibleSections}
              initial={{
                salutation: pr?.salutation ?? "Mr",
                fullName: employee.fullName,
                preferredName: employee.preferredName ?? "",
                gender: pr?.gender ?? "Prefer not to say",
                maritalStatus: pr?.maritalStatus ?? "Single",
                nationality: pr?.nationality ?? "Zambian",
                homeTown: pr?.homeTown ?? "",
                dateOfBirth: pr?.dateOfBirth ?? "",
                nationalId: employee.nationalId,
                passportNo: pr?.passportNo ?? "",
                passportExpiry: pr?.passportExpiry ?? "",

                email: employee.email ?? "",
                personalEmail: pr?.personalEmail ?? "",
                phone: employee.phone ?? "",
                alternatePhone: pr?.alternatePhone ?? "",
                residentialAddress: pr?.residentialAddress ?? "",
                postalAddress: pr?.postalAddress ?? "",

                emergencyName: pr?.emergency[0]?.name ?? "",
                emergencyRelationship: pr?.emergency[0]?.relationship ?? "Spouse",
                emergencyPhone: pr?.emergency[0]?.phone ?? "",

                jobTitle: employee.jobTitle,
                department: employee.department,
                grade: employee.grade,
                employmentType: employee.employmentType,
                reportsTo: pr?.reportsTo ?? "",
                costCentre: pr?.costCentre ?? "",
                noticePeriodDays: String(pr?.noticePeriodDays ?? 30),
                probationEndsOn: pr?.probationEndsOn ?? "",

                branch: employee.branch,
                location: employee.location,
                shiftPattern: pr?.shiftPattern ?? SHIFTS[0],
                holidayCalendar: pr?.holidayCalendar ?? "",
                leavePolicy: pr?.leavePolicy ?? "",
                attendanceDeviceId: pr?.attendanceDeviceId ?? "",

                payGroup: pr?.payGroup ?? "",
                paymentMethod: pr?.paymentMethod ?? "Bank transfer",
                bankName: pr?.bankName ?? "Zanaco",
                bankBranch: pr?.bankBranch ?? "",
                bankAccount: pr?.bankAccount ?? employee.bankAccount,

                tpin: pr?.tpin ?? "",
                napsaNumber: pr?.napsaNumber ?? "",
                nhimaNumber: pr?.nhimaNumber ?? "",

                bloodGroup: pr?.bloodGroup ?? "",
                workplaceAdjustments: pr?.workplaceAdjustments ?? "",
                dietaryRequirements: pr?.dietaryRequirements ?? "",

                effectiveFrom: "2026-09-01",
                reason: "",
              }}
              saveLabel="Save the change"
              footerNote={USE_REAL ? "Saved changes are written to the live HRM worker record and audited by the API." : "Nothing reaches payroll until the change is approved."}
              onCancel={() => navigate({ to: "/hrm/employees/$id", params: { id } })}
              onSave={async (values, changed) => {
                if (USE_REAL) {
                  const body: Record<string, unknown> = {};
                  if (changed.includes("fullName")) {
                    const parts = values.fullName.trim().split(/\s+/);
                    body.firstName = parts[0] ?? "";
                    body.middleName = parts.slice(1, parts.length - 1).join(" ") || null;
                    body.lastName = parts[parts.length - 1] ?? "";
                  }
                  if (changed.includes("email")) body.email = values.email || null;
                  if (changed.includes("phone")) body.phone = values.phone || null;
                  if (changed.includes("preferredName")) body.preferredName = values.preferredName || null;
                  if (changed.includes("nationalId")) body.nrc = values.nationalId || null;
                  if (changed.includes("dateOfBirth")) body.dateOfBirth = values.dateOfBirth || null;
                  if (changed.includes("passportNo")) body.passportNo = values.passportNo || null;
                  if (changed.includes("nationality")) body.nationality = values.nationality || null;
                  if (changed.includes("tpin")) body.tpin = values.tpin || null;
                  if (changed.includes("napsaNumber")) body.napsaNumber = values.napsaNumber || null;
                  if (changed.includes("nhimaNumber")) body.nhimaNumber = values.nhimaNumber || null;
                  if (changed.includes("jobTitle")) body.jobTitle = values.jobTitle || null;
                  if (changed.includes("grade")) body.grade = values.grade || null;
                  if (changed.includes("employmentType"))
                    body.workerType =
                      values.employmentType === "Contractor"
                        ? "contractor"
                        : values.employmentType === "Intern"
                          ? "intern"
                          : "employee";
                  try {
                    if (Object.keys(body).length) {
                      await realApi.updateWorker(id, body);
                    }
                    feedback.saved(`${employee.fullName} updated in the live HRM record.`);
                    navigate({ to: "/hrm/employees/$id", params: { id } });
                    return;
                  } catch (err) {
                    const msg = err instanceof ApiError ? err.message : String(err);
                    feedback.blocked("The change could not be saved.", msg);
                    return;
                  }
                }
                const paySensitive = changed.filter((c) =>
                  [
                    "grade",
                    "bankAccount",
                    "bankName",
                    "paymentMethod",
                    "employmentType",
                    "fullName",
                    "payGroup",
                    "tpin",
                    "napsaNumber",
                    "nhimaNumber",
                  ].includes(c),
                );
                if (paySensitive.length) {
                  feedback.submitted(
                    `${changed.length} change${changed.length === 1 ? "" : "s"} sent for approval.`,
                    `${paySensitive.length} of them ${
                      paySensitive.length === 1 ? "affects" : "affect"
                    } pay, so a second person signs off before payroll sees it. Effective ${values.effectiveFrom}.`,
                  );
                } else {
                  feedback.saved(
                    `${employee.fullName} updated, effective ${values.effectiveFrom}.`,
                    () => feedback.note("Change reverted."),
                  );
                }
                navigate({ to: "/hrm/employees/$id", params: { id } });
              }}
            />
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
