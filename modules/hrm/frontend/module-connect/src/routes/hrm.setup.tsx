import { createFileRoute } from "@tanstack/react-router";

// M50: the true input wizard now lives in the WelcomeOverlay modal on /hrm
// (rendered while setup state is pending). The standalone /hrm/setup route is
// retired to a plain redirect so no one lands on the obsolete link-checklist.
export const Route = createFileRoute("/hrm/setup")({
  beforeLoad: () => {
    window.location.replace("/hrm");
  },
  component: () => null,
});
