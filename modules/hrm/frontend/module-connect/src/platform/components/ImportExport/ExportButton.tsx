import { useState } from "react";
import { Download, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { realApi } from "@/platform/use-api";

export interface ExportButtonProps {
  typeKey: string;
  fileName: string;
  filter?: string;
  label?: string;
  className?: string;
}

/** Shared server-generated export control for any registered import/export type. */
export function ExportButton({
  typeKey,
  fileName,
  filter,
  label = "Export",
  className,
}: ExportButtonProps) {
  const [busy, setBusy] = useState(false);

  async function handleExport(format: "csv" | "xlsx") {
    setBusy(true);
    try {
      const query = [filter, format === "xlsx" ? "format=xlsx" : ""]
        .filter(Boolean)
        .join(filter?.includes("?") ? "&" : "&");
      const blob = await realApi.importExportBlob(typeKey, query || undefined);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${fileName}-${new Date().toISOString().slice(0, 10)}.${format}`;
      anchor.click();
      URL.revokeObjectURL(url);
      toast.success(`${fileName} ${format.toUpperCase()} export downloaded.`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Export is not available yet.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" className={`gap-2 ${className ?? ""}`} disabled={busy}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Download className="size-4" aria-hidden />}
          {label}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => void handleExport("csv")}>Export as CSV</DropdownMenuItem>
        <DropdownMenuItem onClick={() => void handleExport("xlsx")}>Export as Excel</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
