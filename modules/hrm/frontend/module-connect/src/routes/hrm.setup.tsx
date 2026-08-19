import { createFileRoute, redirect } from "@tanstack/react-router";
import { WelcomeOverlay } from "@/platform/components/WelcomeOverlay";

// M50.11: the true input setup wizard now lives at its own full route
// /hrm/setup instead of the full-screen overlay. The page has its own clean
// layout (header strip, progress rail, step form, footer) while the app
// behind it stays fully accessible. The backend still owns the gating:
// while setup is PENDING the shell keeps the wizard reachable, and Finish
// setup lifts the lock. If setup is already complete, landing here sends
// the user back to the dashboard.
export const Route = createFileRoute("/hrm/setup")({
  beforeLoad: async () => {
    try {
      const { realApi } = await import("@/platform/use-api");
      const state = await realApi.setupState();
      if (state?.status === "complete") {
        throw redirect({ to: "/hrm" });
      }
    } catch (e) {
      if ((e as { redirect?: unknown })?.redirect) throw e;
      // Cannot reach the state endpoint yet (e.g. still bootstrapping) —
      // allow the page to render; its own useApi will re-check.
    }
  },
  component: SetupPage,
});

function SetupPage() {
  return <WelcomeOverlay pageMode />;
}
