import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "@tanstack/react-router";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from "@/components/ui/select";
import { realApi, useApi } from "@/platform/use-api";
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  ChevronRight,
  Plus,
  ShieldCheck,
  Sparkles,
  Trash2,
  Upload,
  X,
} from "lucide-react";

/**
 * M50 (+M50.11): true input setup wizard. Now rendered as a full page route
 * (/hrm/setup) — M50.11 removed the full-screen overlay cover (pageMode
 * prop); the shell keeps pending operators on this route instead. While the
 * organisation's setup state is "pending", the wizard is the only thing
 * reachable; the shell's gate re-steers anyone who lands on /hrm back here.
 *
 * Each step collects REAL configuration data through inline forms and writes
 * it to the backend via POST /hrm/setup/steps/{key}. Steps only advance on a
 * successful server write, mandatory steps cannot be skipped, and the Finish
 * button only lights up once the mandatory prefix is complete. The backend
 * owns the state machine — GET /hrm/setup/state returns "pending" only while
 * the wizard is unfinished, and "complete" lifts the cover for good.
 *
 * The wizard provisions the entire payroll-ready foundation: legal entity,
 * work calendar, branches and departments, leave types, the Zambian statutory
 * engine (NAPSA 5% ee/er with ceiling, NHIMA 1% ee, ZRA PAYE 2026 slabs),
 * the default pay group, the ZMW-STANDARD salary structure and an open pay
 * period for the current month. The existing configuration pages then become
 * read-only previews of what the wizard configured.
 *
 * Design language: frosted white blur + rotating two-arrow mark, matching
 * the M47 scope-switch overlay so both covers read as one family.
 */
type StepDto = {
  key: string;
  label: string;
  description: string;
  mandatory: boolean;
  completed: boolean;
  open: boolean;
};

const LEAVE_DEFAULTS = [
  { name: "Annual Leave", category: "paid", days: 24, evidence: false, carry: 0 },
  { name: "Sick Leave", category: "paid", days: 30, evidence: true, carry: 0 },
  { name: "Maternity Leave", category: "unpaid", days: 98, evidence: true, carry: 0 },
  { name: "Paternity Leave", category: "paid", days: 5, evidence: false, carry: 0 },
  { name: "Marriage Leave", category: "paid", days: 5, evidence: false, carry: 0 },
  { name: "Compassionate Leave", category: "paid", days: 5, evidence: false, carry: 0 },
];

const CONTRACT_DEFAULTS = [
  { name: "Permanent", probation: 90, notice: 30 },
  { name: "Fixed-term", probation: 30, notice: 14 },
  { name: "Casual", probation: 0, notice: 7 },
];

const PAYE_BANDS = [
  { band: "ZMW 0 – 5,100", rate: "0%" },
  { band: "ZMW 5,100.01 – 6,700", rate: "20%" },
  { band: "ZMW 6,700.01 – 8,400", rate: "30%" },
  { band: "Above ZMW 8,400", rate: "37.5%" },
];

// ---------- Employees step: user-driven column mapping ----------
// System fields the import cares about (FirstName/LastName mandatory).
type SystemField = { key: string; label: string; required: boolean };
const SYSTEM_FIELDS: SystemField[] = [
  { key: "FirstName", label: "First name", required: true },
  { key: "LastName", label: "Last name", required: true },
  { key: "Email", label: "Email", required: false },
  { key: "Phone", label: "Phone", required: false },
  { key: "JobTitle", label: "Job title", required: false },
  { key: "Grade", label: "Grade", required: false },
  { key: "StartDate", label: "Start date", required: false },
  { key: "OrgUnitName", label: "Department", required: false },
];

// A snapshot of the parsed spreadsheet: raw header cells + data rows.
type ParsedSheet = { headers: string[]; rows: string[][] };

// Mapping state: which system field is fed by which spreadsheet column.
// Number = column index (-1 = ignore). String `unit:<Name>` = a real org unit
// picked from the ones created earlier in the wizard (never guessed).
const UNIT_PREFIX = "unit:";
type Mapping = Record<string, number | string>;

// One blank manual-entry row — first/last are mandatory, the rest optional.
type Row = {
  first: string;
  last: string;
  email: string;
  phone: string;
  jobTitle: string;
  grade: string;
  startDate: string;
  orgUnitName: string;
};
const EMPTY_ROW: Row = { first: "", last: "", email: "", phone: "", jobTitle: "", grade: "", startDate: "", orgUnitName: "" };

function defaultMapping(headers: string[]): Mapping {
  // Never guess required columns — but hint optional ones by fuzzy matching.
  const map: Mapping = {};
  const norm = headers.map((h) => h.toLowerCase().replace(/[^a-z0-9]+/g, " ").trim());
  const find = (...terms: string[]) => {
    for (const t of terms) {
      const i = norm.findIndex((n) => n.split(" ").includes(t));
      if (i >= 0) return i;
    }
    return -1;
  };
  return {
    FirstName: find("first", "firstname", "given"),
    LastName: find("last", "lastname", "surname", "family"),
    Email: find("email", "e-mail", "mail"),
    Phone: find("phone", "mobile", "cell", "contact", "telephone", "tel"),
    JobTitle: find("title", "job", "position", "designation", "role"),
    Grade: find("grade", "level", "band", "rank"),
    StartDate: find("start", "joined", "join"),
    OrgUnitName: find("department", "dept", "unit", "branch"),
  };
}

function buildCanonical(rows: string[][], mapping: Mapping): string[][] {
  // Always emit 8 canonical columns in SYSTEM_FIELDS order, in CSV order.
  return rows.map((r) =>
    SYSTEM_FIELDS.map((f) => {
      const col = mapping[f.key];
      if (typeof col === "string" && col.startsWith(UNIT_PREFIX)) return col.slice(UNIT_PREFIX.length);
      if (typeof col !== "number" || col < 0 || col >= r.length) return "";
      return (r[col] ?? "").trim().replace(/^"|"$/g, "");
    })
  );
}

function slugify(v: string) {
  return v
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 24);
}

function isValidEmail(e: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e.trim());
}

