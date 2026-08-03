import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/lifecycle/")({
  head: () => ({
    meta: [
      { title: "Lifecycle — Meridian ERP HRM" },
      { name: "description", content: "Onboarding, movements, assets, separation and rehire." },
      { property: "og:title", content: "Lifecycle — Meridian ERP HRM" },
      { property: "og:description", content: "Onboarding, movements, assets, separation and rehire." },
    ],
  }),
  component: () => <AreaOverview area={areas.lifecycle} />,
});
