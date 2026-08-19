import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";
import { AlertCircle, CheckCircle2, ChevronRight, Sparkles } from "lucide-react";

/**
 * M49: first-time setup wizard over the real backend state machine.
 *
 * GET  /hrm/setup/state   — { status, resumeStepKey, completedSteps,
 *                            optionalSteps, completionPercent }
 * GET  /hrm/setup/steps   — [ { key, label, description, mandatory,
 *                            completed, open } ]
 * POST /hrm/setup/steps/{key} — mark a step complete (dataJson optional)
 * POST /hrm/setup/finish      — refuses until the mandatory prefix is done
 *
 * The backend owns gating and persistence; this page is a guided view that
 * lets the operator open each step's page in the HRM and confirm it.
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

export const Route = createFileRoute("/hrm/setup")({
  head: () => ({
    meta: [
      { title: "Organisation setup — Mightyfin ERP" },
      {
        name: "description",
        content: "Set up the HR module from empty: organisation, structure, employment, leave, payroll and first employees.",
      },
    ],
  }),
  component: SetupPage,
});

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

function SetupPage() {
  const navigate = useNavigate();
  const api = useApi(async () => {
    const [state, steps] = await Promise.all([realApi.setupState(), realApi.setupSteps()]);
    return { state, steps: steps as StepDto[] };
  }, []);

  const [completing, setCompleting] = useState<string | null>(null);
  const [doneIds, setDoneIds] = useState<string[]>([]);
  const [message, setMessage] = useState<{ text: string; kind: "error" | "info" } | null>(null);

  const isComplete = api.data?.state?.status === "complete";

  // Wizard finished → back to the dashboard after a short pause.
  useEffect(() => {
    if (isComplete) {
      const t = setTimeout(() => {
        navigate({ to: "/hrm" });
      }, 2200);
      return () => clearTimeout(t);
    }
  }, [isComplete, navigate]);

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
      setMessage({ text: "Setup complete — heading to the dashboard…", kind: "info" });
    } catch (err) {
      setMessage({ text: setupErrorText(err), kind: "error" });
    } finally {
      setCompleting(null);
    }
  };

  const state = api.data?.state;
  const steps = api.data?.steps ?? [];

  if (api.isLoading) {
    return (
      <AuthGate>
        <AppShell>
          <div className="mx-auto flex max-w-4xl items-center justify-center py-24 text-muted-foreground">
            Loading setup state…
          </div>
        </AppShell>
      </AuthGate>
    );
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="First-time setup"
          title="Organisation setup"
          description={
            isComplete
              ? "Your organisation is ready — redirecting to the HRM dashboard."
              : "Work through each step at your own pace. Mark a step complete once you have set it up in the HRM. Required steps must finish before payroll can be used."
          }
          primaryAction={
            <Button variant="ghost" asChild>
              <a href="/hrm">Skip to dashboard</a>
            </Button>
          }
        />

        <div className="mx-auto max-w-4xl space-y-6 p-4 sm:p-6 sm:px-0">
          <Card>
            <CardHeader>
              <div className="flex items-center gap-3">
                <Sparkles className="size-6 text-primary" aria-hidden />
                <CardTitle>Your progress</CardTitle>
                <Badge variant={isComplete ? "default" : "secondary"} className="ml-auto">
                  {isComplete ? "Complete" : `${state?.completionPercent ?? 0}% done`}
                </Badge>
              </div>
              <CardDescription>
                {isComplete
                  ? "Everything is set up. Redirecting you to the dashboard…"
                  : "The required steps gate payroll usage; the optional ones surface in the after-onboarding checklist."}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Progress value={state?.completionPercent ?? 0} className="h-2" />
            </CardContent>
            <CardFooter className="justify-end border-t bg-muted/40 px-6 py-3">
              {!isComplete && (
                <Button onClick={finishWizard} disabled={completing === "finish"}>
                  {completing === "finish" ? "Finishing…" : "Finish setup"}
                </Button>
              )}
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
                  <Card>
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
                        <Button variant="outline" size="sm" asChild>
                          <a href={page}>
                            {finished ? "Review in HRM" : "Open in HRM"}
                            <ChevronRight className="size-4" aria-hidden />
                          </a>
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

          {!isComplete && (
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
                  {(state?.optionalSteps ?? []).map((key) => {
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
          )}
        </div>
      </AppShell>
    </AuthGate>
  );
}
