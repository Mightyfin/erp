/**
 * M31 — shared Import/Export tool for every CRUD list page.
 *
 * The org arrives with Excel sheets as their only system of record, so this
 * dialog is the front door for migrating spreadsheets in:
 *   1. Drop a .csv / .xlsx file (reads both client-side; XLSX via SheetJS)
 *   2. Map every file column to a system field via a searchable dropdown
 *      (unmatched columns can be "Skip"-ped; unmapped fields stay blank)
 *   3. Auto-map fields whose labels loosely match the file headers
 *   4. Live preview: the server validates every row (create / update / skip /
 *      error) so bad rows are visible before anything is written
 *   5. Confirm → server applies only the accepted rows, per-row report back
 *      with a downloadable error CSV.
 *
 * Pages reuse it with one line: <ImportDialog typeKey="workers" onDone={reload} />
 */
import { useEffect, useMemo, useRef, useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList,
} from "@/components/ui/command";
import { Progress } from "@/components/ui/progress";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Check, ChevronDown, Download, FileSpreadsheet, Loader2, Search,
  ArrowUpFromLine, X, CircleCheck, CircleAlert, CircleMinus, Pen,
} from "lucide-react";
import * as XLSX from "xlsx";
import { cn } from "@/lib/utils";
import { realApi } from "@/platform/use-api";
import { toast } from "sonner";

/* ---------------------------------------------------------------- types */
export interface ImportSchemaField {
  key: string;
  label: string;
  required: boolean;
  naturalKey?: boolean;
  example?: string;
  formatNote?: string;
}
export interface ImportSchema {
  typeKey: string;
  displayName: string;
  fields: ImportSchemaField[];
}
export interface ImportDialogProps {
  /** Which importable type this instance serves, e.g. "workers". */
  typeKey: string;
  /** Called after a successful apply so the list page can refresh. */
  onDone?: () => void;
  /** Default sample of mapped rows used by the demo-mode preview. */
  demoSample?: Array<Record<string, string>>;
  /** Dialog remains the compact default; embedded gives task pages a full workflow surface. */
  presentation?: "dialog" | "embedded";
}

interface FileColumn { name: string; sample: string }