function csvEscape(v: string) {
  if (!v) return "";
  return /[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
}

function setupErrorText(err: unknown): string {
  const raw = err as { message?: string };
  if (raw?.message) {
    const m = raw.message.toLowerCase();
    if (m.includes("setup-confined"))
      return "Only organisation-wide HR can run the setup wizard — branch-confined HR cannot.";
    if (m.includes("finish")) return "Complete the required steps first, then press Finish setup.";
    if (m.includes("401") || m.includes("unauthorized") || m.includes("unauthenticated"))
      return "Your sign-in session expired while you were filling this step — please refresh the page and sign in again, then continue where you left off.";
  }
  return "Something went wrong — try again.";
}

/** Rotating two-arrow switch mark — same glyph family as the M47 overlay. */
function SwitchGlyph() {
  return (
    <svg viewBox="0 0 48 48" fill="none" className="size-12 animate-spin-slow text-primary" aria-hidden>
      <path
        d="M10 17h22m0 0-6-6m6 6-6 6M38 31H16m0 0 6-6m-6 6 6 6"
        stroke="currentColor"
        strokeWidth="3.4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function WelcomeOverlay({ pageMode = false }: { pageMode?: boolean } = {}) {
  const api = useApi(async () => {
    const [state, steps] = await Promise.all([realApi.setupState(), realApi.setupSteps()]);
    return { state, steps: steps as StepDto[] };
  }, []);

  const [active, setActive] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [message, setMessage] = useState<{ text: string; kind: "error" | "info" } | null>(null);
  const [fading, setFading] = useState(false);
  const dialogRef = useRef<HTMLDivElement>(null);
  // Stable router reference so the completion effect (below) can navigate
  // so the completion effect can navigate once setup finishes.
  const routerRef = useRef(useRouter());

  const state = api.data?.state;
  const steps = api.data?.steps ?? [];
  const isComplete = state?.status === "complete";

  // Resume at the first incomplete mandatory step (or the backend's hint) so
  // returning to the wizard picks up exactly where the operator left off.
  const stepsByCompletion = useMemo(() => {
    const done = new Set(state?.completedSteps ?? []);
    return steps.map((s) => ({ ...s, done: done.has(s.key) }));
  }, [steps, state]);

  useEffect(() => {
    if (!active && stepsByCompletion.length) {
      const resume = stepsByCompletion.find((s) => !s.done);
      setActive(resume?.key ?? null);
    }
  }, [stepsByCompletion, active]);

  // M50.12: once the backend confirms setup completion, fade out and leave
  // the wizard page for the now-unlocked dashboard (the shell's pending-gate
  // only applies while setup is PENDING, so /hrm is reachable afterward).
  // M50.14: in page mode the completion view (below) replaces the redirect —
  // only the original overlay run fades out and navigates away.
  useEffect(() => {
    if (isComplete && !pageMode) {
      setFading(true);
      const t = setTimeout(async () => {
        await api.reload();
        try {
          routerRef.current.navigate({ to: "/hrm", replace: true });
        } catch {
          window.location.href = "/hrm";
        }
      }, 900);
      return () => clearTimeout(t);
    }
  }, [isComplete, api, pageMode]);

  // Minimal focus trap: keep focus inside the modal while the cover is up.
  // Skipped in page mode — a full page must never trap the whole document.
  useEffect(() => {
    if (isComplete || fading || pageMode) return;
    const el = dialogRef.current;
    if (!el) return;
    const previouslyFocused = document.activeElement as HTMLElement | null;
    el.focus();
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Tab") {
        const focusable = el.querySelectorAll<HTMLElement>(
          "button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])",
        );
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first?.focus();
        }
      }
    };
    document.addEventListener("keydown", handleKey);
    return () => {
      document.removeEventListener("keydown", handleKey);
      previouslyFocused?.focus?.();
    };
  }, [isComplete, fading]);

  // M50.12: a step save must end in a visible outcome — advancing to a next
  // step or, when the saved step completes the last mandatory one, finishing
  // the whole setup. The server is the source of truth, so re-read the fresh
  // state here (api.reload() returns void and doesn't wait) before deciding.
  const completeStep = async (key: string, payload: Record<string, unknown>) => {
    setSending(true);
    setMessage(null);
    try {
      await realApi.completeSetupStep(key, JSON.stringify(payload));
      // Refetch the authoritative setup state (steps + overall status).
      const [freshState, freshSteps] = await Promise.all([realApi.setupState(), realApi.setupSteps()]);
      const done = new Set(freshState?.completedSteps ?? []);
      const refreshedSteps = (freshSteps as StepDto[]).map((s) => ({ ...s, done: done.has(s.key) }));
      const mandatory = refreshedSteps.filter((s) => s.mandatory);
      const allMandatoryDone = mandatory.length > 0 && mandatory.every((s) => s.done);
      // Advance to the next incomplete step when one exists.
      const idx = refreshedSteps.findIndex((s) => s.key === key);
      const next = refreshedSteps.find((s, i) => i > idx && !s.done);
      if (next) {
        setActive(next.key);
      } else if (allMandatoryDone && freshState?.status !== "complete") {
        // The saved step closed the last mandatory gap — finish the setup so
        // the operator never lands on a success toast with nothing after it.
        setMessage({ text: "Last step saved — unlocking the HRM…", kind: "info" });
        await realApi.finishSetup();
        setMessage({ text: "Setup complete — the HRM is now unlocked.", kind: "info" });
      } else {
        // No next step and setup not (yet) complete: refresh the local state
        // so the UI reflects the freshly saved step and any newly available
        // Finish button without a full reload cycle.
        await api.reload();
      }
    } catch (err) {
      setMessage({ text: setupErrorText(err), kind: "error" });
    } finally {
      setSending(false);
    }
  };

  const finishWizard = async () => {
    if (sending) return;
    setSending(true);
    setMessage(null);
    try {
      await realApi.finishSetup();
      await api.reload();
      setMessage({ text: "Setup complete — the HRM is now unlocked.", kind: "info" });
    } catch (err) {
      setMessage({ text: setupErrorText(err), kind: "error" });
    } finally {
      setSending(false);
    }
  };

  if (isComplete && fading) {
    return (
      <div
        aria-hidden="true"
        className="fixed inset-0 z-[120] bg-white/90 backdrop-blur-xl"
        style={{ animation: "scopeOverlayOut 500ms ease-in both" }}
      />
    );
  }
  // M50.13: completion view — once setup is finished, /hrm/setup shows a
  // "You are all set up" card instead of redirecting. It lists every step
  // with its status, offers "Go to home" (dashboard) and "Make changes"
  // (re-open the wizard at the first step). Page-mode only: the original
  // overlay path still fades out and navigates away after a fresh run.
  if (isComplete && pageMode) {
    return (
      <div className="min-h-screen bg-background">
        <div className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-8">
          <Card className="w-full shadow-lg">
            <CardHeader className="items-center text-center">
              <div className="mb-2 flex h-16 w-16 items-center justify-center rounded-full bg-primary/15">
                <CheckCircle2 className="size-9 text-primary" aria-hidden />
              </div>
              <Badge variant="default" className="mb-3 bg-emerald-600 text-white hover:bg-emerald-600">
                You are all set up
              </Badge>
              <CardTitle className="text-2xl">The HRM is ready to use</CardTitle>
              <CardDescription className="max-w-lg">
                Every step of the setup wizard has been completed. Your organisation
                is configured and the workspace is unlocked — head to the dashboard
                to start working, or come back here any time to update information.
              </CardDescription>
            </CardHeader>
            <CardContent className="mx-auto w-full max-w-lg">
              <div className="flex flex-wrap gap-1.5">
                {stepsByCompletion.map((s) => (
                  <span
                    key={s.key}
                    className={`flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${
                      s.done ? "bg-primary/15 text-primary" : "bg-muted text-muted-foreground"
                    }`}
                  >
                    {s.done ? <CheckCircle2 className="size-3" aria-hidden /> : <Sparkles className="size-3" aria-hidden />}
                    {s.label}
                  </span>
                ))}
              </div>
            </CardContent>
            <CardFooter className="flex justify-center gap-3 pb-8">
              <Button size="lg" onClick={() => routerRef.current.navigate({ to: "/hrm" })}>
                Go to home
              </Button>
              <Button
                size="lg"
                variant="outline"
                onClick={() => setActive(stepsByCompletion[0]?.key ?? null)}
              >
                Make changes
              </Button>
            </CardFooter>
          </Card>
        </div>
      </div>
    );
  }
  if (isComplete || !state || !active) return null;

  const current = stepsByCompletion.find((s) => s.key === active);
  const idx = stepsByCompletion.findIndex((s) => s.key === active);
  const canBack = idx > 0;
  const mandatoryAllDone = stepsByCompletion.filter((s) => s.mandatory).every((s) => s.done);

  return (
    <div className={pageMode ? "min-h-screen bg-background" : "fixed inset-0 z-[120] overflow-y-auto bg-white/90 backdrop-blur-xl"}>
      <div className={pageMode ? "mx-auto w-full max-w-6xl px-4 py-8 sm:px-8" : "mx-auto flex min-h-screen w-full max-w-6xl flex-col items-center px-4 py-8 sm:px-8"}>
        {/* Header lock strip — the only thing visible above the wizard card. */}
        <div className="flex w-full items-center gap-4 pb-4">
          <SwitchGlyph />
          <div>
            <h1 className="text-lg font-semibold text-foreground sm:text-xl">Set up your organisation</h1>
            <p className="text-sm text-muted-foreground">
              {pageMode
                ? "Configure your organisation step by step — the HRM unlocks when setup is finished."
                : "The HRM stays locked until first-time setup is finished."}
            </p>
          </div>
          <Button variant="ghost" className="ml-auto" asChild>
            <a href="/hrm">Skip to dashboard</a>
          </Button>
        </div>

        {/* Progress + stepper rail */}
        <Card className="w-full shadow-lg">
          <CardContent className="space-y-3 pt-5">
            <div className="flex items-center gap-3 text-sm">
              <span className="font-semibold">Step {idx + 1} of {stepsByCompletion.length}</span>
              <Badge variant="secondary" className="ml-auto">{state.completionPercent ?? 0}% done</Badge>
            </div>
            <Progress value={state.completionPercent ?? 0} className="h-2" />
            <div className="flex flex-wrap gap-x-2 gap-y-1">
              {stepsByCompletion.map((s, i) => (
                <button
                  key={s.key}
                  type="button"
                  onClick={() => setActive(s.key)}
                  disabled={s.done || sending}
                  className={`flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium transition-colors ${
                    s.key === active
                      ? "bg-primary text-primary-foreground"
                      : s.done
                        ? "bg-primary/15 text-primary"
                        : "bg-muted text-muted-foreground"
                  }`}
                  aria-current={s.key === active ? "step" : undefined}
                >
                  {s.done ? <CheckCircle2 className="size-3" aria-hidden /> : <span aria-hidden>{i + 1}</span>}
                  {s.label}
                </button>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* The active step form — the only interactive surface on screen. */}
        <div
          ref={dialogRef}
          role={pageMode ? undefined : "dialog"}
          aria-modal={pageMode ? undefined : true}
          aria-label={`Setup step: ${current?.label}`}
          tabIndex={-1}
          className="w-full outline-none"
          style={pageMode ? undefined : { animation: "wizardModalIn 260ms ease-out both" }}
        >
          <StepRenderer
            key={active}
            stepKey={active}
            step={current}
            steps={stepsByCompletion}
            sending={sending}
            // M50.12: completeStep() now handles advancing/auto-finishing after
            // refetching fresh state — no post-save toast or advance() here.
            onComplete={async (payload) => {
              try {
                await completeStep(active, payload);
              } catch (err) {
                setMessage({ text: setupErrorText(err), kind: "error" });
              }
            }}
          />

          {message && (
            <div
              className={`mt-4 flex items-center gap-2 rounded-md border px-4 py-3 text-sm ${
                message.kind === "error"
                  ? "border-destructive/40 bg-destructive/10 text-destructive"
                  : "border-primary/30 bg-primary/10 text-foreground"
              }`}
            >
              {message.kind === "error" ? (
                <AlertCircle className="size-4 shrink-0" aria-hidden />
              ) : (
                <CheckCircle2 className="size-4 shrink-0" aria-hidden />
              )}
              {message.text}
            </div>
          )}

          <Card className="mt-4 flex items-center justify-between gap-3 border-dashed">
            <div className="flex items-center gap-2 px-6 py-4">
              <Button variant="outline" onClick={() => { const p = stepsByCompletion[idx - 1]; if (p) setActive(p.key); }} disabled={!canBack || sending}>
                <ArrowLeft className="size-4" aria-hidden /> Back
              </Button>
              {mandatoryAllDone && (
                <Button
                  onClick={finishWizard}
                  disabled={sending}
                  className="ml-2"
                  data-testid="finish-setup"
                >
                  <ShieldCheck className="size-4" aria-hidden /> Finish setup
                </Button>
              )}
            </div>
            {!mandatoryAllDone && (
              <Button
                className="mr-6"
                disabled={sending}
                onClick={document.querySelector<HTMLButtonElement>('[data-submit="now"]')?.click ?? (() => {})}
                style={{ visibility: "hidden", position: "absolute", width: 1, height: 1 }}
              >
                placeholder
              </Button>
            )}
          </Card>
          {mandatoryAllDone && <div />}
        </div>
      </div>
    </div>
  );
}

/* ---------- per-step inline forms ---------- */

function StepRenderer(props: {
  stepKey: string;
  step: StepDto | undefined;
  steps: Array<StepDto & { done: boolean }>;
  sending: boolean;
  onComplete: (payload: Record<string, unknown>) => Promise<void>;
}) {
  const { stepKey, steps, sending, onComplete } = props;
  switch (stepKey) {
    case "organisation":
      return <OrganisationStep sending={sending} onComplete={onComplete} />;
    case "structure":
      return <StructureStep sending={sending} onComplete={onComplete} />;
    case "employment":
      return <EmploymentStep sending={sending} onComplete={onComplete} />;
    case "working-time":
      return <WorkingTimeStep sending={sending} onComplete={onComplete} />;
    case "leave":
      return <LeaveStep sending={sending} onComplete={onComplete} />;
    case "payroll":
      return <PayrollStep sending={sending} onComplete={onComplete} />;
    case "policies":
      return <PoliciesStep sending={sending} onComplete={onComplete} />;
    case "roles":
      return <RolesStep sending={sending} onComplete={onComplete} />;
    case "employees":
      return <EmployeesStep sending={sending} onComplete={onComplete} steps={steps} />;
    default:
      return <UnknownStep stepKey={stepKey} sending={sending} onComplete={onComplete} />;
  }
}

function StepCard(props: { title: string; description: string; children: React.ReactNode }) {
  return (
    <Card className="mt-4 shadow-lg">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{props.title}</CardTitle>
        <CardDescription>{props.description}</CardDescription>
      </CardHeader>
      <CardContent>{props.children}</CardContent>
    </Card>
  );
}

/* Step 1 — Organisation */
function OrganisationStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [name, setName] = useState("");
  const [trading, setTrading] = useState("");
  const [pacra, setPacra] = useState("");
  const [tpin, setTpin] = useState("");
  const [napsa, setNapsa] = useState("");
  const [nhima, setNhima] = useState("");
  const valid = name.trim().length >= 2 && name.trim().length <= 120;
  return (
    <StepCard
      title="Organisation details"
      description="Who are you registering? This becomes the legal entity that owns the whole HRM."
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="org-name">Registered name *</Label>
            <Input id="org-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Mighty Finance Limited" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="org-trading">Trading name</Label>
            <Input id="org-trading" value={trading} onChange={(e) => setTrading(e.target.value)} placeholder="e.g. Mighty Finance" />
          </div>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="org-pacra">PACRA company registration number</Label>
            <Input id="org-pacra" value={pacra} onChange={(e) => setPacra(e.target.value)} placeholder="e.g. 1234567890" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="org-tpin">TPIN (ZRA)</Label>
            <Input id="org-tpin" value={tpin} onChange={(e) => setTpin(e.target.value)} placeholder="e.g. 1000000000" />
          </div>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="org-napsa">NAPSA employer reference</Label>
            <Input id="org-napsa" value={napsa} onChange={(e) => setNapsa(e.target.value)} placeholder="e.g. 10001-1234567-89" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="org-nhima">NHIMA employer reference</Label>
            <Input id="org-nhima" value={nhima} onChange={(e) => setNhima(e.target.value)} placeholder="e.g. 1234567890" />
          </div>
        </div>
        <SubmitRow
          disabled={!valid || props.sending}
          sending={props.sending}
          label="Save organisation"
          onSubmit={() =>
            props.onComplete({
              RegisteredName: name.trim(),
              TradingName: trading.trim() || null,
              PacraNumber: pacra.trim() || null,
              Tpin: tpin.trim() || null,
              NapsaEmployerRef: napsa.trim() || null,
              NhimaEmployerRef: nhima.trim() || null,
              Currency: "ZMW",
            })
          }
        />
      </div>
    </StepCard>
  );
}

