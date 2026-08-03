import { createFileRoute, Link } from "@tanstack/react-router";
import { ChevronRight, CircleDashed } from "lucide-react";
import { configurationGroups } from "@/modules/hrm/nav";
import { AppShell } from "@/platform/components/AppShell";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/configuration/")({
  head: () => ({
    meta: [
      { title: "Configuration — Meridian ERP HRM" },
      { name: "description", content: "The single entry point for business setup, process design, security and technical settings." },
      { property: "og:title", content: "Configuration — Meridian ERP HRM" },
      { property: "og:description", content: "The single entry point for business setup, process design, security and technical settings." },
    ],
  }),
  component: Configuration,
});

function Configuration() {
  const all = configurationGroups.flatMap((g) => g.items);
  const ready = all.filter((i) => i.to).length;

  return (
    <AppShell>
      <PageHeader
        eyebrow="HRM"
        title="Configuration"
        description="Everything an administrator sets up lives here — nothing configuration-related is scattered into an operational screen elsewhere."
        meta={
          <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
            {ready} of {all.length} settings available in this build
          </span>
        }
      />
      <div className="grid gap-5 sm:grid-cols-2">
        {configurationGroups.map((group) => (
          <section key={group.label} aria-label={group.label} className="rounded-lg border bg-surface p-5">
            <h2 className="text-sm font-semibold">{group.label}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{group.description}</p>
            <ul className="mt-4 divide-y">
              {group.items.map((item) =>
                item.to ? (
                  <li key={item.label}>
                    <Link
                      to={item.to}
                      className="flex items-center justify-between gap-3 py-2.5 text-left text-sm transition-colors hover:text-primary"
                    >
                      <span className="min-w-0">
                        <span className="block truncate font-medium">{item.label}</span>
                        <span className="block truncate text-xs text-muted-foreground">{item.detail}</span>
                      </span>
                      <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    </Link>
                  </li>
                ) : (
                  <li
                    key={item.label}
                    className="flex items-center justify-between gap-3 py-2.5 text-left text-sm opacity-60"
                  >
                    <span className="min-w-0">
                      <span className="block truncate font-medium">{item.label}</span>
                      <span className="block truncate text-xs text-muted-foreground">{item.detail}</span>
                    </span>
                    <span className="flex shrink-0 items-center gap-1.5 text-[11px] text-muted-foreground">
                      <CircleDashed className="size-3.5" aria-hidden />
                      Not built yet
                    </span>
                  </li>
                ),
              )}
            </ul>
          </section>
        ))}
      </div>
    </AppShell>
  );
}
