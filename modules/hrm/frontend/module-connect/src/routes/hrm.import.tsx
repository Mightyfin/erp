// M53 — data import/export tool landing page (/hrm/import).
//
// One front door for every data type the HRM can move to and from spreadsheets.
// Import reuses the shared M31 ImportDialog (drag-drop xlsx/csv, searchable
// column mapper, required-field enforcement, row-by-row preview, approved-row
// apply). Export lists the same schemas and downloads them as Excel or CSV
// through the shared export endpoint.

import { createFileRoute } from "@tanstack/react-router";
import {
  ArrowDownToLine,
  ArrowRight,
  ArrowUpFromLine,
  Check,
  ChevronLeft,
  Download,
  FileSpreadsheet,
  FileType2,
  Loader2,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import type { ApiState } from "@/platform/use-api";
import { useApi } from "@/platform/use-api";
import { realApi } from "@/platform/use-api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/import")({
  component: () => (
    <AppShell>
      <AuthGate>
        <ImportTool />
      </AuthGate>
    </AppShell>
  ),
});

type Direction = "import" | "export" | null;

type SchemaShape = Array<{ typeKey: string; displayName: string; fields: Array<{ key: string; label: string; required: boolean }> }>;

function useSchemas(): ApiState<SchemaShape> {
  return useApi<SchemaShape>(() => realApi.importSchemas(), []);
}

function ImportTool() {
  const schemas = useSchemas();
  const [direction, setDirection] = useState<Direction>(null);
  const [typeKey, setTypeKey] = useState<string>("");
  const [reloadKey, setReloadKey] = useState(0);

  const data = schemas.data ?? [];
  const schema = data?.find((s) => s.typeKey === typeKey);

  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-16">
      <PageHeader
        title="Import & export data"
        description="Move records between this HRM and the spreadsheets your organisation already keeps. Bulk imports go through preview first — nothing lands without your approval."
      />

      <Async state={schemas}>
        {(data) => (
          <>
        {data.length === 0 && (
          <p className="text-sm text-muted-foreground">No importable data types are registered yet.</p>
        )}

        {/* Direction chooser */}
        {direction === null && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Card className="cursor-pointer transition-shadow hover:shadow-md" onClick={() => setDirection("import")}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <ArrowDownToLine className="size-5 text-primary" aria-hidden />
                  Import data
                </CardTitle>
                <CardDescription>
                  Bring records in from a spreadsheet. The tool reads your sheet's own column titles, lets you map
                  them to HRM fields, and validates every row before anything is created.
                </CardDescription>
              </CardHeader>
            </Card>
            <Card className="cursor-pointer transition-shadow hover:shadow-md" onClick={() => setDirection("export")}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <ArrowUpFromLine className="size-5 text-success" aria-hidden />
                  Export data
                </CardTitle>
                <CardDescription>
                  Pull records out as a spreadsheet you can share, archive or hand to payroll and accounts. Every
                  type keeps a round-trip-safe layout the importer understands.
                </CardDescription>
              </CardHeader>
            </Card>
          </div>
        )}

        {/* Type picker */}
        {direction !== null && !schema && (
          <div>
            <Button variant="ghost" size="sm" onClick={() => setDirection(null)} className="mb-4">
              <ChevronLeft className="size-4" aria-hidden />
              Back
            </Button>
            <h2 className="mb-1 text-lg font-semibold">
              {direction === "import" ? "What are you bringing in?" : "What are you taking out?"}
            </h2>
            <p className="mb-4 text-sm text-muted-foreground">
              One tool, every data type. Pick the kind of record you want to move.
            </p>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {data?.map((s) => (
                <Card
                  key={s.typeKey}
                  className="cursor-pointer transition-shadow hover:shadow-md"
                  onClick={() => setTypeKey(s.typeKey)}
                >
                  <CardHeader className="pb-3">
                    <CardTitle className="flex items-center gap-2 text-base">
                      <FileSpreadsheet className="size-4 text-primary" aria-hidden />
                      {s.displayName}
                    </CardTitle>
                    <CardDescription>{s.fields.length} fields · {s.fields.filter((f) => f.required).length} required</CardDescription>
                  </CardHeader>
                </Card>
              ))}
            </div>
            <div className="mt-6 rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
              More data types appear here automatically as their schemas are registered on the backend. This list is
              driven by the server, not hard-coded buttons.
            </div>
          </div>
        )}

        {/* Import flow — reuse the shared M31 dialog for the full map/preview/apply journey */}
        {direction === "import" && schema ? (
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              <Button variant="ghost" size="sm" onClick={() => setTypeKey("")} className="-ml-2">
                <ChevronLeft className="size-4" aria-hidden />
                Data types
              </Button>
              <ArrowRight className="size-3" aria-hidden />
              <span className="font-medium text-foreground">{schema.displayName}</span>
              <span className="text-xs uppercase tracking-wide">· Import</span>
            </div>
            <ImportDialog
              key={`${schema.typeKey}-${reloadKey}`}
              typeKey={schema.typeKey}
              onDone={() => setReloadKey((k) => k + 1)}
            />
            <div className="rounded-md bg-surface-muted p-3 text-xs text-muted-foreground">
              <strong className="font-semibold text-foreground">What must be in the sheet:</strong> required fields
              are marked on the mapper. Rows missing them are refused with an exact reason. Rows matching an existing
              record by its identity key (employee number, NRC, NAPSA) are flagged instead of silently overwriting.
            </div>
          </div>
        ) : null}

        {/* Export flow */}
        {direction === "export" && schema ? (
          <ExportFlow schema={schema} onReset={() => setTypeKey("")} />
        ) : null}
          </>
        )}
      </Async>
    </div>
  );
}

