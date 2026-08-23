import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, FileSpreadsheet, History, Upload } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { ExportButton } from "@/platform/components/ImportExport/ExportButton";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/attendance/import")({
  head: () => ({
    meta: [
      { title: "Import attendance — New World Cargo HRM" },
      { name: "description", content: "Import attendance from the shared spreadsheet workflow and review the results." },
    ],
  }),
  component: AttendanceImportPage,
});

function AttendanceImportPage() {
  const history = useApi(realApi.timeOperationsHistory, []);
  const imports = history.data?.imports ?? [];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave / attendance"
          title="Import attendance"
          description="Bring clock-in and clock-out records into the HRM, validate them, and send derived overtime to review."
          meta={<Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground"><FileSpreadsheet className="size-3" aria-hidden /> Shared Import/Export workflow</Badge>}
          primaryAction={
            <div className="flex flex-wrap gap-2">
              <ImportDialog typeKey="attendance" onDone={() => history.reload()} />
              <ExportButton typeKey="attendance" fileName="attendance" />
            </div>
          }
        />

        <div className="space-y-6" data-testid="attendance-import-page">
          <section className="grid gap-3 md:grid-cols-3" aria-label="Import steps">
            <Step number="1" title="Choose your file" detail="Use CSV or Excel from your attendance device or working sheet." active />
            <Step number="2" title="Check the preview" detail="Map columns and let the server identify errors before writing." />
            <Step number="3" title="Review overtime" detail="Imported hours are reconciled before they can enter payroll." />
          </section>

          <Card className="border-primary/20 bg-primary-soft/20 shadow-none">
            <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground"><Upload className="size-5" aria-hidden /></span>
                <div>
                  <p className="font-semibold">Use the shared import flow</p>
                  <p className="mt-1 max-w-2xl text-sm leading-5 text-muted-foreground">The same mapping and server-preview experience is used across HRM data types. Nothing is written until you confirm the accepted rows.</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
            <Card className="shadow-none">
              <CardHeader className="pb-4"><CardTitle className="flex items-center gap-2 text-base"><History className="size-4" aria-hidden />Recent attendance imports</CardTitle><CardDescription>Persisted import activity for your organisation scope.</CardDescription></CardHeader>
              <CardContent>
                {history.loading ? <p className="text-sm text-muted-foreground">Loading import history…</p> : imports.length === 0 ? <div className="rounded-xl border border-dashed p-8 text-center"><p className="font-medium">No attendance imports yet</p><p className="mt-1 text-sm text-muted-foreground">Your confirmed imports will appear here with accepted and rejected row counts.</p></div> : <div className="divide-y rounded-xl border">{imports.slice(0, 8).map((item) => <div key={String(item.batchId ?? item.id)} className="flex flex-wrap items-center justify-between gap-3 p-4"><div><p className="font-medium">{String(item.fileName ?? "Attendance import")}</p><p className="mt-1 text-xs text-muted-foreground">Batch {String(item.batchId ?? item.id ?? "—")}</p></div><div className="flex items-center gap-2 text-xs"><Badge variant="outline" className="border-success/30 bg-success-soft text-success-foreground">{String(item.importedCount ?? item.created ?? 0)} imported</Badge><Badge variant="outline" className="border-danger/30 bg-danger-soft text-danger">{String(item.rejectedCount ?? item.errors ?? 0)} rejected</Badge></div></div>)}</div>}
              </CardContent>
            </Card>

            <Card className="h-fit shadow-none"><CardHeader className="pb-4"><CardTitle className="text-base">Next step</CardTitle><CardDescription>Attendance import does not approve overtime automatically.</CardDescription></CardHeader><CardContent><p className="text-sm leading-5 text-muted-foreground">After a successful import, open the review queue to check scheduled hours, worked hours, overtime multipliers, and decision notes before payroll.</p><Button asChild className="mt-4 w-full gap-2"><Link to="/hrm/time/operations">Review overtime <ArrowRight className="size-4" aria-hidden /></Link></Button></CardContent></Card>
          </div>
        </div>
      </AppShell>
    </AuthGate>
  );
}

function Step({ number, title, detail, active = false }: { number: string; title: string; detail: string; active?: boolean }) {
  return <div className={`rounded-xl border p-4 ${active ? "border-primary/40 bg-primary-soft/30" : "bg-card"}`}><div className="flex items-center gap-2"><span className={`flex size-7 items-center justify-center rounded-full text-xs font-semibold ${active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}>{number}</span><p className="text-sm font-semibold">{title}</p></div><p className="mt-2 text-xs leading-5 text-muted-foreground">{detail}</p></div>;
}
