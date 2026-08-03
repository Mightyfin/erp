import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, KeyRound, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { api } from "@/mock/service";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { UndisclosableNotFound } from "@/platform/components/Sensitive";

export const Route = createFileRoute("/speak-up")({
  head: () => ({
    meta: [
      { title: "Protected disclosure" },
      { name: "description", content: "Raise a concern anonymously. No account required and minimal metadata is stored." },
      { property: "og:title", content: "Protected disclosure" },
      { property: "og:description", content: "Raise a concern anonymously. No account required and minimal metadata is stored." },
    ],
  }),
  component: SpeakUp,
});

const categories = [
  "Harassment or discrimination",
  "Fraud or financial irregularity",
  "Health and safety",
  "Conflict of interest",
  "Something else",
] as const;

/** A demo-valid access code so the "check status" path can be exercised. */
const DEMO_VALID_CODE = "SU-7742-DEMO";

function ReportForm() {
  const [ref, setRef] = useState<{ id: string; code: string } | null>(null);
  const [category, setCategory] = useState<string>(categories[0]);
  const [narrative, setNarrative] = useState("");
  const [identify, setIdentify] = useState("anonymous");
  const [understood, setUnderstood] = useState(false);

  const steps: FlowStep[] = [
    {
      id: "category",
      title: "What's this about",
      purpose: "A broad category is enough to start — you're not locked into it.",
      render: () => (
        <div className="max-w-lg">
          <Label htmlFor="category">Concern category</Label>
          <Select value={category} onValueChange={setCategory}>
            <SelectTrigger id="category" className="mt-1"><SelectValue /></SelectTrigger>
            <SelectContent>
              {categories.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
            </SelectContent>
          </Select>
          <div className="mt-4 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>If someone is in immediate danger, contact emergency services first. This form is not monitored in real time.</span>
          </div>
        </div>
      ),
    },
    {
      id: "narrative",
      title: "Tell us what happened",
      purpose: "Dates, people involved and what you saw or experienced — as much or as little as you're comfortable sharing.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <div>
            <Label htmlFor="narrative">What happened</Label>
            <Textarea id="narrative" className="mt-1" rows={6} value={narrative} onChange={(e) => setNarrative(e.target.value)} />
          </div>
          <div className="rounded-md border border-dashed p-4 text-center text-xs text-muted-foreground">
            Attach evidence if you have it (mock upload — file metadata is minimized, nothing is stored).
          </div>
        </div>
      ),
    },
    {
      id: "identify",
      title: "Anonymous or identified",
      purpose: "Reporting anonymously is always available. Identifying yourself can help an investigation but is never required.",
      render: () => (
        <fieldset className="max-w-lg">
          <legend className="sr-only">Reporting mode</legend>
          <RadioGroup value={identify} onValueChange={setIdentify} className="space-y-2">
            <label className="flex items-start gap-2 text-sm">
              <RadioGroupItem value="anonymous" className="mt-0.5" />
              <span>Stay anonymous — no name, employee number or contact detail is requested.</span>
            </label>
            <label className="flex items-start gap-2 text-sm">
              <RadioGroupItem value="identified" className="mt-0.5" />
              <span>Identify myself to the investigator (optional, not shown here in this demo).</span>
            </label>
          </RadioGroup>
        </fieldset>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "What happens to this information before you send it.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-md border bg-surface-muted px-3 py-2"><dt className="text-xs text-muted-foreground">Category</dt><dd className="text-sm font-medium">{category}</dd></div>
            <div className="rounded-md border bg-surface-muted px-3 py-2"><dt className="text-xs text-muted-foreground">Reporting as</dt><dd className="text-sm font-medium">{identify === "anonymous" ? "Anonymous" : "Identified"}</dd></div>
          </dl>
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-1" checked={understood} onChange={(e) => setUnderstood(e.target.checked)} />
            <span>
              I understand this report is routed to conflict-checked, restricted investigators only,
              that unnecessary technical metadata is minimized, and that full anonymity cannot be
              guaranteed where law or safety requires escalation.
            </span>
          </label>
        </div>
      ),
    },
  ];

  return (
    <GuidedFlow
      flowId="speak-up-new"
      steps={steps}
      submitLabel="Submit report"
      onSubmit={async () => {
        const r = await api.submit("speak-up", { category, narrative, identify });
        setRef({ id: `${DEMO_VALID_CODE.split("-")[0]}-${r.id}`, code: DEMO_VALID_CODE });
      }}
      submitted={
        ref ? (
          <NextSteps
            reference={`Reference ${ref.id} · Access code ${ref.code}`}
            title="Report submitted"
            steps={[
              "Save your access code now — it's the only way to check status or add information later, and we cannot recover it for you.",
              "A conflict-checked, restricted investigator will triage this. You will not be identified unless you chose to be.",
              "Use \"Check on a previous report\" above at any time with your access code.",
            ]}
          />
        ) : undefined
      }
    />
  );
}

function StatusCheck() {
  const [code, setCode] = useState("");
  const [checked, setChecked] = useState<string | null>(null);

  return (
    <div className="max-w-lg space-y-4 rounded-lg border bg-surface p-5">
      <div>
        <Label htmlFor="access-code">Access code</Label>
        <Input id="access-code" className="mt-1" value={code} onChange={(e) => setCode(e.target.value)} placeholder="SU-XXXX-XXXX" />
      </div>
      <Button onClick={() => setChecked(code.trim())}>Check status</Button>

      {checked ? (
        checked === DEMO_VALID_CODE ? (
          <div className="rounded-md border border-success/30 bg-success-soft p-4 text-sm">
            <p className="font-medium text-success">In review</p>
            <p className="mt-1 text-foreground">
              Your report is with a restricted investigator. Last update: evidence review in
              progress. No further action is needed from you right now.
            </p>
          </div>
        ) : (
          <UndisclosableNotFound reference={checked} />
        )
      ) : null}
    </div>
  );
}

function SpeakUp() {
  const [mode, setMode] = useState<"report" | "status">("report");

  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-10 sm:px-6">
      <PageHeader
        eyebrow="Protected disclosure"
        title="Speak up"
        description="Raise a concern anonymously. No account is required and no employee data is requested unless you choose to identify yourself."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
            <ShieldCheck className="size-3.5" aria-hidden />
            Independent of ordinary HR administration
          </span>
        }
      />

      <div className="flex gap-2" role="tablist" aria-label="Speak up mode">
        <Button variant={mode === "report" ? "default" : "outline"} onClick={() => setMode("report")}>
          Report a concern
        </Button>
        <Button variant={mode === "status" ? "default" : "outline"} onClick={() => setMode("status")} className="gap-2">
          <KeyRound className="size-4" aria-hidden />
          Check on a previous report
        </Button>
      </div>

      {mode === "report" ? <ReportForm /> : <StatusCheck />}
    </div>
  );
}
