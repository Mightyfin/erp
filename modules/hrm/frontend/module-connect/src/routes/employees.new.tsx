import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Info } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { employees, entities } from "@/mock/data";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/employees/new")({
  head: () => ({
    meta: [
      { title: "Add an employee — Meridian ERP HRM" },
      { name: "description", content: "Create an employee record, place them in the organisation, and hand over to onboarding." },
      { property: "og:title", content: "Add an employee — Meridian ERP HRM" },
      { property: "og:description", content: "Create an employee record, place them in the organisation, and hand over to onboarding." },
    ],
  }),
  component: NewEmployee,
});

const employmentTypes = ["Permanent", "Fixed term", "Part time", "Contractor", "Intern"] as const;

function NewEmployee() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [entityId, setEntityId] = useState(entities[0].id);
  const [branch, setBranch] = useState(entities[0].branches[0]);
  const [jobTitle, setJobTitle] = useState("");
  const [department, setDepartment] = useState("Operations");
  const [managerId, setManagerId] = useState("w-1002");
  const [employmentType, setEmploymentType] = useState<string>("Permanent");
  const [startDate, setStartDate] = useState("2026-09-01");
  const [endDate, setEndDate] = useState("");

  const entity = entities.find((e) => e.id === entityId)!;
  const fullName = [firstName, lastName].filter(Boolean).join(" ");
  const needsEndDate = employmentType === "Fixed term" || employmentType === "Intern";

  const steps: FlowStep[] = [
    {
      id: "identity",
      title: "Who is joining",
      purpose: "The minimum needed to create a record. Personal details they can complete themselves later.",
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="first">First name</Label>
            <Input id="first" className="mt-1" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="last">Last name</Label>
            <Input id="last" className="mt-1" value={lastName} onChange={(e) => setLastName(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="email">Work email (optional)</Label>
            <Input id="email" type="email" className="mt-1" value={email} onChange={(e) => setEmail(e.target.value)} />
            <p className="mt-1 text-xs text-muted-foreground">
              Leave blank if IT will issue it during onboarding.
            </p>
          </div>
          <p className="sm:col-span-2 flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              Do not enter bank details, national identifiers or health information here. The
              employee supplies those themselves during onboarding, under their own consent.
            </span>
          </p>
        </div>
      ),
    },
    {
      id: "placement",
      title: "Where they sit",
      purpose: "Entity and branch decide which policies, calendar and statutory rules apply.",
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="entity">Legal entity</Label>
            <Select
              value={entityId}
              onValueChange={(v) => {
                setEntityId(v);
                const e = entities.find((x) => x.id === v);
                if (e) setBranch(e.branches[0]);
              }}
            >
              <SelectTrigger id="entity" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {entities.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.name} · {e.country}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="branch">Branch</Label>
            <Select value={branch} onValueChange={setBranch}>
              <SelectTrigger id="branch" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {entity.branches.map((b) => (
                  <SelectItem key={b} value={b}>
                    {b}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="dept">Department</Label>
            <Select value={department} onValueChange={setDepartment}>
              <SelectTrigger id="dept" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {["Operations", "Manufacturing", "Logistics", "Finance", "People"].map((d) => (
                  <SelectItem key={d} value={d}>
                    {d}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="manager">Reports to</Label>
            <Select value={managerId} onValueChange={setManagerId}>
              <SelectTrigger id="manager" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.fullName} — {e.jobTitle}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      ),
    },
    {
      id: "employment",
      title: "Employment terms",
      purpose: "What kind of engagement this is, and when it starts.",
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="title">Job title</Label>
            <Input id="title" className="mt-1" value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="type">Employment type</Label>
            <Select value={employmentType} onValueChange={setEmploymentType}>
              <SelectTrigger id="type" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {employmentTypes.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="start">Start date</Label>
            <Input id="start" type="date" className="mt-1" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </div>
          {needsEndDate ? (
            <div className="sm:col-span-2">
              <Label htmlFor="end">
                End date <span className="text-danger">(required for {employmentType.toLowerCase()})</span>
              </Label>
              <Input id="end" type="date" className="mt-1" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
              <p className="mt-1 text-xs text-muted-foreground">
                An expiry alert is raised 60 days before this date.
              </p>
            </div>
          ) : null}
          {employmentType === "Contractor" ? (
            <p className="sm:col-span-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span>
                Contractors are engaged, not employed. Check the classification rules for{" "}
                {entity.country} before continuing — misclassification carries real liability.
              </span>
            </p>
          ) : null}
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and create",
      purpose: "Creating the record does not complete the hire — onboarding does.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            {[
              ["Name", fullName || "Not given"],
              ["Job title", jobTitle || "Not given"],
              ["Employment type", employmentType],
              ["Entity", entity.name],
              ["Branch", branch],
              ["Department", department],
              ["Reports to", employees.find((e) => e.id === managerId)?.fullName ?? "—"],
              ["Start date", startDate],
              ...(needsEndDate ? [["End date", endDate || "Not set"]] : []),
            ].map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-muted-foreground">
            The employee number is issued automatically. The record starts as <em>Pre-hire</em> and
            only becomes active on the start date.
          </p>

          <div className="rounded-md border border-warning/40 bg-warning-soft p-3">
            <p className="text-xs font-medium text-warning">
              Still needed before this person can be paid
            </p>
            <ul className="mt-1.5 list-inside list-disc space-y-0.5 text-xs text-foreground">
              <li>NRC number and date of birth</li>
              <li>Bank name, branch and account number</li>
              <li>TPIN, NAPSA and NHIMA registration numbers</li>
              <li>An emergency contact</li>
            </ul>
            <p className="mt-1.5 text-xs text-muted-foreground">
              A pay run will not include an employee missing any of these. You can complete them on
              the profile, or the employee supplies them during onboarding.
            </p>
          </div>
        </div>
      ),
    },
  ];

  if (ref) {
    return (
      <AppShell>
        <PageHeader eyebrow="People" title="Employee record created" />
        <NextSteps
          reference={`EMP-${ref}`}
          title={`${fullName || "The employee"} has been added as Pre-hire`}
          steps={[
            "Complete the profile — bank details and the NAPSA, NHIMA and TPIN numbers, without which payroll cannot include them.",
            "Record an emergency contact. It is the one field that should never be left blank.",
            "The record becomes Active automatically on the start date.",
          ]}
          actions={
            <>
              <Button asChild>
                <Link to="/employees">Complete the profile</Link>
              </Button>
              <Button variant="outline" asChild>
                <Link to="/employees">Back to employees</Link>
              </Button>
            </>
          }
        />
      </AppShell>
    );
  }

  return (
    <AppShell>
      <PageHeader
        eyebrow="People"
        title="Add an employee"
        description="Four short steps. This creates the record only — onboarding is a separate, tracked process."
        primaryAction={
          <Button variant="ghost" asChild>
            <Link to="/employees">Cancel</Link>
          </Button>
        }
      />
      <GuidedFlow
        flowId="employee-new"
        steps={steps}
        submitLabel="Create employee record"
        onSubmit={async () => {
          const r = await api.submit("employee", { fullName, jobTitle, entityId, branch, startDate });
          setRef(r.id);
        }}
      />
    </AppShell>
  );
}