/* ---------------------------------------------------------------- csv parse */
/** Quote-aware CSV parser mirroring the server's ImportRowParser. */
function parseCsvLine(line: string): string[] {
  const cells: string[] = [];
  let cell = "";
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (inQuotes) {
      if (ch === '"' && i + 1 < line.length && line[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') inQuotes = false;
      else cell += ch;
    } else {
      if (ch === '"') inQuotes = true;
      else if (ch === ",") { cells.push(cell); cell = ""; }
      else cell += ch;
    }
  }
  cells.push(cell);
  return cells;
}
function parseCsvText(text: string): string[][] {
  const lines: string[][] = [];
  let cur = "";
  for (const raw of text.replace(/\r\n/g, "\n").split("\n")) {
    // Honor unclosed quotes spanning lines (multi-line quoted value).
    const quoteCount = (raw.match(/"/g) ?? []).length % 2;
    if (cur !== "" || (cur === "" && quoteCount !== 0 && !lines.length)) {
      cur += (cur === "" ? "" : "\n") + raw;
      if (quoteCount !== 0) continue;
      lines.push(parseCsvLine(cur));
      cur = "";
    } else {
      lines.push(parseCsvLine(raw));
    }
  }
  if (cur !== "") lines.push(parseCsvLine(cur));
  return lines.filter((l) => l.length > 1 || (l[0] ?? "").trim() !== "");
}

async function readFileRows(file: File): Promise<{ headers: string[]; rows: string[][] }> {
  const buf = await file.arrayBuffer();
  if (/\.xlsx?$/i.test(file.name)) {
    const wb = XLSX.read(buf, { type: "array" });
    const ws = wb.Sheets[wb.SheetNames[0]];
    const raw: unknown[][] = XLSX.utils.sheet_to_json(ws, { header: 1, defval: "" });
    const rows = raw.filter((r) => r.some((c) => String(c ?? "").trim() !== "")) as string[][];
    if (rows.length === 0) throw new Error("The workbook has no data rows.");
    return { headers: rows[0].map(String), rows: rows.slice(1).map((r) => r.map(String)) };
  }
  const text = new TextDecoder("utf-8").decode(buf);
  // Strip the UTF-8 BOM if present.
  const all = parseCsvText(text.startsWith("\uFEFF") ? text.slice(1) : text);
  if (all.length === 0) throw new Error("The CSV file is empty.");
  return { headers: all[0], rows: all.slice(1) };
}

/* ---------------------------------------------------------------- fuzzy map */
/** Loose label→field matching so most Excel headers map without clicks. */
function autoMap(fileColumns: string[], fields: ImportSchemaField[]): Record<string, string> {
  const map: Record<string, string> = {};
  for (const col of fileColumns) {
    const lc = col.toLowerCase();
    const score = (key: string, label: string) => {
      const lk = key.toLowerCase();
      const ll = label.toLowerCase();
      if (lk === lc || ll === lc) return 3;
      if (lk.includes(lc) || lc.includes(lk) || ll.includes(lc) || lc.includes(ll)) return 2;
      const abbr = lk.replace(/[^a-z0-9]/g, "");
      const lab = ll.replace(/[^a-z0-9]/g, "");
      if (abbr && (lab.includes(abbr) || abbr.includes(lab))) return 1;
      return 0;
    };
    let bestKey = "";
    let bestScore = 0;
    for (const f of fields) {
      const s = score(f.key, f.label);
      if (s > bestScore) { bestScore = s; bestKey = f.key; }
    }
    if (bestScore > 0) map[col] = bestKey;
    else map[col] = "__skip__";
  }
  return map;
}

const SKIP = "__skip__";
const USE_REAL_API = import.meta.env.VITE_USE_REAL_API === "true";

/* ---------------------------------------------------------------- demo data */
/** Demo-mode schemas keep the offline preview flow usable. */
const DEMO_SCHEMAS: Record<string, ImportSchema> = {
  workers: {
    typeKey: "workers",
    displayName: "Employees",
    fields: [
      { key: "employeeNo", label: "Employee number", required: false, naturalKey: true, example: "EMP-0011", formatNote: "Auto-generated when left blank" },
      { key: "firstName", label: "First name", required: true },
      { key: "lastName", label: "Last name", required: true },
      { key: "middleName", label: "Middle name", required: false },
      { key: "email", label: "Email", required: false, formatNote: "name@company.co.zm" },
      { key: "phone", label: "Phone", required: false, formatNote: "+260 97 … or 097…" },
      { key: "nrc", label: "NRC number", required: false, formatNote: "NNNNNN/NN/L" },
      { key: "tpin", label: "TPIN", required: false, formatNote: "10 digits" },
      { key: "napsaNumber", label: "NAPSA number", required: false, formatNote: "12 digits" },
      { key: "nhimaNumber", label: "NHIMA number", required: false, formatNote: "12 digits" },
      { key: "grade", label: "Grade", required: false },
      { key: "jobTitle", label: "Job title", required: false },
      { key: "startDate", label: "Start date", required: false, formatNote: "YYYY-MM-DD" },
      { key: "workerType", label: "Worker type", required: false, example: "employee" },
      { key: "orgUnitName", label: "Department", required: false },
    ],
  },
  attendance: {
    typeKey: "attendance",
    displayName: "Attendance",
    fields: [
      { key: "employeeNo", label: "Employee number", required: true, naturalKey: true, example: "EMP-0005" },
      { key: "workDate", label: "Date", required: true, formatNote: "YYYY-MM-DD" },
      { key: "clockIn", label: "Clock in", required: false, formatNote: "HH:mm" },
      { key: "clockOut", label: "Clock out", required: false, formatNote: "HH:mm" },
      { key: "note", label: "Note", required: false },
    ],
  },
};

function demoPreview(rows: Array<Record<string, string>>) {
  return {
    id: "demo-preview",
    typeKey: "workers",
    fileName: "uploaded-sheet",
    mode: "insert",
    totalRows: rows.length,
    willCreate: rows.filter((r) => r.firstName && r.lastName).length,
    willUpdate: 0, willSkip: 0,
    willError: rows.filter((r) => !(r.firstName && r.lastName)).length,
    rows: rows.map((r, i) => ({
      row: i + 2,
      status: r.firstName && r.lastName ? "create" : "error",
      message: r.firstName && r.lastName ? null : "First name and last name are required",
      resolved: r,
    })),
  };
}

/* ----------------------------------------------------------------- dialog */
export function ImportDialog({ typeKey, onDone, demoSample, presentation = "dialog" }: ImportDialogProps) {
  const [open, setOpen] = useState(false);
  const [fileColumns, setFileColumns] = useState<FileColumn[]>([]);
  const [fileRows, setFileRows] = useState<string[][]>([]);
  const [fileName, setFileName] = useState("");
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<Record<string, unknown> | null>(null);
  const [step, setStep] = useState<"upload" | "map" | "preview">("upload");
  const [mode, setMode] = useState<"insert" | "update">("insert");
  const [busy, setBusy] = useState(false);
  const [sheetName, setSheetName] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const [schemas, setSchemas] = useState<ImportSchema[] | null>(null);
  const [schemaError, setSchemaError] = useState<string | null>(null);
  const embedded = presentation === "embedded";
  const loadSchemas = async () => {
    try {
      const s = await realApi.importSchemas();
      setSchemas(s);
      setSchemaError(null);
    } catch (error) {
      setSchemas(USE_REAL_API ? [] : null);
      setSchemaError(USE_REAL_API ? (error instanceof Error ? error.message : "Import schema service is unavailable") : null);
    }
  };

  const schema = useMemo<ImportSchema | null>(() => {
    if (!schemas) return USE_REAL_API ? null : DEMO_SCHEMAS[typeKey] ?? null;
    return schemas.find((s) => s.typeKey === typeKey) ?? (USE_REAL_API ? null : DEMO_SCHEMAS[typeKey] ?? null);
  }, [schemas, typeKey]);

  async function openDialog() {
    setOpen(true);
    setStep("upload");
    setFileColumns([]);
    setFileRows([]);
    setPreview(null);
    setMapping({});
    setSchemaError(null);
    void loadSchemas();
  }

  useEffect(() => {
    if (!embedded) return;
    setStep("upload");
    setFileColumns([]);
    setFileRows([]);
    setPreview(null);
    setMapping({});
    setSchemaError(null);
    void loadSchemas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [embedded, typeKey]);

  async function handleFile(file: File) {
    try {
      const { headers, rows } = await readFileRows(file);
      const headersClean = headers.map((h) => (h ?? "").trim());
      if (headersClean.length === 0) throw new Error("No header row found — the first row must name the columns.");
      setFileName(file.name);
      setFileColumns(headersClean.map((name) => ({ name,       sample: String(rows[0]?.[headersClean.indexOf(name)] ?? "") })));
      setFileRows(rows);
      if (!schema) {
        if (USE_REAL_API) {
          toast.error("This import type is not available because the live import schema could not be loaded.");
          return;
        }
        const demo = DEMO_SCHEMAS[typeKey] || DEMO_SCHEMAS.workers;
        setMapping(autoMap(headersClean, demo.fields));
      } else {
        setMapping(autoMap(headersClean, schema.fields));
      }
      setStep("map");
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not read the file");
    }
  }

  /** Server-mapped rows keyed by field key, in file order. */
  const mappedRows = useMemo(() => {
    if (fileColumns.length === 0 || fileRows.length === 0) return [];
    return fileRows.map((row) => {
      const out: Record<string, string> = {};
      fileColumns.forEach((col, ci) => {
        const key = mapping[col.name];
        if (!key || key === SKIP) return;
        out[key] = (row[ci] ?? "").trim();
      });
      return out;
    });
  }, [fileColumns, fileRows, mapping]);

  async function runPreview() {
    if (!schema) {
      toast.error("The live import schema is unavailable. Nothing has been written.");
      return;
    }
    setBusy(true);
    try {
      if (!schemas) {
        setPreview(demoPreview(mappedRows.slice(0, 200)));
      } else {
        const rows = mappedRows.slice(0, 5000).map((r) =>
          Object.fromEntries(Object.entries(r).map(([k, v]) => [k, String(v ?? "")])) as Record<string, string>);
        const p = await realApi.importPreview(typeKey, fileName || "upload", mode, rows);
        setPreview(p);
      }
      setStep("preview");
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Preview failed");
    } finally {
      setBusy(false);
    }
  }

  function previewRowStatus(r: Record<string, unknown>): "create" | "update" | "skip" | "error" {
    const s = String(r.status ?? "error");
    return s === "create" || s === "update" || s === "skip" ? s : "error";
  }

  async function applyAccepted() {
    if (!preview || !schema) return;
    const rows = preview.rows as Array<Record<string, unknown>>;
    const idxs = rows
      .map((r, i) => (previewRowStatus(r) === "create" || previewRowStatus(r) === "update") ? i : -1)
      .filter((i) => i >= 0);
    if (idxs.length === 0) { toast.info("No rows are ready to import."); return; }
    setBusy(true);
    try {
      const res = await realApi.importApply(typeKey, String(preview.id), idxs);
      const counts = res as { created?: number; updated?: number; skipped?: number; rowOutcomes?: Array<{ row: number; status: string }> };
      toast.success(`Import done — ${counts.created ?? 0} created, ${counts.updated ?? 0} updated${counts.skipped ? `, ${counts.skipped} skipped` : ""}`);
      if (embedded) {
        setStep("upload");
        setFileColumns([]);
        setFileRows([]);
        setPreview(null);
        setMapping({});
        setFileName("");
      } else {
        setOpen(false);
      }
      onDone?.();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Apply failed");
    } finally {
      setBusy(false);
    }
  }

  const accepted = preview
    ? (preview.rows as Array<Record<string, unknown>>).filter((r) => previewRowStatus(r) !== "error" && previewRowStatus(r) !== "skip").length
    : 0;

  const statusIcon = (status: string) =>
    status === "create" ? <CircleCheck className="h-4 w-4 text-emerald-600" />
      : status === "update" ? <Pen className="h-4 w-4 text-sky-600" />
        : status === "skip" ? <CircleMinus className="h-4 w-4 text-zinc-400" />
          : <CircleAlert className="h-4 w-4 text-red-600" />;

  const previewRows = preview?.rows as Array<Record<string, unknown>> | undefined;
  const previewRowFileIndex = (row: Record<string, unknown>, fallbackIndex: number) => {
    const rowNumber = Number(row.row);
    return Number.isFinite(rowNumber) && rowNumber >= 2 ? rowNumber - 2 : fallbackIndex;
  };

  const workflow = (
    <>
      {embedded ? (
        <div className="mb-5 border-b pb-4">
          <h2 className="flex items-center gap-2 text-lg font-semibold">
            <FileSpreadsheet className="h-5 w-5 text-primary" />
            Import {schema?.displayName ?? typeKey}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {step === "upload" && "Drop an Excel or CSV file from your records. Every column can be mapped to a system field next."}
            {step === "map" && "Match each column in your file to a system field. Fields you skip are left blank."}
            {step === "preview" && "The server checked every row. Review the preview, then confirm to import."}
          </p>
        </div>
      ) : (
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileSpreadsheet className="h-4 w-4" />
            Import {schema?.displayName ?? typeKey}
          </DialogTitle>
          <DialogDescription>
            {step === "upload" && "Drop an Excel or CSV file from your records. Every column can be mapped to a system field next."}
            {step === "map" && "Match each column in your file to a system field. Fields you skip are left blank."}
            {step === "preview" && "The server checked every row. Review the preview, then confirm to import."}
          </DialogDescription>
        </DialogHeader>
      )}

      <ScrollArea className={embedded ? "" : "flex-1 min-h-0"}>
        {step === "upload" && (
          <div className="space-y-4 p-1">
            <div
              className="border-2 border-dashed rounded-lg p-10 text-center cursor-pointer hover:border-primary/60 transition-colors"
              onClick={() => inputRef.current?.click()}
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault();
                const f = e.dataTransfer.files[0];
                if (f) void handleFile(f);
              }}
            >
              <ArrowUpFromLine className="h-8 w-8 mx-auto text-muted-foreground" />
              <p className="mt-2 font-medium">Drag your file here, or click to choose</p>
              <p className="text-sm text-muted-foreground">.xlsx, .xls or .csv — the first row should name the columns</p>
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="w-full justify-start gap-2"
              onClick={async () => {
                // Download a blank template (CSV) for the mapped type.
                try {
                  const blob = await realApi.importExportBlob(typeKey, "__template__");
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement("a");
                  a.href = url;
                  a.download = `${typeKey}-template.csv`;
                  a.click();
                  URL.revokeObjectURL(url);
                } catch {
                  toast.error("Template is not available yet — start from your own spreadsheet columns.");
                }
              }}
            >
              <Download className="h-4 w-4" /> Download blank import template
            </Button>
            {schemaError && USE_REAL_API && <div role="alert" className="rounded-lg border border-danger/30 bg-danger-soft/30 p-3 text-sm text-danger">{schemaError}. Try again when the import service is available; no demo schema will be used.</div>}
            {!schema && USE_REAL_API && !schemaError && <div role="status" className="rounded-lg border border-warning/40 bg-warning-soft/30 p-3 text-sm text-warning-foreground">Loading the live import schema…</div>}
            {busy && <Progress value={undefined} className="h-1" />}
          </div>
        )}

        {step === "map" && schema && (
          <div className="space-y-3 p-1">
            <div className="flex items-center justify-between gap-2 flex-wrap">
              <p className="text-sm text-muted-foreground">
                <span className="font-medium text-foreground">{fileName}</span>
                {sheetName ? ` · sheet “${sheetName}”` : ""} · {fileRows.length} rows
              </p>
              <div className="flex items-center gap-2">
                <Label className="text-xs">Mode</Label>
                <Button
                  variant="outline" size="sm"
                  className={cn("h-7 text-xs", mode === "insert" ? "bg-accent" : "")}
                  onClick={() => setMode("insert")}
                >
                  Insert only
                </Button>
                <Button
                  variant="outline" size="sm"
                  className={cn("h-7 text-xs", mode === "update" ? "bg-accent" : "")}
                  onClick={() => setMode("update")}
                >
                  Match & update
                </Button>
              </div>
            </div>
            <div className="border rounded-lg">
              <div className="grid grid-cols-[1fr_1fr_1fr] gap-3 px-3 py-2 bg-muted/60 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                <span>Column in your file</span>
                <span>System field</span>
                <span>Sample value</span>
              </div>
              {fileColumns.map((col) => (
                <div key={col.name} className="grid grid-cols-[1fr_1fr_1fr] gap-3 px-3 py-2 items-center border-t">
                  <span className="text-sm truncate" title={col.name}>{col.name}</span>
                  <FieldMapper
                    fields={schema.fields}
                    value={mapping[col.name] ?? SKIP}
                    onChange={(k) => setMapping((m) => ({ ...m, [col.name]: k }))}
                  />
                  <span className="text-xs text-muted-foreground truncate" title={col.sample}>{col.sample}</span>
                </div>
              ))}
            </div>
            <div className="flex items-center justify-between pt-1">
              <p className="text-xs text-muted-foreground">
                {Object.values(mapping).filter((v) => v && v !== SKIP).length} of {fileColumns.length} columns mapped
              </p>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" onClick={() => setStep("upload")}>Back</Button>
                <Button size="sm" onClick={() => void runPreview()} disabled={busy}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
                  Preview
                </Button>
              </div>
            </div>
          </div>
        )}

        {step === "preview" && preview && schema && (
          <div className="space-y-3 p-1">
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                { label: "Total rows", value: String(preview.totalRows ?? 0), cls: "" },
                { label: "Will create", value: String(preview.willCreate ?? 0), cls: "text-emerald-600" },
                { label: "Will update", value: String(preview.willUpdate ?? 0), cls: "text-sky-600" },
                { label: "Problems", value: String(preview.willError ?? 0), cls: "text-red-600" },
              ].map((c) => (
                <div key={c.label} className="border rounded-lg p-3">
                  <p className="text-xs text-muted-foreground">{c.label}</p>
                  <p className={cn("text-xl font-semibold", c.cls)}>{c.value}</p>
                </div>
              ))}
            </div>
            <div className="overflow-hidden rounded-lg border">
              <div className="max-h-[28rem] overflow-auto">
                <table className="w-full min-w-max border-collapse text-left text-sm">
                  <thead className="sticky top-0 z-10 bg-muted/80 text-xs font-medium uppercase tracking-wide text-muted-foreground backdrop-blur">
                    <tr>
                      <th className="w-16 whitespace-nowrap border-b px-3 py-2">Row</th>
                      <th className="w-36 whitespace-nowrap border-b px-3 py-2">Status</th>
                      <th className="min-w-56 whitespace-nowrap border-b px-3 py-2">Validation</th>
                      {fileColumns.map((col) => (
                        <th key={col.name} className="min-w-44 whitespace-nowrap border-b px-3 py-2" title={col.name}>
                          <span className="block max-w-56 truncate">{col.name}</span>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {(previewRows ?? []).map((r, index) => {
                      const fileIndex = previewRowFileIndex(r, index);
                      const sourceRow = fileRows[fileIndex] ?? [];
                      return (
                        <tr key={`${String(r.row)}-${index}`} className="border-b last:border-b-0 hover:bg-surface-muted/50">
                          <td className="whitespace-nowrap px-3 py-2 text-xs text-muted-foreground">{String(r.row ?? fileIndex + 2)}</td>
                          <td className="whitespace-nowrap px-3 py-2">
                            <span className="inline-flex items-center gap-1.5">
                              {statusIcon(previewRowStatus(r))}
                              <span className="capitalize">{previewRowStatus(r)}</span>
                            </span>
                          </td>
                          <td className="max-w-80 px-3 py-2 text-xs text-muted-foreground" title={r.message ? String(r.message) : undefined}>
                            <span className="line-clamp-2">{r.message ? String(r.message) : "Ready"}</span>
                          </td>
                          {fileColumns.map((col, colIndex) => (
                            <td key={`${col.name}-${colIndex}`} className="max-w-64 px-3 py-2 text-sm" title={sourceRow[colIndex] ?? ""}>
                              <span className="block truncate">{sourceRow[colIndex] || "—"}</span>
                            </td>
                          ))}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
            <div className="flex items-center justify-between pt-1">
              <Button variant="ghost" size="sm" onClick={() => setStep("map")}>Change mapping</Button>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={busy} onClick={() => {
                  if (embedded) setStep("upload");
                  else setOpen(false);
                }}>
                  Cancel
                </Button>
                <Button size="sm" onClick={() => void applyAccepted()} disabled={busy || accepted === 0}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
                  Import {accepted} rows
                </Button>
              </div>
            </div>
          </div>
        )}
      </ScrollArea>
    </>
  );

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept=".csv,.xlsx,.xls"
        className="hidden"
        onChange={(e) => {
          const f = e.target.files?.[0];
          e.target.value = "";
          if (f) void handleFile(f);
        }}
      />
      {embedded ? (
        <div className="rounded-xl border bg-card p-5 shadow-sm">{workflow}</div>
      ) : (
        <>
          <Button variant="outline" onClick={() => void openDialog()} className="gap-2">
            <FileSpreadsheet className="h-4 w-4" /> Import
          </Button>
          <Dialog open={open} onOpenChange={(o) => { if (!o) setOpen(false); }}>
            <DialogContent className="max-w-4xl max-h-[85vh] overflow-hidden flex flex-col">
              {workflow}
            </DialogContent>
          </Dialog>
        </>
      )}
    </>
  );
}

/** Searchable dropdown mapping one file column to a target field or Skip. */
function FieldMapper({ fields, value, onChange }: {
  fields: ImportSchemaField[];
  value: string;
  onChange: (key: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const chosen = value === SKIP ? null : fields.find((f) => f.key === value);
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className={cn("w-full justify-between h-8 text-sm font-normal", !chosen && "text-muted-foreground")}
        >
          {chosen ? (
            <span className="flex items-center gap-1.5 truncate">
              {chosen.label}
              {chosen.required && <Badge variant="outline" className="h-4 px-1 text-[9px]">required</Badge>}
            </span>
          ) : (
            <span className="flex items-center gap-1.5">
              <X className="h-3 w-3" /> Skip this column
            </span>
          )}
          <ChevronDown className="h-3 w-3 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-0" align="start">
        <Command>
          <CommandInput placeholder="Search fields…" className="h-9" />
          <CommandList>
            <CommandEmpty>No fields found.</CommandEmpty>
            <CommandGroup>
              <CommandItem
                value="__skip__"
                onSelect={() => { onChange(SKIP); setOpen(false); }}
              >
                <X className="h-3 w-3 mr-2 text-muted-foreground" /> Skip this column
              </CommandItem>
              {fields.map((f) => (
                <CommandItem
                  key={f.key}
                  value={`${f.label} ${f.key}`}
                  onSelect={() => { onChange(f.key); setOpen(false); }}
                  className="flex items-center gap-1.5"
                >
                  <span>{f.label}</span>
                  {f.naturalKey && <Badge variant="outline" className="h-4 px-1 text-[9px] ml-auto">match</Badge>}
                  {f.required && <Badge variant="outline" className="h-4 px-1 text-[9px] ml-auto">req</Badge>}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
