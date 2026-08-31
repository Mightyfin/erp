import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/hrm/talent/")({
  head: () => ({
    meta: [
      { title: "Talent — Mightyfin HRMS" },
      { name: "description", content: "Performance, learning, succession and skills." },
      { property: "og:title", content: "Talent — Mightyfin HRMS" },
      { property: "og:description", content: "Performance, learning, succession and skills." },
    ],
  }),
  component: () => <AreaOverview area={areas.talent} />,
});
