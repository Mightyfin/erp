import { createFileRoute } from "@tanstack/react-router";
import { WelcomeOverlay } from "@/platform/components/WelcomeOverlay";

// M50.11: the true input setup wizard lives at its own full route /hrm/setup
// instead of the full-screen overlay. The page has its own clean layout
// (header strip, progress rail, step form, footer) while the app behind it
// stays fully accessible. The backend still owns the gating: while setup is
// PENDING the shell keeps the wizard reachable, and Finish setup lifts the
// lock.
// M50.14: when setup is already complete, /hrm/setup no longer redirects to
// the dashboard — the component renders a "You are all set up" completion
// view with Go to home / Make changes, so the page doubles as the place to
// update configuration later.
export const Route = createFileRoute("/hrm/setup")({
  component: SetupPage,
});

function SetupPage() {
  return <WelcomeOverlay pageMode />;
}
