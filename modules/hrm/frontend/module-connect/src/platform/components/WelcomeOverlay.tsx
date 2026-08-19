import { Button } from "@/components/ui/button";

/**
 * M49: welcome gate overlay. While the organisation's setup state is
 * "pending", every HRM page is covered by an intense-but-slightly-transparent
 * white blur carrying the rotating two-arrow "switch" mark (borrowed from the
 * M47 scope-switch overlay so both covers feel like one design language) and
 * a single clear action — "Continue to setup" — that drives the operator into
 * the wizard. The backend owns the decision: GET /hrm/setup/state returns
 * "pending" only while the wizard is unfinished, and "complete" lifts the
 * cover. Confined branch HR never see this cover because they can never run
 * the wizard (the backend refuses with setup-confined), and the shell already
 * filters them out.
 */
export function WelcomeOverlay() {
  return (
    <div
      className="fixed inset-0 z-[110] flex flex-col items-center justify-center bg-white/90 backdrop-blur-xl"
      role="dialog"
      aria-modal="true"
      aria-label="Organisation setup required"
    >
      <div className="flex flex-col items-center gap-6 px-6 text-center">
        {/* Rotating two-arrow switch mark — same glyph family as the M47
            scope-switch overlay, rendered static so it reads as a logo. */}
        <svg
          viewBox="0 0 48 48"
          fill="none"
          className="size-16 animate-spin-slow text-primary"
          aria-hidden
        >
          <path
            d="M10 17h22m0 0-6-6m6 6-6 6M38 31H16m0 0 6-6m-6 6 6 6"
            stroke="currentColor"
            strokeWidth="3.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold text-foreground sm:text-3xl">
            Welcome to Mightyfin ERP
          </h1>
          <p className="max-w-md text-sm text-muted-foreground sm:text-base">
            Your organisation is not set up yet. A few short steps stand between
            you and the full HRM — company details, structure, leave rules and
            your first employees.
          </p>
        </div>
        <Button size="lg" asChild>
          <a href="/hrm/setup">Continue to setup</a>
        </Button>
        <p className="text-xs text-muted-foreground">
          You can come back to this at any time under First-time setup.
        </p>
      </div>
    </div>
  );
}