// ============================ EXPORT FLOW ============================

// NOTE: the side-nav item for this page is wired in AppShell's menu config
// alongside "Master data operations" and "Data quality".

function ExportFlow({
  schema,
  onReset,
}: {
  schema: { typeKey: string; displayName: string; fields: Array<{ key: string; label: string; required: boolean }> };
  onReset: () => void;
}) {
  const [format, setFormat] = useState<"xlsx" | "csv">("xlsx");
  const [busy, setBusy] = useState(false);

  const download = async () => {
    setBusy(true);
    try {
      const blob = await realApi.importExportBlob(schema.typeKey, format === "xlsx" ? "format=xlsx" : undefined);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${schema.typeKey}-export.${format}`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      toast.success(`${schema.displayName} exported as ${format.toUpperCase()}.`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Export failed — the server returned an error.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-base">Export {schema.displayName}</CardTitle>
            <CardDescription>Every field in the layout is round-trip safe — the same file can be re-imported.</CardDescription>
          </div>
          <Button variant="ghost" size="sm" onClick={onReset}>
            <ChevronLeft className="size-4" aria-hidden />
            Data types
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-1.5">
          {schema.fields.map((f) => (
            <span key={f.key} className="inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs text-muted-foreground">
              {f.label}
              {f.required ? <Badge variant="outline" className="h-4 px-1 text-[9px]">req</Badge> : null}
            </span>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-3 border-t pt-4">
          <div className="flex items-center gap-2">
            <Button
              variant={format === "xlsx" ? "secondary" : "outline"}
              size="sm"
              onClick={() => setFormat("xlsx")}
            >
              <FileType2 className="size-4" aria-hidden />
              Excel (.xlsx)
            </Button>
            <Button
              variant={format === "csv" ? "secondary" : "outline"}
              size="sm"
              onClick={() => setFormat("csv")}
            >
              CSV
            </Button>
          </div>
          <Button disabled={busy} onClick={() => void download()}>
            {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Download className="size-4" aria-hidden />}
            Download
          </Button>
        </div>
        <p className="text-xs text-muted-foreground">
          The export uses canonical column keys. If your organisation keeps its own column names, map them on import —
          the tool reads whatever titles your sheet uses.
        </p>
      </CardContent>
    </Card>
  );
}
