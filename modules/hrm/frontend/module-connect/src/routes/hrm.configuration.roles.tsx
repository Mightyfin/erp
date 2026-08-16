import { createFileRoute, redirect } from "@tanstack/react-router";

// M34 replaced the prototype-only matrix with the backend-enforced,
// tenant-scoped review surface in Security and compliance. Preserve the old
// bookmark while ensuring administrators never edit a non-persistent matrix.
export const Route = createFileRoute("/hrm/configuration/roles")({
  beforeLoad: () => {
    throw redirect({ to: "/hrm/configuration/compliance" });
  },
  component: () => null,
});
