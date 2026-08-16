import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/relations/protected-disclosures")({
  component: ProtectedDisclosures,
});
type Row = Record<string, unknown>;

function ProtectedDisclosures() {
  const queue = useApi(async () => (await realApi.protectedDisclosures()).items as Row[], []);
  const [selected, setSelected] = useState<Row | null>(null);
  const [notes, setNotes] = useState("");
  const [outcome, setOutcome] = useState("");
  const [assignedToSubjectId, setAssignedToSubjectId] = useState("");
  const open = async (id: string) => {
    try {
      setSelected(await realApi.protectedDisclosure(id));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not open disclosure");
    }
  };
  const transition = async (status: string) => {
    if (!selected) return;
    try {
      const updated = await realApi.transitionProtectedDisclosure(String(selected.id), {
        status,
        triageNotes: notes || undefined,
        outcome: outcome || undefined,
        assignedToSubjectId: assignedToSubjectId || undefined,
      });
      setSelected(updated);
      queue.reload();
      toast.success(`Disclosure moved to ${status}`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not update disclosure", {
        duration: Infinity,
      });
    }
  };
  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Relations and safety"
          title="Protected disclosures"
          description="A separate, investigator-only workspace. Anonymous reports never become ordinary employee-relations or HR-request records."
        />
        <div className="grid gap-6 lg:grid-cols-2" data-testid="protected-disclosures">
          <Card>
            <CardHeader>
              <CardTitle>Restricted queue</CardTitle>
              <CardDescription>The queue excludes narrative and reporter identity.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              {(queue.data ?? []).map((item) => (
                <button
                  key={String(item.id)}
                  className="flex w-full items-center justify-between rounded-md border p-3 text-left"
                  onClick={() => open(String(item.id))}
                >
                  <span>
                    <strong>{String(item.caseReference)}</strong>
                    <span className="block text-xs text-muted-foreground">
                      {String(item.category)} · {String(item.severity)}
                    </span>
                  </span>
                  <StatusBadge status={String(item.status)} />
                </button>
              ))}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Investigation</CardTitle>
              <CardDescription>
                Views and transitions are written to the protected audit history.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {selected ? (
                <>
                  <div className="flex justify-between">
                    <strong>{String(selected.caseReference)}</strong>
                    <StatusBadge status={String(selected.status)} />
                  </div>
                  <p className="rounded-md border p-3 text-sm">{String(selected.description)}</p>
                  <div>
                    <Label htmlFor="disclosure-investigator">Investigator subject ID</Label>
                    <Input
                      id="disclosure-investigator"
                      value={assignedToSubjectId}
                      onChange={(e) => setAssignedToSubjectId(e.target.value)}
                    />
                  </div>
                  <div>
                    <Label htmlFor="triage-notes">Restricted investigator notes</Label>
                    <Textarea
                      id="triage-notes"
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                    />
                  </div>
                  <div>
                    <Label htmlFor="disclosure-outcome">Outcome</Label>
                    <Textarea
                      id="disclosure-outcome"
                      value={outcome}
                      onChange={(e) => setOutcome(e.target.value)}
                    />
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={() => transition("triage")}>
                      Triage
                    </Button>
                    <Button variant="outline" onClick={() => transition("investigating")}>
                      Investigate
                    </Button>
                    <Button onClick={() => transition("resolved")}>Resolve</Button>
                    <Button variant="outline" onClick={() => transition("dismissed")}>
                      Dismiss
                    </Button>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {((selected.history as Row[]) ?? []).length} protected audit events.
                  </p>
                  <ul
                    className="max-h-40 space-y-1 overflow-auto text-xs"
                    data-testid="protected-audit"
                  >
                    {((selected.history as Row[]) ?? []).map((event) => (
                      <li key={String(event.id)} className="rounded border p-2">
                        {String(event.action)} · {String(event.actorSubjectId ?? "system")}
                        {event.toStatus ? ` · ${String(event.toStatus)}` : ""}
                      </li>
                    ))}
                  </ul>
                </>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Select a protected disclosure to open its investigator-only record.
                </p>
              )}
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}
