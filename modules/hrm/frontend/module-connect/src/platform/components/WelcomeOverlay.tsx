import { useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { realApi, useApi } from "@/platform/use-api";
import { AlertCircle, CheckCircle2, ChevronRight, Sparkles } from "lucide-react";

/**
 * M49: first-time setup modal gate. While the organisation's setup state is
 * "pending", the ENTIRE app is covered by an intense-but-slightly-transparent
 * white blur and this modal is the only thing on screen — the background is
 * inaccessible (role="dialog", focus trap, Escape-freeze, no navigation out
 * except "Skip to dashboard" which itself returns here while pending).
 *
 * The wizard lives INSIDE the cover: progress bar, per-step cards with
 * "Open in HRM" links (which temporarily lift the cover so the operator can
 * do the actual work) and "Mark complete" confirmations. The backend owns the
 * state machine — GET /hrm/setup/state returns "pending" only while the
 * wizard is unfinished, and "complete" lifts the cover for good. Confined
 * branch HR never see this cover (the backend refuses with setup-confined
 * and the shell filters them out).
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

const STEP_PAGES: Record<string, string> = {
  organisation: "/hrm/configuration",
  structure: "/hrm/configuration",
  employment: "/hrm/configuration",
  "working-time": "/hrm/configuration",
  leave: "/hrm/configuration/leave",
  payroll: "/hrm/payroll",
  policies: "/hrm/configuration/process",
  roles: "/hrm/configuration/branch-access",
  employees: "/hrm/employees",
};

function setupErrorText(err: unknown): string {
  const raw = err as { message?: string };
  if (raw?.message) {
    const m = raw.message.toLowerCase();
    if (m.includes("setup-confined"))
      return "Only organisation-wide HR can run the setup wizard — branch-confined HR cannot.";
    if (m.includes("finish"))
      return "Complete the required steps first, then press Finish setup.";
    return raw.message;
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

/**
 * "Open in HRM" links open the real admin page in a NEW tab so the modal
 * stays up and keeps the app locked until the operator confirms the step.
 */
function openInNewTab(path: string) {
  window.open(path, "_blank", "noopener,noreferrer");
}

export function WelcomeOverlay() {
  const api = useApi(async () => {
    const [state, steps] = await Promise.all([realApi.setupState(), realApi.setupSteps()]);
    return { state, steps: steps as StepDto[] };
  }, []);

  const [completing, setCompleting] = useState<string | null>(null);
  const [doneIds, setDoneIds] = useState<string[]>([]);
  const [message, setMessage] = useState<{ text: string; kind: "error" | "info" } | null>(null);
  const [fading, setFading] = useState(false);
  const dialogRef = useRef<HTMLDivElement>(null);

  const state = api.data?.state;
  const steps = api.data?.steps ?? [];
  const isComplete = state?.status === "complete";

  // Lift the cover with a short fade-out as soon as the backend confirms
  // setup completion. The shell then stops rendering this overlay.
  useEffect(() => {
    if (isComplete) {
      setFading(true);
      const t = setTimeout(() => api.reload(), 600);
      return () => clearTimeout(t);
    }
  }, [isComplete, api]);

  // Minimal focus trap: keep focus inside the modal while the cover is up.
  useEffect(() => {
    if (isComplete || fading) return;
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

  const markComplete = async (key: string) => {
    if (completing) return;
    setCompleting(key);
    setMessage(null);
    try {
      await realApi.completeSetupStep(key);
      setDoneIds((ids) => [...ids, key]);
      await api.reload();
      setMessage({ text: `${key} marked complete`, kind: "info" });
    } catch (err) {
      setMessage({ text: setupErrorText(err), kind: "error" });
    } finally {
      setCompleting(null);
    }
  };

  const finishWizard = async () => {
    if (completing) return;
    setCompleting("finish");
    setMessage(null);
    try {
      await realApi.finishSetup();
      await api.reload();
      setMessage({ text: "Setup complete — the HRM is now unlocked.", kind: "info" });
    } catch (err) {
      setMessage({ text: setupErrorText(err), kind: "error" });
    } finally {
      setCompleting(null);
    }
  };

  if (isComplete && fading) {
    // Cover sliding away — the background will be live again in a beat.
    return (
      <div
        aria-hidden="true"
        className="fixed inset-0 z-[120] bg-white/90 backdrop-blur-xl"
        style={{ animation: "scopeOverlayOut 500ms ease-in both" }}
      />
    );
  }
  if (isComplete || !state) return null;

  return (
    <div
      // The cover: intense frosted white + strong blur. Everything behind it
      // is visually present but unreachable — inert on the root content is
      // handled by rendering this overlay above the whole shell at z-[120].
      className="fixed inset-0 z-[120] overflow-y-auto bg-white/90 backdrop-blur-xl"
    >
      <div className="mx-auto flex min-h-screen w-full max-w-3xl flex-col items-center px-4 py-10">
        {/* Header lock strip — the only thing visible above the modal card. */}
        <div className="flex w-full items-center gap-4 pb-6">
          <SwitchGlyph />
          <div>
            <h1 className="text-lg font-semibold text-foreground sm:text-xl">
              Your organisation is being set up
            </h1>
            <p className="text-sm text-muted-foreground">
              The HRM stays locked until first-time setup is finished.
            </p>
          </div>
          <Button variant="ghost" className="ml-auto" asChild>
            <a href="/hrm">Skip to dashboard</a>
          </Button>
        </div>

        {/* The modal itself — the only interactive surface on screen. */}
        <div
          ref={dialogRef}
          role="dialog"
          aria-modal="true"
          aria-label="First-time setup"
          tabIndex={-1}
          className="w-full space-y-5 outline-none"
          style={{ animation: "wizardModalIn 260ms ease-out both" }}
        >
          <Card className="shadow-lg">
            <CardHeader>
              <div className="flex items-center gap-3">
                <Sparkles className="size-6 text-primary" aria-hidden />
                <CardTitle>Your progress</CardTitle>
                <Badge variant="secondary" className="ml-auto">
                  {state.completionPercent ?? 0}% done
                </Badge>
              </div>
              <CardDescription>
                Work through each step at your own pace. Open the page in a new tab,
                set it up in the HRM, then mark it complete here. Required steps must
                finish before payroll can be used; the optional ones can be revisited
                anytime under Configuration.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Progress value={state.completionPercent ?? 0} className="h-2" />
            </CardContent>
            <CardFooter className="justify-end border-t bg-muted/40 px-6 py-3">
              <Button onClick={finishWizard} disabled={completing === "finish"}>
                {completing === "finish" ? "Finishing…" : "Finish setup"}
              </Button>
            </CardFooter>
          </Card>

          {message && (
            <div
              className={`flex items-center gap-2 rounded-md border px-4 py-3 text-sm ${
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

          <ol className="space-y-3">
            {steps.map((s, idx) => {
              const page = STEP_PAGES[s.key];
              const finished = s.completed || doneIds.includes(s.key);
              return (
                <li key={s.key}>
                  <Card className={finished ? "opacity-80" : undefined}>
                    <CardHeader className="pb-2">
                      <div className="flex items-start gap-3">
                        <span
                          className={`mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${
                            finished
                              ? "bg-primary text-primary-foreground"
                              : "bg-muted text-muted-foreground"
                          }`}
                          aria-hidden
                        >
                          {finished ? "✓" : idx + 1}
                        </span>
                        <div className="min-w-0 flex-1">
                          <CardTitle className="flex items-center gap-2 text-base">
                            {s.label}
                            {s.mandatory ? (
                              <Badge variant="outline" className="text-xs font-normal">Required</Badge>
                            ) : (
                              <Badge variant="outline" className="text-xs font-normal text-muted-foreground">Optional</Badge>
                            )}
                          </CardTitle>
                          <CardDescription>{s.description}</CardDescription>
                        </div>
                      </div>
                    </CardHeader>
                    <CardFooter className="flex items-center justify-between gap-2 border-t bg-muted/30 px-6 py-3">
                      {page ? (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => openInNewTab(page)}
                          aria-label={`Open ${s.label} in a new tab`}
                        >
                          {finished ? "Review in HRM" : "Open in HRM"}
                          <ChevronRight className="size-4" aria-hidden />
                        </Button>
                      ) : (
                        <span className="text-sm text-muted-foreground">No dedicated page</span>
                      )}
                      {finished ? (
                        <Badge className="gap-1">
                          <CheckCircle2 className="size-3.5" aria-hidden /> Done
                        </Badge>
                      ) : (
                        <Button
                          size="sm"
                          variant={s.open ? "default" : "secondary"}
                          disabled={completing !== null || !s.open}
                          onClick={() => markComplete(s.key)}
                        >
                          {completing === s.key ? "Marking…" : "Mark complete"}
                        </Button>
                      )}
                    </CardFooter>
                  </Card>
                </li>
              );
            })}
          </ol>

          <Card className="border-dashed">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">Optional steps</CardTitle>
              <CardDescription>
                These can be revisited anytime under Configuration — they do not block
                payroll usage.
              </CardDescription>
            </CardHeader>
            <CardContent className="pb-4">
              <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                {(state.optionalSteps ?? []).map((key) => {
                  const def = steps.find((x) => x.key === key);
                  return (
                    <Badge key={key} variant="secondary" className="gap-1">
                      <CheckCircle2 className="size-3" aria-hidden /> {def?.label ?? key}
                    </Badge>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
