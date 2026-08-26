/**
 * M31 — shared Import/Export tool for every CRUD list page.
 *
 * The org arrives with Excel sheets as their only system of record, so this
 * dialog is the front door for migrating spreadsheets in:
 *   1. Drop a .csv / .xlsx file (reads both client-side; XLSX via SheetJS)
 *   2. Map each system field to one of the spreadsheet's real column titles
 *      (unmapped fields stay blank)
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
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Progress } from "@/components/ui/progress";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Check, Download, FileSpreadsheet, Loader2, Search,
  ArrowUpFromLine, CircleCheck, CircleAlert, CircleMinus, Pen, Plus, Trash2,
  ChevronLeft, ChevronRight, X,
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

async function readFileRows(file: File): Promise<{ headers: string[]; rows: string[][]; sheetName?: string }> {
  const buf = await file.arrayBuffer();
  if (/\.xlsx?$/i.test(file.name)) {
    const wb = XLSX.read(buf, { type: "array" });
    const sheetName = wb.SheetNames[0];
    const ws = wb.Sheets[sheetName];
    const raw: unknown[][] = XLSX.utils.sheet_to_json(ws, { header: 1, defval: "" });
    const rows = raw.filter((r) => r.some((c) => String(c ?? "").trim() !== "")) as string[][];
    if (rows.length === 0) throw new Error("The workbook has no data rows.");
    return { headers: rows[0].map(String), rows: rows.slice(1).map((r) => r.map(String)), sheetName };
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
const IMPORT_PAGE_SIZE = 25;

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

function pageCount(totalRows: number) {
  return Math.max(1, Math.ceil(totalRows / IMPORT_PAGE_SIZE));
}

function clampPage(page: number, totalRows: number) {
  return Math.min(Math.max(page, 1), pageCount(totalRows));
}

function PageControls({
  page,
  totalRows,
  onPageChange,
}: {
  page: number;
  totalRows: number;
  onPageChange: (page: number) => void;
}) {
  const totalPages = pageCount(totalRows);
  const start = totalRows === 0 ? 0 : (page - 1) * IMPORT_PAGE_SIZE + 1;
  const end = Math.min(page * IMPORT_PAGE_SIZE, totalRows);
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-t px-3 py-2 text-xs text-muted-foreground">
      <span>
        Showing {start}-{end} of {totalRows} rows
      </span>
      <div className="flex items-center gap-1">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          aria-label="Previous import rows page"
        >
          <ChevronLeft className="size-4" aria-hidden />
        </Button>
        <span className="min-w-20 text-center tabular-nums">
          Page {page} / {totalPages}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          aria-label="Next import rows page"
        >
          <ChevronRight className="size-4" aria-hidden />
        </Button>
      </div>
    </div>
  );
}

/* ----------------------------------------------------------------- dialog */
export function ImportDialog({ typeKey, onDone, demoSample, presentation = "dialog" }: ImportDialogProps) {
  const [open, setOpen] = useState(false);
  const [fileColumns, setFileColumns] = useState<FileColumn[]>([]);
  const [fileRows, setFileRows] = useState<string[][]>([]);
  const [fileName, setFileName] = useState("");
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [manualRows, setManualRows] = useState<Array<Record<string, string>>>([]);
  const [preview, setPreview] = useState<Record<string, unknown> | null>(null);
  const [step, setStep] = useState<"upload" | "map" | "preview">("upload");
  const [entryMode, setEntryMode] = useState<"upload" | "manual">("upload");
  const [mode, setMode] = useState<"insert" | "update">("insert");
  const [manualPage, setManualPage] = useState(1);
  const [mapPage, setMapPage] = useState(1);
  const [previewPage, setPreviewPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [sheetName, setSheetName] = useState<string | null>(null);
  const [pasteError, setPasteError] = useState<string | null>(null);
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
    setManualRows([]);
    setEntryMode("upload");
    setManualPage(1);
    setMapPage(1);
    setPreviewPage(1);
    setSchemaError(null);
    setPasteError(null);
    void loadSchemas();
  }

  useEffect(() => {
    if (!embedded) return;
    setStep("upload");
    setFileColumns([]);
    setFileRows([]);
    setPreview(null);
    setMapping({});
    setManualRows([]);
    setEntryMode("upload");
    setManualPage(1);
    setMapPage(1);
    setPreviewPage(1);
    setSchemaError(null);
    setPasteError(null);
    void loadSchemas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [embedded, typeKey]);

  function loadParsedSheet(headers: string[], rows: string[][], sourceName: string, parsedSheetName?: string) {
    const headersClean = headers.map((h, index) => (h ?? "").trim() || `Column ${index + 1}`);
    if (headersClean.length === 0) throw new Error("No header row found — the first row must name the columns.");
    if (rows.length === 0) throw new Error("No readable data rows found below the header.");
    setFileName(sourceName);
    setSheetName(parsedSheetName ?? null);
    setFileColumns(headersClean.map((name, index) => ({ name, sample: String(rows[0]?.[index] ?? "") })));
    setFileRows(rows);
    setPreview(null);
    setPasteError(null);
    setManualPage(1);
    setMapPage(1);
    setPreviewPage(1);
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
  }

  function clearSheet() {
    setFileColumns([]);
    setFileRows([]);
    setFileName("");
    setSheetName(null);
    setMapping({});
    setPreview(null);
    setPasteError(null);
    setEntryMode("upload");
    setManualPage(1);
    setMapPage(1);
    setPreviewPage(1);
    setStep("upload");
    if (inputRef.current) inputRef.current.value = "";
  }

  async function handleFile(file: File) {
    try {
      const { headers, rows, sheetName: parsedSheetName } = await readFileRows(file);
      loadParsedSheet(headers, rows, file.name, parsedSheetName);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not read the file");
    }
  }

  function handlePastedText(text: string) {
    setPasteError(null);
    const trimmed = text.trim();
    if (!trimmed) return;
    try {
      const all = parseCsvText(trimmed.startsWith("\uFEFF") ? trimmed.slice(1) : trimmed);
      if (all.length < 2) throw new Error("The spreadsheet must have a header row plus at least one data row.");
      loadParsedSheet(all[0], all.slice(1), "pasted-spreadsheet.csv");
    } catch (e) {
      setPasteError(e instanceof Error ? e.message : "Could not read the pasted spreadsheet data.");
    }
  }

  /** Server-mapped rows keyed by field key, in file order. */
  const mappedRows = useMemo(() => {
    if (entryMode === "manual") {
      return manualRows
        .filter((row) => Object.values(row).some((value) => String(value ?? "").trim().length > 0))
        .map((row) =>
          Object.fromEntries(Object.entries(row).map(([key, value]) => [key, String(value ?? "").trim()])) as Record<string, string>
        );
    }
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
  }, [entryMode, fileColumns, fileRows, manualRows, mapping]);

  function switchEntryMode(next: "upload" | "manual") {
    setEntryMode(next);
    setPreview(null);
    setManualPage(1);
    if (next === "manual" && manualRows.length === 0) {
      const seeded = mappedRows;
      setManualRows(seeded.length > 0 ? seeded : [{}]);
    }
  }

  function showUploadEntry() {
    setEntryMode("upload");
    setPreview(null);
    setStep(fileRows.length > 0 ? "map" : "upload");
  }

  function showManualEntry() {
    switchEntryMode("manual");
    setStep("upload");
  }

  function updateManualRow(index: number, fieldKey: string, value: string) {
    setManualRows((rows) => rows.map((row, rowIndex) => (rowIndex === index ? { ...row, [fieldKey]: value } : row)));
  }

  function editMappedRowsManually() {
    if (manualRows.length === 0) {
      const seeded = mappedRows;
      setManualRows(seeded.length > 0 ? seeded : [{}]);
    }
    setEntryMode("manual");
    setPreview(null);
    setManualPage(1);
    setPreviewPage(1);
    setStep("upload");
  }

  async function runPreview() {
    if (!schema) {
      toast.error("The live import schema is unavailable. Nothing has been written.");
      return;
    }
    setBusy(true);
    try {
      if (!schemas) {
        setPreview(demoPreview(mappedRows));
      } else {
        const rows = mappedRows.map((r) =>
          Object.fromEntries(Object.entries(r).map(([k, v]) => [k, String(v ?? "")])) as Record<string, string>);
        const p = await realApi.importPreview(typeKey, fileName || "upload", mode, rows);
        setPreview(p);
      }
      setPreviewPage(1);
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
        setPreviewPage(1);
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
  const selectedColumnForField = (fieldKey: string) =>
    fileColumns.find((col) => mapping[col.name] === fieldKey)?.name ?? SKIP;
  const setFieldColumn = (fieldKey: string, columnName: string) => {
    setMapping((current) => {
      const next = { ...current };
      for (const [colName, mappedField] of Object.entries(next)) {
        if (mappedField === fieldKey) next[colName] = SKIP;
      }
      if (columnName !== SKIP) next[columnName] = fieldKey;
      return next;
    });
  };
  const requiredMapped = schema?.fields
    .filter((field) => field.required)
    .every((field) => entryMode === "manual" || selectedColumnForField(field.key) !== SKIP) ?? false;
  const mappedPreviewFields = schema?.fields ?? [];
  const filledManualRows = manualRows.filter((row) => Object.values(row).some((value) => String(value ?? "").trim().length > 0));
  const manualValid = schema ? filledManualRows.length > 0 && filledManualRows.every((row) =>
    schema.fields
      .filter((field) => field.required)
      .every((field) => String(row[field.key] ?? "").trim().length > 0)
  ) : false;
  const canPreview = entryMode === "manual" ? manualValid : requiredMapped;
  const visibleManualRows = (manualRows.length ? manualRows : [{}]).slice((manualPage - 1) * IMPORT_PAGE_SIZE, manualPage * IMPORT_PAGE_SIZE);
  const visibleMappedRows = mappedRows.slice((mapPage - 1) * IMPORT_PAGE_SIZE, mapPage * IMPORT_PAGE_SIZE);
  const visiblePreviewRows = (previewRows ?? []).slice((previewPage - 1) * IMPORT_PAGE_SIZE, previewPage * IMPORT_PAGE_SIZE);

  useEffect(() => {
    setManualPage((page) => clampPage(page, manualRows.length || 1));
  }, [manualRows.length]);

  useEffect(() => {
    setMapPage((page) => clampPage(page, mappedRows.length || 1));
  }, [mappedRows.length]);

  useEffect(() => {
    setPreviewPage((page) => clampPage(page, previewRows?.length ?? 0));
  }, [previewRows?.length]);

  const modeTabs = step !== "preview" ? (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-surface-muted/40 p-2">
      <div className="flex flex-wrap gap-2 text-sm">
        <button
          type="button"
          className={cn("rounded-md px-3 py-1.5", entryMode === "upload" ? "bg-primary text-primary-foreground" : "bg-muted")}
          onClick={showUploadEntry}
        >
          Upload spreadsheet
        </button>
        <button
          type="button"
          className={cn("rounded-md px-3 py-1.5", entryMode === "manual" && !fileRows.length ? "bg-primary text-primary-foreground" : "bg-muted")}
          onClick={showManualEntry}
        >
          Enter manually
        </button>
        {fileRows.length > 0 ? (
          <button
            type="button"
            className={cn("rounded-md px-3 py-1.5", entryMode === "manual" && fileRows.length ? "bg-primary text-primary-foreground" : "bg-muted")}
            onClick={editMappedRowsManually}
          >
            Edit imported rows
          </button>
        ) : null}
      </div>
      {fileRows.length > 0 ? (
        <Button type="button" variant="ghost" size="sm" className="gap-2 text-muted-foreground" onClick={clearSheet}>
          <X className="size-4" aria-hidden />
          Remove spreadsheet
        </Button>
      ) : null}
    </div>
  ) : null;

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
        {modeTabs ? <div className="p-1 pb-3">{modeTabs}</div> : null}
        {step === "upload" && (
          <div className="space-y-4 p-1">
            {entryMode === "manual" && schema ? (
              <div className="space-y-3">
                <div className="max-w-full overflow-x-auto rounded-md border pb-2">
                  <table className="w-full min-w-[1600px] text-xs">
                    <thead className="bg-muted/50">
                      <tr>
                        {schema.fields.map((field) => (
                          <th key={field.key} className="min-w-40 px-2 py-1.5 text-left font-medium whitespace-nowrap">
                            {field.label}{field.required && <span className="ml-0.5 text-destructive">*</span>}
                          </th>
                        ))}
                        <th className="w-12 px-2 py-1.5" aria-label="Actions" />
                      </tr>
                    </thead>
                    <tbody>
                      {visibleManualRows.map((row, visibleRowIndex) => {
                        const rowIndex = (manualPage - 1) * IMPORT_PAGE_SIZE + visibleRowIndex;
                        return (
                        <tr key={rowIndex} className="border-t">
                          {schema.fields.map((field) => (
                            <td key={field.key} className="px-2 py-1.5">
                              <Input
                                value={row[field.key] ?? ""}
                                onChange={(event) => updateManualRow(rowIndex, field.key, event.target.value)}
                                placeholder={field.example || field.formatNote || field.label}
                                className="h-8 min-w-40 text-xs"
                              />
                            </td>
                          ))}
                          <td className="px-2 py-1.5">
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              disabled={manualRows.length <= 1}
                              onClick={() => setManualRows((rows) => rows.filter((_, index) => index !== rowIndex))}
                              aria-label={`Remove row ${rowIndex + 1}`}
                            >
                              <Trash2 className="size-4 text-muted-foreground" aria-hidden />
                            </Button>
                          </td>
                        </tr>
                      );
                      })}
                    </tbody>
                  </table>
                  <PageControls
                    page={manualPage}
                    totalRows={manualRows.length || 1}
                    onPageChange={(page) => setManualPage(clampPage(page, manualRows.length || 1))}
                  />
                </div>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-xs text-muted-foreground">Enter data horizontally, the same way the setup wizard lets you review staff rows.</p>
                  <Button type="button" variant="outline" size="sm" onClick={() => setManualRows((rows) => [...(rows.length ? rows : [{}]), {}])}>
                    <Plus className="size-4" aria-hidden /> Add row
                  </Button>
                </div>
                <div className="flex justify-end gap-2">
                  <Button size="sm" onClick={() => void runPreview()} disabled={busy || !canPreview}>
                    {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
                    Preview
                  </Button>
                </div>
              </div>
            ) : (
              <>
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
            <div className="space-y-2">
              <p className="text-xs text-muted-foreground">
                Or paste spreadsheet contents below. Any column titles are allowed; you will map the useful columns before anything is imported.
              </p>
              <textarea
                rows={7}
                className="w-full rounded-md border bg-background px-3 py-2 font-mono text-xs shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder={"Employee No,First Name,Last Name,Email,Department\nEMP-001,Jane,Mwansa,jane@company.co.zm,Finance"}
                onChange={(event) => handlePastedText(event.target.value)}
                aria-label="Paste spreadsheet contents"
              />
              {pasteError && <p className="text-xs text-destructive">{pasteError}</p>}
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
              </>
            )}
          </div>
        )}

        {step === "map" && schema && (
          <div className="space-y-4 p-1">
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
            <div className="rounded-lg border p-4">
              <div className="mb-2 flex items-center justify-between gap-3">
                <span className="text-xs font-medium text-muted-foreground">
                  Columns found in your file ({fileColumns.length})
                </span>
                {!requiredMapped && (
                  <Badge variant="outline" className="border-warning/40 bg-warning-soft text-warning-foreground">
                    Required fields need mapping
                  </Badge>
                )}
              </div>
              <div className="flex flex-wrap gap-1.5">
                {fileColumns.map((col, index) => (
                  <Badge key={`${col.name}-${index}`} variant="outline" className="px-2 py-1 font-mono text-[11px]">
                    {col.name || `Column ${index + 1}`}
                  </Badge>
                ))}
              </div>
            </div>
            {fileRows.length > 0 && (
              <div className="space-y-2">
                <div className="text-xs font-medium text-muted-foreground">
                  Map and preview — rows {fileRows.length ? (mapPage - 1) * IMPORT_PAGE_SIZE + 1 : 0}-{Math.min(mapPage * IMPORT_PAGE_SIZE, mappedRows.length)} as the system will receive them
                </div>
                <div className="max-w-full overflow-x-auto rounded-lg border pb-2">
                  <table className="w-full min-w-[1600px] border-collapse text-left text-xs">
                    <thead className="bg-muted/50 text-muted-foreground">
                      <tr>
                        {mappedPreviewFields.map((field) => (
                          <th key={field.key} className="min-w-48 px-2 py-2 align-top font-medium">
                            <div className="space-y-1">
                              <div className="flex items-center gap-1 whitespace-nowrap">
                                <span>{field.label}</span>
                                {field.required && <span className="text-destructive">*</span>}
                                {field.naturalKey && <Badge variant="outline" className="h-5 px-1.5 text-[10px]">match</Badge>}
                              </div>
                              <Select value={selectedColumnForField(field.key)} onValueChange={(value) => setFieldColumn(field.key, value)}>
                                <SelectTrigger className="h-8 min-w-44 bg-background text-xs">
                                  <SelectValue />
                                </SelectTrigger>
                                <SelectContent position="popper" side="bottom" collisionPadding={8}>
                                  <SelectItem value={SKIP}>Ignore this field</SelectItem>
                                  {fileColumns.map((col, index) => (
                                    <SelectItem key={`${col.name}-${index}`} value={col.name}>
                                      {col.name || `Column ${index + 1}`}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </div>
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {visibleMappedRows.map((row, rowIndex) => (
                        <tr key={rowIndex} className="border-t">
                          {mappedPreviewFields.map((field) => (
                            <td key={field.key} className="max-w-56 px-3 py-2" title={row[field.key] ?? ""}>
                              <span className="block truncate">{row[field.key] || "—"}</span>
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <PageControls
                    page={mapPage}
                    totalRows={mappedRows.length}
                    onPageChange={(page) => setMapPage(clampPage(page, mappedRows.length))}
                  />
                </div>
              </div>
            )}
            <div className="flex items-center justify-between pt-1">
              <p className="text-xs text-muted-foreground">
                {Object.values(mapping).filter((v) => v && v !== SKIP).length} fields mapped
              </p>
              <div className="flex flex-wrap justify-end gap-2">
                <Button variant="ghost" size="sm" onClick={() => setStep("upload")}>Back</Button>
                <Button variant="outline" size="sm" onClick={editMappedRowsManually} disabled={!mappedRows.length}>
                  <Pen className="h-4 w-4" />
                  Manual edit rows
                </Button>
                <Button size="sm" onClick={() => void runPreview()} disabled={busy || !canPreview}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
                  Preview
                </Button>
              </div>
            </div>
          </div>
        )}

        {step === "preview" && preview && schema && (
          <div className="space-y-3 p-1">
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-surface-muted/40 p-3">
              <div>
                <p className="text-sm font-medium">Ready to finish import</p>
                <p className="text-xs text-muted-foreground">
                  {accepted} rows are ready to upload
                  {(preview.willError as number | undefined) ? ` · ${String(preview.willError)} rows need correction` : ""}
                </p>
              </div>
              <Button size="sm" onClick={() => void applyAccepted()} disabled={busy || accepted === 0}>
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
                Confirm import
              </Button>
            </div>
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
            <div className="max-w-full overflow-hidden rounded-lg border">
              <div className="max-h-[28rem] overflow-auto">
                <table className="w-full min-w-[1600px] border-collapse text-left text-sm">
                  <thead className="sticky top-0 z-10 bg-muted/80 text-xs font-medium uppercase tracking-wide text-muted-foreground backdrop-blur">
                    <tr>
                      <th className="w-16 whitespace-nowrap border-b px-3 py-2">Row</th>
                      <th className="w-36 whitespace-nowrap border-b px-3 py-2">Status</th>
                      <th className="min-w-56 whitespace-nowrap border-b px-3 py-2">Validation</th>
                      {(entryMode === "manual" ? schema.fields : fileColumns).map((col) => (
                        <th key={"key" in col ? col.key : col.name} className="min-w-44 whitespace-nowrap border-b px-3 py-2" title={"label" in col ? col.label : col.name}>
                          <span className="block max-w-56 truncate">{"label" in col ? col.label : col.name}</span>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {visiblePreviewRows.map((r, visibleIndex) => {
                      const index = (previewPage - 1) * IMPORT_PAGE_SIZE + visibleIndex;
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
                          {entryMode === "manual"
                            ? schema.fields.map((field) => {
                              const resolved = (r.resolved as Record<string, unknown> | undefined) ?? mappedRows[index] ?? {};
                              const value = String(resolved[field.key] ?? "");
                              return (
                                <td key={field.key} className="max-w-64 px-3 py-2 text-sm" title={value}>
                                  <span className="block truncate">{value || "—"}</span>
                                </td>
                              );
                            })
                            : fileColumns.map((col, colIndex) => (
                              <td key={`${col.name}-${colIndex}`} className="max-w-64 px-3 py-2 text-sm" title={sourceRow[colIndex] ?? ""}>
                                <span className="block truncate">{sourceRow[colIndex] || "—"}</span>
                              </td>
                            ))}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <PageControls
                  page={previewPage}
                  totalRows={previewRows?.length ?? 0}
                  onPageChange={(page) => setPreviewPage(clampPage(page, previewRows?.length ?? 0))}
                />
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