/* Step 2 — Structure */
function StructureStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [branches, setBranches] = useState([{ name: "", code: "", address: "", city: "" }]);
  const [departments, setDepartments] = useState([{ name: "" }]);
  const valid = branches.every((b) => b.name.trim().length > 0) && branches.length > 0 && departments.every((d) => d.name.trim().length > 0);

  const updateBranch = (i: number, patch: Partial<(typeof branches)[0]>) =>
    setBranches((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const updateDept = (i: number, name: string) =>
    setDepartments((rows) => rows.map((r, j) => (j === i ? { name } : r)));

  return (
    <StepCard
      title="Branches and departments"
      description="Where does the organisation operate, and how is it divided internally? At least one branch is required."
    >
      <div className="space-y-4">
        <div>
          <Label className="mb-2 block">Branches</Label>
          <div className="space-y-3">
            {branches.map((b, i) => (
              <div key={i} className="grid gap-3 rounded-md border p-3 sm:grid-cols-[1fr_auto]">
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1">
                    <Label>Branch name *</Label>
                    <Input value={b.name} onChange={(e) => updateBranch(i, { name: e.target.value })} placeholder="e.g. Kitwe Branch" />
                  </div>
                  <div className="space-y-1">
                    <Label>City / town</Label>
                    <Input value={b.city} onChange={(e) => updateBranch(i, { city: e.target.value })} placeholder="e.g. Kitwe" />
                  </div>
                  <div className="space-y-1 sm:col-span-2">
                    <Label>Address</Label>
                    <Input value={b.address} onChange={(e) => updateBranch(i, { address: e.target.value })} placeholder="e.g. 14 Freedom Way" />
                  </div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="self-start"
                  disabled={branches.length <= 1}
                  onClick={() => setBranches((rows) => rows.filter((_, j) => j !== i))}
                  aria-label={`Remove branch ${i + 1}`}
                >
                  <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                </Button>
              </div>
            ))}
            <Button type="button" variant="outline" size="sm" onClick={() => setBranches((rows) => [...rows, { name: "", code: "", address: "", city: "" }])}>
              <Plus className="size-4" aria-hidden /> Add branch
            </Button>
          </div>
        </div>
        <div>
          <Label className="mb-2 block">Departments</Label>
          <div className="space-y-2">
            {departments.map((d, i) => (
              <div key={i} className="flex items-center gap-2">
                <Input value={d.name} onChange={(e) => updateDept(i, e.target.value)} placeholder="e.g. Finance" />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={departments.length <= 1}
                  onClick={() => setDepartments((rows) => rows.filter((_, j) => j !== i))}
                  aria-label={`Remove department ${i + 1}`}
                >
                  <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                </Button>
              </div>
            ))}
            <Button type="button" variant="outline" size="sm" onClick={() => setDepartments((rows) => [...rows, { name: "" }])}>
              <Plus className="size-4" aria-hidden /> Add department
            </Button>
          </div>
        </div>
        <SubmitRow
          disabled={!valid || props.sending}
          sending={props.sending}
          label="Save structure"
          onSubmit={() =>
            props.onComplete({
              Branches: branches.map((b) => ({
                Name: b.name.trim(),
                Code: b.code.trim() || slugify(b.name) || null,
                AddressLine: b.address.trim() || null,
                City: b.city.trim() || null,
                Province: null,
                District: null,
                Type: "branch",
              })),
              Departments: departments.map((d) => ({ Name: d.name.trim(), UnitType: "department", ManagerName: null })),
            })
          }
        />
      </div>
    </StepCard>
  );
}

/* Step 3 — Employment */
function EmploymentStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [grades, setGrades] = useState([{ name: "Grade 1" }, { name: "Grade 2" }, { name: "Manager" }]);
  const [positions, setPositions] = useState([{ name: "" }]);
  const valid = grades.every((g) => g.name.trim().length > 0) && positions.every((p) => p.name.trim().length > 0);
  return (
    <StepCard
      title="Grades and positions"
      description="Define the job grades and position titles used later when adding employees — this keeps the dropdowns consistent."
    >
      <div className="space-y-4">
        <div>
          <Label className="mb-2 block">Job grades</Label>
          <div className="space-y-2">
            {grades.map((g, i) => (
              <div key={i} className="flex items-center gap-2">
                <Input
                  value={g.name}
                  onChange={(e) => setGrades((rows) => rows.map((r, j) => (j === i ? { name: e.target.value } : r)))}
                  placeholder="e.g. Grade 1, Officer, Manager"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={grades.length <= 1}
                  onClick={() => setGrades((rows) => rows.filter((_, j) => j !== i))}
                  aria-label={`Remove grade ${i + 1}`}
                >
                  <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                </Button>
              </div>
            ))}
            <Button type="button" variant="outline" size="sm" onClick={() => setGrades((rows) => [...rows, { name: "" }])}>
              <Plus className="size-4" aria-hidden /> Add grade
            </Button>
          </div>
        </div>
        <div>
          <Label className="mb-2 block">Position titles</Label>
          <div className="space-y-2">
            {positions.map((p, i) => (
              <div key={i} className="flex items-center gap-2">
                <Input
                  value={p.name}
                  onChange={(e) => setPositions((rows) => rows.map((r, j) => (j === i ? { name: e.target.value } : r)))}
                  placeholder="e.g. Accountant, HR Officer"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={positions.length <= 1}
                  onClick={() => setPositions((rows) => rows.filter((_, j) => j !== i))}
                  aria-label={`Remove position ${i + 1}`}
                >
                  <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                </Button>
              </div>
            ))}
            <Button type="button" variant="outline" size="sm" onClick={() => setPositions((rows) => [...rows, { name: "" }])}>
              <Plus className="size-4" aria-hidden /> Add position
            </Button>
          </div>
        </div>
        <SubmitRow
          disabled={!valid || props.sending}
          sending={props.sending}
          label="Save grades and positions"
          onSubmit={() =>
            props.onComplete({
              Grades: grades.map((g) => ({ Name: g.name.trim() })),
              Positions: positions.map((p) => ({ Name: p.name.trim(), GradeName: null })),
            })
          }
        />
      </div>
    </StepCard>
  );
}

