import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Check, Lock, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { privacyApi } from "@/mock/privacy";
import type { ProcessingPurpose } from "@/mock/privacy";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/people/privacy")({
  head: () => ({
    meta: [
      { title: "Privacy and consent — Meridian ERP HRM" },
      { name: "description", content: "What personal data is held about you, why, and how to exercise your rights over it." },
      { property: "og:title", content: "Privacy and consent — Meridian ERP HRM" },
      { property: "og:description", content: "What personal data is held about you, why, and how to exercise your rights over it." },
    ],
  }),
  component: PrivacyPage,
});

const consentLabel: Record<ProcessingPurpose["consent"], string> = {
  granted: "Consent given",
  withdrawn: "Consent withdrawn",
  "not-required": "Consent not required",
};

function PurposeCard({ p }: { p: ProcessingPurpose }) {
  const [confirming, setConfirming] = useState(false);
  const [consent, setConsent] = useState(p.consent);

  return (
    <li className="rounded-lg border bg-surface p-5">
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
        <div className="min-w-0">
          <h3 className="text-sm font-semibold">{p.purpose}</h3>
          <p className="mt-1 text-sm text-muted-foreground">{p.dataHeld}</p>

          <dl className="mt-3 grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
            <div>
              <dt className="text-muted-foreground">Why we can hold it</dt>
              <dd className="font-medium">{p.lawfulBasis}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Kept for</dt>
              <dd className="font-medium">{p.retention}</dd>
            </div>
            {p.crossBorder ? (
              <div className="sm:col-span-2">
                <dt className="text-muted-foreground">Where it is processed</dt>
                <dd className="font-medium">{p.crossBorder}</dd>
              </div>
            ) : null}
          </dl>
        </div>

        <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
          <span
            className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${
              consent === "granted"
                ? "border-success/30 bg-success-soft text-success"
                : consent === "withdrawn"
                  ? "border-warning/40 bg-warning-soft text-warning"
                  : "border-border bg-muted text-muted-foreground"
            }`}
          >
            {consent === "granted" ? (
              <Check className="size-3.5" aria-hidden />
            ) : consent === "withdrawn" ? (
              <AlertTriangle className="size-3.5" aria-hidden />
            ) : (
              <Lock className="size-3.5" aria-hidden />
            )}
            {consentLabel[consent]}
          </span>

          {p.withdrawable && consent === "granted" ? (
            <Button variant="outline" size="sm" onClick={() => setConfirming(true)}>
              Withdraw consent
            </Button>
          ) : null}

          {p.withdrawable && consent === "withdrawn" ? (
            <Button variant="ghost" size="sm" onClick={() => setConsent("granted")}>
              Give consent again
            </Button>
          ) : null}

          {!p.withdrawable && p.notRequiredReason ? (
            <p className="max-w-56 text-left text-[11px] text-muted-foreground sm:text-right">
              {p.notRequiredReason}
            </p>
          ) : null}
        </div>
      </div>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Withdraw consent for {p.purpose.toLowerCase()}?</AlertDialogTitle>
            <AlertDialogDescription>
              {p.consequenceOfWithdrawal} This is recorded and takes effect immediately. You can give
              consent again at any time.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep consent</AlertDialogCancel>
            <AlertDialogAction onClick={() => setConsent("withdrawn")}>
              Withdraw consent
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </li>
  );
}

function PrivacyPage() {
  const purposes = useMock(() => privacyApi.purposes());
  const requests = useMock(() => privacyApi.requests());

  return (
    <AppShell>
      <PageHeader
        eyebrow="People"
        title="Privacy and consent"
        description="Everything held about you, grouped by why it is held rather than by where it is stored."
        primaryAction={
          <Button asChild>
            <Link to="/requests/new">Raise a data request</Link>
          </Button>
        }
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
            <ShieldCheck className="size-3.5" aria-hidden />
            You can access, correct or ask us to erase your data
          </span>
        }
      />

      <section aria-label="What we hold and why">
        <h2 className="text-sm font-semibold">What we hold, and why</h2>
        <Async state={purposes} rows={4}>
          {(rows) => (
            <ul className="mt-3 space-y-3">
              {rows.map((p) => (
                <PurposeCard key={p.id} p={p} />
              ))}
            </ul>
          )}
        </Async>
      </section>

      <section aria-label="Your requests" className="pt-2">
        <h2 className="text-sm font-semibold">Your data requests</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          Each request has a statutory response deadline. We tell you if something cannot be actioned,
          and why.
        </p>
        <Async state={requests} rows={3}>
          {(rows) => (
            <ul className="mt-3 divide-y rounded-lg border bg-surface">
              {rows.map((r) => (
                <li key={r.id} className="p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs text-muted-foreground">{r.id}</span>
                    <span className="text-sm font-medium">{r.type}</span>
                    <StatusBadge status={r.status} />
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">{r.scope}</p>
                  <p className="mt-2 text-xs">
                    <span className="text-muted-foreground">Next: </span>
                    {r.nextAction}
                    <span className="text-muted-foreground"> · due {r.dueDate}</span>
                    <span className="text-muted-foreground"> · statutory deadline {r.statutoryDeadline}</span>
                  </p>
                  {r.legalHold ? (
                    <p className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
                      <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                      <span>{r.legalHold}</span>
                    </p>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>
    </AppShell>
  );
}