/* Step 4 — Working time (optional) */
// Zambian public holidays — name + month-day (MM-DD) that repeats every year.
const ZAMBIAN_HOLIDAYS: { name: string; date: string }[] = [
  { name: "New Year's Day", date: "01-01" },
  { name: "Youth Day", date: "03-12" },
  { name: "Women's Day", date: "03-13" },
  { name: "Kenneth Kaunda Day", date: "04-28" },
  { name: "Labour Day", date: "05-01" },
  { name: "Africa Day", date: "05-25" },
  { name: "Heroes' Day", date: "07-01" },
  { name: "Unity Day", date: "07-02" },
  { name: "Farmers' Day", date: "08-06" },
  { name: "National Prayer Day", date: "10-18" },
  { name: "Independence Day", date: "10-24" },
  { name: "Christmas Day", date: "12-25" },
];
function holidayRows(holidays: { name: string; date: string }[]) {
  return holidays.map((h) => ({ Name: h.name, Date: h.date, IsRecurring: true }));
}
function WorkingTimeStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [hours, setHours] = useState("45");
  const [weekends, setWeekends] = useState<string[]>(["sat", "sun"]);
  const [useHolidays, setUseHolidays] = useState(true);
  const [holidays, setHolidays] = useState<{ name: string; date: string }[]>(ZAMBIAN_HOLIDAYS);
  const DAY_KEYS = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"] as const;
  const toggle = (d: string) =>
    setWeekends((ws) => (ws.includes(d) ? ws.filter((x) => x !== d) : [...ws, d]));
  const buildPayload = (hrs: number, days: string, holidayEnabled: boolean) => ({
    StandardWeeklyHours: hrs,
    WeekendDays: days,
    PublicHolidays: holidayEnabled ? holidayRows(holidays) : [],
  });
  return (
    <StepCard
      title="Standard working time"
      description="This is optional — you can skip it for now with “I'll do this later” and adjust it under Configuration anytime."
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="wt-hours">Standard weekly hours</Label>
            <Input id="wt-hours" type="number" min={1} max={80} value={hours} onChange={(e) => setHours(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Weekend days</Label>
            <div className="flex flex-wrap gap-2 pt-1">
              {DAY_KEYS.map((d) => (
                <button
                  key={d}
                  type="button"
                  onClick={() => toggle(d)}
                  className={`rounded-md border px-2.5 py-1 text-xs font-medium uppercase ${
                    weekends.includes(d) ? "border-primary bg-primary/10 text-primary" : "text-muted-foreground"
                  }`}
                  aria-pressed={weekends.includes(d)}
                >
                  {d}
                </button>
              ))}
            </div>
          </div>
        </div>
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <input
              id="wt-holidays"
              type="checkbox"
              checked={useHolidays}
              onChange={(e) => setUseHolidays(e.target.checked)}
              className="size-4 accent-primary"
            />
            <Label htmlFor="wt-holidays" className="cursor-pointer">
              Use Zambia's public holidays ({holidays.length} days — repeats every year)
            </Label>
          </div>
          {useHolidays && (
            <div className="space-y-1.5">
              {holidays.map((h, i) => (
                <div key={i} className="flex items-center gap-2">
                  <Input
                    aria-label="Holiday name"
                    value={h.name}
                    onChange={(e) => setHolidays((xs) => xs.map((r, j) => (j === i ? { ...r, name: e.target.value } : r)))}
                    className="h-9"
                  />
                  <Input
                    aria-label="Date MM-DD"
                    type="text"
                    maxLength={5}
                    placeholder="MM-DD"
                    value={h.date}
                    onChange={(e) => {
                      let v = e.target.value.replace(/[^0-9-]/g, "");
                      setHolidays((xs) => xs.map((r, j) => (j === i ? { ...r, date: v } : r)));
                    }}
                    className="h-9 w-24"
                  />
                  <button
                    type="button"
                    onClick={() => setHolidays((xs) => xs.filter((_, j) => j !== i))}
                    disabled={holidays.length <= 1}
                    className="rounded-md border px-2 py-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive disabled:opacity-30"
                    aria-label={`Remove ${h.name}`}
                  >
                    <X className="size-3.5" aria-hidden />
                  </button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setHolidays((xs) => [...xs, { name: "", date: "" }])}
              >
                <Plus className="size-4" aria-hidden /> Add holiday
              </Button>
            </div>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <SubmitRow
            disabled={props.sending}
            sending={props.sending}
            label="Save working time"
            onSubmit={() =>
              props.onComplete(
                buildPayload(Math.max(1, Math.min(80, Number(hours) || 45)), (weekends.length ? weekends : ["sat", "sun"]).join(","), useHolidays),
              )
            }
          />
          <SkipRow
            sending={props.sending}
            onSkip={() => props.onComplete(buildPayload(45, "sat,sun", false))}
          />
        </div>
      </div>
    </StepCard>
  );
}

/* Step 5 — Leave */
function LeaveStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [types, setTypes] = useState(
    LEAVE_DEFAULTS.map((d) => ({ name: d.name, category: d.category, days: d.days, evidence: d.evidence, carry: d.carry })),
  );
  const valid = types.every((t) => t.name.trim().length > 0 && t.days >= 0);
  const update = (i: number, patch: Partial<(typeof types)[0]>) =>
    setTypes((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  return (
    <StepCard
      title="Leave types"
      description="Pre-filled with the common Zambian entitlements — adjust names or days to match your policy. Each type becomes a real leave type in the system."
    >
      <div className="space-y-3">
        {types.map((t, i) => (
          <div key={i} className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_140px_100px_110px_100px_auto] md:items-end">
            <div className="space-y-1">
              <Label>Name *</Label>
              <Input value={t.name} onChange={(e) => update(i, { name: e.target.value })} />
            </div>
            <div className="space-y-1">
              <Label>Category</Label>
              <Select value={t.category} onValueChange={(v) => update(i, { category: v })}>
                <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
                <SelectContent position="popper" side="bottom" collisionPadding={8}>
                  <SelectItem value="paid">Paid</SelectItem>
                  <SelectItem value="unpaid">Unpaid</SelectItem>
                  <SelectItem value="half-pay">Half pay</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Days / year</Label>
              <Input type="number" min={0} value={t.days} onChange={(e) => update(i, { days: Math.max(0, Number(e.target.value) || 0) })} />
            </div>
            <div className="flex items-center gap-1.5 pt-6">
              <Checkbox id={`ev-${i}`} checked={t.evidence} onCheckedChange={(v) => update(i, { evidence: v === true })} />
              <Label htmlFor={`ev-${i}`} className="text-xs">Needs evidence</Label>
            </div>
            <div className="space-y-1">
              <Label>Carry forward</Label>
              <Input type="number" min={0} value={t.carry} onChange={(e) => update(i, { carry: Math.max(0, Number(e.target.value) || 0) })} />
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              disabled={types.length <= 1}
              onClick={() => setTypes((rows) => rows.filter((_, j) => j !== i))}
              aria-label={`Remove leave type ${i + 1}`}
            >
              <Trash2 className="size-4 text-muted-foreground" aria-hidden />
            </Button>
          </div>
        ))}
        <Button type="button" variant="outline" size="sm" onClick={() => setTypes((rows) => [...rows, { name: "", category: "paid", days: 0, evidence: false, carry: 0 }])}>
          <Plus className="size-4" aria-hidden /> Add leave type
        </Button>
        <SubmitRow
          disabled={!valid || props.sending}
          sending={props.sending}
          label="Save leave types"
          onSubmit={() =>
            props.onComplete({
              LeaveTypes: types.map((t) => ({
                Name: t.name.trim(),
                Code: slugify(t.name) || null,
                Category: t.category,
                DaysPerYear: Math.round(t.days),
                RequiresEvidence: t.evidence,
                CarryForwardDays: Math.round(t.carry),
              })),
            })
          }
        />
      </div>
    </StepCard>
  );
}

/* Step 6 — Payroll */
function PayrollStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [frequency, setFrequency] = useState("monthly");
  const [payday, setPayday] = useState("25");
  const [basis, setBasis] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const valid = confirm;
  return (
    <StepCard
      title="Payroll configuration"
      description="How often does your organisation pay, and on what basis? The system provisions the full Zambian statutory engine — review the rates, then confirm."
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="py-freq">Pay frequency</Label>
            <Select value={frequency} onValueChange={setFrequency}>
              <SelectTrigger id="py-freq" className="w-full"><SelectValue /></SelectTrigger>
              <SelectContent position="popper" side="bottom" collisionPadding={8}>
                <SelectItem value="monthly">Monthly</SelectItem>
                <SelectItem value="semimonthly">Semi-monthly</SelectItem>
                <SelectItem value="biweekly">Bi-weekly</SelectItem>
                <SelectItem value="weekly">Weekly</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="py-day">Payday (day of month)</Label>
            <Input id="py-day" type="number" min={1} max={31} value={payday} onChange={(e) => setPayday(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="py-cur">Currency</Label>
            <Select value="ZMW" onValueChange={() => {}} disabled>
              <SelectTrigger id="py-cur" className="w-full"><SelectValue /></SelectTrigger>
              <SelectContent position="popper" side="bottom" collisionPadding={8}><SelectItem value="ZMW">ZMW (Kwacha)</SelectItem></SelectContent>
            </Select>
          </div>
        </div>
        <div className="flex items-center gap-2 rounded-md border p-3">
          <Checkbox id="py-basis" checked={basis} onCheckedChange={(v) => setBasis(v === true)} />
          <Label htmlFor="py-basis">Payroll is timesheet-based</Label>
          <span className="ml-auto text-xs text-muted-foreground">
            Off = fixed salaries (most organisations)
          </span>
        </div>
        <div className="rounded-md border bg-muted/40 p-4">
          <div className="mb-2 flex items-center gap-2 font-medium">
            <ShieldCheck className="size-4 text-primary" aria-hidden />
            Statutory rates being provisioned
          </div>
          <div className="space-y-1 text-sm">
            <p><strong>ZRA PAYE (2026 monthly slabs):</strong> {PAYE_BANDS.map((b, i) => `${b.band} → ${b.rate}${i < PAYE_BANDS.length - 1 ? "; " : ""}`).join("")}</p>
            <p><strong>NAPSA:</strong> 5% employee + 5% employer on basic salary, ceiling ZMW 1,221.80 contribution</p>
            <p><strong>NHIMA:</strong> 1% employee (minimum ZMW 50) + 1% employer</p>
          </div>
          <div className="mt-3 flex items-start gap-2">
            <Checkbox id="py-confirm" checked={confirm} onCheckedChange={(v) => setConfirm(v === true)} />
            <Label htmlFor="py-confirm" className="text-sm leading-snug">
              I confirm these statutory rates are correct for my organisation and may be updated later under
              Configuration → Payroll.
            </Label>
          </div>
        </div>
        <SubmitRow
          disabled={!valid || props.sending}
          sending={props.sending}
          label="Provision payroll engine"
          onSubmit={() =>
            props.onComplete({
              Frequency: frequency,
              PaydayDay: Math.max(1, Math.min(31, Number(payday) || 25)),
              Currency: "ZMW",
              PayBasisTimesheet: basis,
              ConfirmStatutory: confirm,
              BasicDefaultAmount: null,
            })
          }
        />
      </div>
    </StepCard>
  );
}

/* Step 7 — Policies (optional) */
function PoliciesStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [types, setTypes] = useState(CONTRACT_DEFAULTS.map((c) => ({ ...c })));
  const valid = types.every((t) => t.name.trim().length > 0 && t.probation >= 0 && t.notice >= 0);
  const update = (i: number, patch: Partial<(typeof types)[0]>) =>
    setTypes((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  return (
    <StepCard
      title="Contract types"
      description="This is optional — the defaults cover the common cases and can be adjusted later under Configuration."
    >
      <div className="space-y-3">
        {types.map((t, i) => (
          <div key={i} className="grid gap-3 rounded-md border p-3 sm:grid-cols-[1fr_130px_130px_auto] sm:items-end">
            <div className="space-y-1">
              <Label>Contract type *</Label>
              <Input value={t.name} onChange={(e) => update(i, { name: e.target.value })} />
            </div>
            <div className="space-y-1">
              <Label>Probation (days)</Label>
              <Input type="number" min={0} value={t.probation} onChange={(e) => update(i, { probation: Math.max(0, Number(e.target.value) || 0) })} />
            </div>
            <div className="space-y-1">
              <Label>Notice (days)</Label>
              <Input type="number" min={0} value={t.notice} onChange={(e) => update(i, { notice: Math.max(0, Number(e.target.value) || 0) })} />
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              disabled={types.length <= 1}
              onClick={() => setTypes((rows) => rows.filter((_, j) => j !== i))}
              aria-label={`Remove contract type ${i + 1}`}
            >
              <Trash2 className="size-4 text-muted-foreground" aria-hidden />
            </Button>
          </div>
        ))}
        <Button type="button" variant="outline" size="sm" onClick={() => setTypes((rows) => [...rows, { name: "", probation: 0, notice: 0 }])}>
          <Plus className="size-4" aria-hidden /> Add contract type
        </Button>
        <div className="flex flex-wrap gap-2">
          <SubmitRow
            disabled={!valid || props.sending}
            sending={props.sending}
            label="Save contract types"
            onSubmit={() =>
              props.onComplete({
                ContractTypes: types.map((t) => ({ Name: t.name.trim(), ProbationDays: Math.round(t.probation), NoticeDays: Math.round(t.notice) })),
              })
            }
          />
          <SkipRow
            sending={props.sending}
            onSkip={() => props.onComplete({ ContractTypes: CONTRACT_DEFAULTS.map((c) => ({ ...c })) })}
          />
        </div>
      </div>
    </StepCard>
  );
}

/* Step 8 — Roles */
function RolesStep(props: { sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  const [input, setInput] = useState("");
  const [emails, setEmails] = useState<string[]>([]);
  const addEmail = () => {
    const e = input.trim().toLowerCase();
    if (!e || emails.includes(e) || !isValidEmail(e)) return;
    setEmails((xs) => [...xs, e]);
    setInput("");
  };
  return (
    <StepCard
      title="HR administrators"
      description="Invite the people who will run this HRM. Their accounts are provisioned in the identity system — enter each email and press Add."
    >
      <div className="space-y-4">
        <div className="flex gap-2">
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="hr.manager@mightyfinance.co.zm"
            onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), addEmail())}
          />
          <Button type="button" variant="outline" onClick={addEmail}>
            <Plus className="size-4" aria-hidden /> Add
          </Button>
        </div>
        <div className="flex min-h-10 flex-wrap gap-2">
          {emails.length === 0 && (
            <span className="text-sm text-muted-foreground">No administrators added yet — you can add them anytime.</span>
          )}
          {emails.map((e, i) => (
            <Badge key={e} variant="secondary" className="gap-1 px-2.5 py-1.5">
              {e}
              <button type="button" onClick={() => setEmails((xs) => xs.filter((_, j) => j !== i))} aria-label={`Remove ${e}`}>
                <X className="size-3 text-muted-foreground" aria-hidden />
              </button>
            </Badge>
          ))}
        </div>
        <SubmitRow
          disabled={props.sending}
          sending={props.sending}
          label={emails.length ? "Save administrators" : "Skip — I'll add administrators later"}
          onSubmit={() => props.onComplete({ AdminEmails: emails })}
        />
      </div>
    </StepCard>
  );
}

/* Step 9 — Employees */
function EmployeesStep(props: {
  sending: boolean;
  onComplete: (p: Record<string, unknown>) => Promise<void>;
  steps: Array<StepDto & { done: boolean }>;
}) {
  const [mode, setMode] = useState<"upload" | "manual">("upload");
  const [rows, setRows] = useState<Row[]>([EMPTY_ROW]);
  const [sheet, setSheet] = useState<ParsedSheet | null>(null);
  const [mapping, setMapping] = useState<Mapping>({});
  const [pasteError, setPasteError] = useState<string | null>(null);
  // Switches entry mode while keeping the spreadsheet data — the manual grid
  // is seeded from the imported rows so nothing typed or pasted is lost.
  const switchMode = (next: "upload" | "manual") => {
    setMode(next);
    if (next === "manual" && sheet) {
      const canonical = buildCanonical(sheet.rows, mapping);
      const seeded: Row[] = canonical.slice(0, 20).map((r) => ({
        first: r[0] ?? "",
        last: r[1] ?? "",
        email: r[2] ?? "",
        phone: r[3] ?? "",
        jobTitle: r[4] ?? "",
        grade: r[5] ?? "",
        startDate: r[6] ?? "",
        orgUnitName: r[7] ?? "",
      }));
      setRows(seeded.length > 0 ? seeded : [EMPTY_ROW]);
    }
  };
  // Clears the imported spreadsheet so the operator can upload/paste again.
  const clearSheet = () => {
    setSheet(null);
    setMapping({});
    setPasteError(null);
    if (fileInput.current) fileInput.current.value = "";
  };

  // Departments already created in step 2 (backend returns { items }).
  const fileInput = useRef<HTMLInputElement>(null);
  const units = useApi(async () => {
    const res = await realApi.orgUnits();
    return (Array.isArray(res) ? res : (res as { items?: unknown[] })?.items ?? []).map((u) => String((u as { name?: unknown }).name ?? ""));
  }, []);

  const update = (i: number, patch: Partial<Row>) =>
    setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  // Parses whatever spreadsheet shape the operator pasted/uploads. We never
  // assume the headers — every system field is mapped to a real column by the
  // operator (required fields start unmapped on purpose).
  const parseText = (text: string) => {
    setPasteError(null);
    const lines = text.trim().split(/\r?\n/).filter(Boolean);
    if (lines.length < 2) {
      setPasteError("The spreadsheet must have a header row plus at least one data row.");
      setSheet(null);
      return;
    }
    const headers = lines[0].split(",").map((h) => h.trim().replace(/^"|"$/g, ""));
    const dataRows = lines.slice(1).map((l) => l.split(",").map((c) => c.trim().replace(/^"|"$/g, "")));
    if (dataRows.every((r) => r.every((c) => !c))) {
      setPasteError("No readable data rows found below the header.");
      setSheet(null);
      return;
    }
    setSheet({ headers, rows: dataRows });
    setMapping(defaultMapping(headers));
  };

  const onFile = (f: File) => {
    setPasteError(null);
    const reader = new FileReader();
    reader.onload = () => parseText(String(reader.result ?? ""));
    reader.readAsText(f);
  };

  // Canonical projection: what the backend will receive once Import is pressed.
  const canonical = sheet ? buildCanonical(sheet.rows, mapping) : null;
  const requiredMapped =
    SYSTEM_FIELDS.filter((f) => f.required).every((f) => {
      const v = mapping[f.key] ?? -1;
      return typeof v === "number" && v >= 0;
    });
  const valid =
    (mode === "manual"
      ? rows.every((r) => r.first.trim().length > 0 && r.last.trim().length > 0 && (!r.email || isValidEmail(r.email)))
      : !!canonical &&
        canonical.length > 0 &&
        requiredMapped &&
        canonical.every((r) => r[0].length > 0 && r[1].length > 0) &&
        canonical.every((r) => !r[2] || isValidEmail(r[2])));
  const complete = () => {
    if (mode === "manual") {
      return props.onComplete({
        Employees: rows
          .filter((r) => r.first.trim() || r.last.trim())
          .map((r) => ({
            FirstName: r.first.trim(),
            LastName: r.last.trim(),
            Email: r.email?.trim() || null,
            Phone: r.phone?.trim() || null,
            JobTitle: r.jobTitle?.trim() || null,
            Grade: r.grade?.trim() || null,
            StartDate: r.startDate?.trim() || null,
            OrgUnitName: r.orgUnitName?.trim() || null,
            WorkerType: null,
          })),
      });
    }
    const fields = SYSTEM_FIELDS.map((f) => f.key);
    return props.onComplete({
      Employees: canonical!.map((r) => ({
        FirstName: r[fields.indexOf("FirstName")],
        LastName: r[fields.indexOf("LastName")],
        Email: r[fields.indexOf("Email")] || null,
        Phone: r[fields.indexOf("Phone")] || null,
        JobTitle: r[fields.indexOf("JobTitle")] || null,
        Grade: r[fields.indexOf("Grade")] || null,
        StartDate: r[fields.indexOf("StartDate")] || null,
        OrgUnitName: r[fields.indexOf("OrgUnitName")] || null,
        WorkerType: null,
      })),
    });
  };

  return (
    <StepCard
      title="Import employees"
      description="Choose your staff spreadsheet (CSV) — the wizard shows its real column titles, and you map each one to a system field below. Anything unmapped is simply ignored. First name and last name are the only required fields."
    >
      <div className="space-y-4">
        <div className="flex gap-2 text-sm">
          <button
            type="button"
            className={`rounded-md px-3 py-1.5 ${mode === "upload" ? "bg-primary text-primary-foreground" : "bg-muted"}`}
            onClick={() => switchMode("upload")}
          >
            Upload spreadsheet
          </button>
          <button
            type="button"
            className={`rounded-md px-3 py-1.5 ${mode === "manual" ? "bg-primary text-primary-foreground" : "bg-muted"}`}
            onClick={() => switchMode("manual")}
          >
            Enter manually
          </button>
        </div>
        {mode === "upload" ? (
          <div className="space-y-3">
            <input ref={fileInput} type="file" accept=".csv,.txt" className="hidden" onChange={(e) => e.target.files?.[0] && onFile(e.target.files[0])} />
            <Button type="button" variant="outline" className="w-full" onClick={() => fileInput.current?.click()}>
              <Upload className="size-4" aria-hidden /> Choose CSV / text export from your spreadsheet
            </Button>
            <p className="text-xs text-muted-foreground">
              Or paste the spreadsheet contents below — any shape and any column titles. After loading, you
              map each system field to your column, or leave it unmapped to ignore it.
            </p>
            <textarea
              rows={6}
              className="w-full rounded-md border bg-background px-3 py-2 font-mono text-xs"
              placeholder={"<your real headers>,<and data rows below>\nSurname,Jane,Mwansa,jane@co.zm,0970000000,Accountant,Grade 2,2026-01-15,Finance"}
              onChange={(e) => parseText(e.target.value)}
              aria-label="Paste employee spreadsheet"
            />
            {pasteError && <p className="text-xs text-destructive">{pasteError}</p>}

            {sheet && (
              <div className="space-y-3 rounded-md border p-4">
                <div>
                  <div className="mb-2 flex items-center justify-between">
                    <span className="text-xs font-medium text-muted-foreground">
                      Columns found in your file ({sheet.headers.length})
                    </span>
                    <button
                      type="button"
                      className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                      onClick={clearSheet}
                      aria-label="Remove spreadsheet and upload again"
                    >
                      <X className="size-4" aria-hidden />
                    </button>
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {sheet.headers.map((h, i) => (
                      <Badge key={i} variant="outline" className="px-2 py-1 font-mono text-[11px]">
                        {h || `Column ${i + 1}`}
                      </Badge>
                    ))}
                  </div>
                </div>
                <div>
                  <div className="mb-2 text-xs font-medium text-muted-foreground">
                    Map each system field to a spreadsheet column — unmapped fields are ignored.
                    For Department you can also pick one of the departments created in the wizard
                    (every row will then be placed in that department, whatever your columns say).
                  </div>
                  <div className="space-y-2">
                    {SYSTEM_FIELDS.map((f) => (
                      <div key={f.key} className="grid gap-2 sm:grid-cols-[160px_1fr] sm:items-center">
                        <Label className="text-sm">
                          {f.label}
                          {f.required && <span className="ml-0.5 text-destructive">*</span>}
                        </Label>
                        {f.key === "OrgUnitName" ? (
                          <UnitMappingControl
                            units={units.data ?? []}
                            value={mapping[f.key] ?? -1}
                            onChange={(v) => setMapping((m) => ({ ...m, [f.key]: v }))}
                            headers={sheet.headers}
                          />
                        ) : (
                          <Select
                            value={String(mapping[f.key] ?? -1)}
                            onValueChange={(v) => setMapping((m) => ({ ...m, [f.key]: Number(v) }))}
                          >
                            <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
                            <SelectContent position="popper" side="bottom" collisionPadding={8} avoidCollisions={false}>
                              <SelectItem value="-1">— Ignore this field —</SelectItem>
                              {sheet.headers.map((h, i) => (
                                <SelectItem key={i} value={String(i)}>{h || `Column ${i + 1}`}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
                {canonical && canonical.length > 0 && (
                  <>
                    <div className="text-xs font-medium text-muted-foreground">
                      Preview — first 5 rows as the system will receive them ({canonical.length} total)
                    </div>
                    <div className="overflow-x-auto rounded-md border">
                      <table className="w-full text-xs">
                        <thead className="bg-muted/50">
                          <tr>
                            {SYSTEM_FIELDS.map((f) => (
                              <th key={f.key} className="px-2 py-1.5 text-left font-medium whitespace-nowrap">
                                {f.label}{f.required && <span className="ml-0.5 text-destructive">*</span>}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {canonical.slice(0, 5).map((r, i) => (
                            <tr key={i} className="border-t">
                              {r.map((c, j) => <td key={j} className="max-w-40 truncate px-2 py-1">{c}</td>)}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}
              </div>
            )}
          </div>
        ) : (
          <div className="space-y-2">
            {rows.map((r, i) => (
              <div key={i} className="grid gap-2 rounded-md border p-2 md:grid-cols-[1fr_1fr_1fr_auto]">
                <Input value={r.first} onChange={(e) => update(i, { first: e.target.value })} placeholder="First name *" />
                <Input value={r.last} onChange={(e) => update(i, { last: e.target.value })} placeholder="Last name *" />
                <Input value={r.email} onChange={(e) => update(i, { email: e.target.value })} placeholder="Email" />
                <Button type="button" variant="ghost" size="icon" disabled={rows.length <= 1} onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))} aria-label={`Remove row ${i + 1}`}>
                  <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                </Button>
                <Input value={r.phone} onChange={(e) => update(i, { phone: e.target.value })} placeholder="Phone" />
                <Input value={r.jobTitle} onChange={(e) => update(i, { jobTitle: e.target.value })} placeholder="Job title" />
                <Input value={r.grade} onChange={(e) => update(i, { grade: e.target.value })} placeholder="Grade" />
                <Input value={r.startDate} onChange={(e) => update(i, { startDate: e.target.value })} placeholder="Start date YYYY-MM-DD" />
                <div className="md:col-span-2">
                  <Select value={r.orgUnitName || undefined} onValueChange={(v) => update(i, { orgUnitName: v })}>
                    <SelectTrigger className="w-full"><SelectValue placeholder="Department (optional)" /></SelectTrigger>
                    <SelectContent position="popper" side="bottom" collisionPadding={8} avoidCollisions={false}>
                      {units.data?.filter(Boolean).map((u) => <SelectItem key={u} value={u}>{u}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            ))}
            <Button type="button" variant="outline" size="sm" onClick={() => setRows((rs) => [...rs, EMPTY_ROW])}>
              <Plus className="size-4" aria-hidden /> Add employee
            </Button>
          </div>
        )}
        <div className="rounded-md bg-primary/5 p-3 text-xs text-muted-foreground">
          Every imported employee automatically receives a payroll profile against the default pay group and
          salary structure — so the first pay run has something to calculate.
        </div>
        <SubmitRow disabled={!valid || props.sending} sending={props.sending} label="Import employees" onSubmit={complete} />
      </div>
    </StepCard>
  );
}

/* Fallback for steps without inline forms */
function UnknownStep(props: { stepKey: string; sending: boolean; onComplete: (p: Record<string, unknown>) => Promise<void> }) {
  return (
    <StepCard title="Step" description="This step completes the setup checklist.">
      <SubmitRow disabled={props.sending} sending={props.sending} label="Mark complete" onSubmit={() => props.onComplete({})} />
    </StepCard>
  );
}

/* Department mapping with a choice of the REAL units created in the wizard.
   Every row lands in that department regardless of what the spreadsheet
   columns say — so the import can never reference a unit that doesn't exist. */
function UnitMappingControl(props: {
  units: string[];
  value: number | string;
  onChange: (v: number | string) => void;
  headers: string[];
}) {
  const { units, value, onChange, headers } = props;
  const asNumber = typeof value === "number" ? value : -1;
  const asUnit = typeof value === "string" && value.startsWith(UNIT_PREFIX) ? value.slice(UNIT_PREFIX.length) : null;
  return (
    <div className="space-y-1.5">
      <Select value={String(asNumber)} onValueChange={(v) => onChange(Number(v))}>
        <SelectTrigger className="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent position="popper" side="bottom" collisionPadding={8} avoidCollisions={false}>
          <SelectItem value="-1">— Ignore this field —</SelectItem>
          {headers.map((h, i) => (
            <SelectItem key={i} value={String(i)}>{h || `Column ${i + 1}`}</SelectItem>
          ))}
          {units.filter(Boolean).length > 0 && (
            <SelectGroup>
              <SelectLabel>Existing departments from this setup</SelectLabel>
              {units.filter(Boolean).map((u) => (
                <SelectItem key={UNIT_PREFIX + u} value={UNIT_PREFIX + u}>{u}</SelectItem>
              ))}
            </SelectGroup>
          )}
        </SelectContent>
      </Select>
      {asUnit && (
        <p className="text-[11px] text-muted-foreground">
          Every row will be placed in <span className="font-medium">{asUnit}</span> — it can still be
          changed per employee later on the employees page.
        </p>
      )}
    </div>
  );
}

/* ---------- small shared pieces ---------- */

function SubmitRow(props: { disabled: boolean; sending: boolean; label: string; onSubmit: () => void }) {
  return (
    <Button
      data-submit="now"
      className="w-full sm:w-auto"
      disabled={props.disabled}
      onClick={() => {
        if (!props.disabled) props.onSubmit();
      }}
    >
      {props.sending ? "Saving…" : props.label}
      <ChevronRight className="size-4" aria-hidden />
    </Button>
  );
}

function SkipRow(props: { sending: boolean; onSkip: () => void }) {
  return (
    <Button variant="outline" disabled={props.sending} onClick={props.onSkip}>
      I'll do this later
    </Button>
  );
}
