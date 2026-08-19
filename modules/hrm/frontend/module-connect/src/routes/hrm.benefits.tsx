import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/benefits")({ component: Benefits });

type TypeRow = Record<string, unknown>;
type AllowanceRow = Record<string, unknown>;
type ClaimRow = Record<string, unknown>;
type EmployeeRow = Record<string, unknown>;

const STATUS_LABEL: Record<string, string> = {
  submitted: "Submitted",
  returned: "Returned",
  approved: "Approved",
  rejected: "Rejected",
  paid: "Paid",
};

const STATUS_CLASS: Record<string, string> = {
  submitted: "rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-700",
  returned: "rounded bg-sky-100 px-1.5 py-0.5 text-xs font-medium text-sky-700",
  approved: "rounded bg-emerald-100 px-1.5 py-0.5 text-xs font-medium text-emerald-700",
  rejected: "rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700",
  paid: "rounded bg-slate-700 px-1.5 py-0.5 text-xs font-medium text-white",
};

function emptyType(): TypeRow {
  return {
    code: "",
    name: "",
    description: "",
    annualCap: 0,
    requiresEvidence: false,
  };
}

function Benefits() {
  const types = useApi(realApi.benefitTypes, []);
  const allowances = useApi(realApi.benefitAllowances, []);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const claims = useApi(() => realApi.benefitClaims({ status: statusFilter || undefined, pageSize: 50 }), [statusFilter]);
  const employees = useApi(() => realApi.employees({ pageSize: 200 }), []);

  const [typeForm, setTypeForm] = useState<TypeRow>(emptyType());
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);

  // Per-worker allowance form
  const [allowanceWorker, setAllowanceWorker] = useState("");
  const [allowanceType, setAllowanceType] = useState("");
  const [allowanceYear, setAllowanceYear] = useState(String(new Date().getFullYear()));
  const [allowanceAmount, setAllowanceAmount] = useState("");

  // New claim form
  const [claimWorker, setClaimWorker] = useState("");
  const [claimType, setClaimType] = useState("");
  const [claimAmount, setClaimAmount] = useState("");
  const [claimCurrency, setClaimCurrency] = useState("ZMW");
  const [claimNote, setClaimNote] = useState("");
  const [claimEvidence, setClaimEvidence] = useState(false);

  const [busy, setBusy] = useState(false);
  const [approveAmount, setApproveAmount] = useState<Record<string, string>>({});
  const [decisionReason, setDecisionReason] = useState<Record<string, string>>({});

  const activeTypes = useMemo(
    () => ((types.data ?? []) as TypeRow[]).filter((row) => Boolean(row.isActive)),
    [types.data],
  );

  const employeeLabel = (row: EmployeeRow) => {
    const name = String(row.fullName ?? row.name ?? row.employeeName ?? "—");
    const no = row.employeeNo ? ` (${String(row.employeeNo)})` : "";
    return `${name}${no}`;
  };

  const employeeOptions = useMemo(
    () => ((employees.data?.items ?? []) as EmployeeRow[]).map((row) => ({ value: String(row.id), label: employeeLabel(row) })),
    [employees.data],
  );

  const run = async <T,>(name: string, operation: () => Promise<T>, onSuccess?: () => void) => {
    setBusy(true);
    try {
      await operation();
      toast.success(`${name} completed`);
      onSuccess?.();
      types.reload();
      allowances.reload();
      claims.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `${name} failed`);
    } finally {
      setBusy(false);
    }
  };

  const submitType = async () => {
    const annualCap = Number(typeForm.annualCap) || 0;
    if (!typeForm.code || !typeForm.name || annualCap <= 0) {
      toast.error("Code, name and a positive annual cap are required.");
      return;
    }
    if (editingTypeId) {
      await run(
        "Benefit type updated",
        () =>
          realApi.updateBenefitType(editingTypeId, {
            code: typeForm.code,
            name: typeForm.name,
            description: typeForm.description || null,
            annualCap,
            requiresEvidence: Boolean(typeForm.requiresEvidence),
            isActive: Boolean(typeForm.isActive),
          }),
      );
      setEditingTypeId(null);
    } else {
      await run(
        "Benefit type created",
        () =>
          realApi.createBenefitType({
            code: typeForm.code,
            name: typeForm.name,
            description: typeForm.description || null,
            annualCap,
            requiresEvidence: Boolean(typeForm.requiresEvidence),
          }),
      );
    }
    setTypeForm(emptyType());
  };

  const startEditType = (row: TypeRow) => {
    setEditingTypeId(String(row.id));
    setTypeForm(row);
  };

  const deleteType = async (id: string) => {
    await run("Benefit type deactivated", () =>
      realApi.updateBenefitType(id, {
        code: "",
        name: "",
        description: null,
        annualCap: 0,
        requiresEvidence: false,
        isActive: false,
      }),
    );
  };

  const submitAllowance = async () => {
    if (!allowanceWorker || !allowanceType || !allowanceAmount) {
      toast.error("Employee, benefit type and an amount are required.");
      return;
    }
    await run(
      "Allowance set",
      () =>
        realApi.setBenefitAllowance({
          workerId: allowanceWorker,
          benefitTypeCode: allowanceType,
          annualAmount: Number(allowanceAmount) || 0,
          year: Number(allowanceYear) || new Date().getFullYear(),
        }),
      () => {
        setAllowanceAmount("");
      },
    );
  };

  const submitClaim = async () => {
    if (!claimWorker || !claimType || !claimAmount) {
      toast.error("Employee, benefit type and an amount are required.");
      return;
    }
    const evidenceAttached = Boolean(claimEvidence) || (activeTypes.find((row) => row.code === claimType)?.requiresEvidence === true);
    await run(
      "Claim submitted",
      () =>
        realApi.createBenefitClaim({
          workerId: claimWorker,
          benefitTypeCode: claimType,
          amountClaimed: Number(claimAmount) || 0,
          currency: claimCurrency || "ZMW",
          note: claimNote || null,
          evidenceAttached,
        }),
      () => {
        setClaimAmount("");
        setClaimNote("");
        setClaimEvidence(false);
      },
    );
  };

  const decide = async (id: string, action: string) => {
    const approvedAmountRaw = approveAmount[id];
    const approvedAmount =
      action === "approve" && approvedAmountRaw ? Number(approvedAmountRaw) : undefined;
    await run(
      `Claim ${action === "approve" ? "approved" : action === "reject" ? "rejected" : "returned"}`,
      () =>
        realApi.decideBenefitClaim(id, {
          action,
          reason: decisionReason[id] || undefined,
          ...(Number.isFinite(Number(approvedAmount)) ? { approvedAmount } : {}),
        }),
    );
  };

  const pay = async (id: string) => {
    await run("Claim paid", () => realApi.payBenefitClaim(id));
  };

  const typeOptions = (rows: unknown[]) =>
    (rows as TypeRow[]).map((row) => (
      <SelectItem key={String(row.code)} value={String(row.code)}>
        {String(row.name)} · {String(row.code)}
      </SelectItem>
    ));

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave"
          title="Benefits"
          description="Flexible benefit types with annual caps, optional per-employee annual allowance overrides, and a claims inbox that approves, returns and pays each claim."
          meta={<ScopeBadge />}
        />
        <div className="grid gap-6 xl:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>Benefit types</CardTitle>
              <CardDescription>
                Create a claimable benefit with an annual cap. Tick “requires evidence” when receipts
                or proof must accompany each claim. Deactivating a type stops new allowances and claims
                without removing history.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <Label htmlFor="type-code">Code</Label>
                  <Input
                    id="type-code"
                    value={String(typeForm.code ?? "")}
                    onChange={(event) => setTypeForm({ ...typeForm, code: event.target.value })}
                    placeholder="e.g. medical"
                    disabled={editingTypeId !== null}
                  />
                </div>
                <div>
                  <Label htmlFor="type-name">Name</Label>
                  <Input
                    id="type-name"
                    value={String(typeForm.name ?? "")}
                    onChange={(event) => setTypeForm({ ...typeForm, name: event.target.value })}
                    placeholder="e.g. Medical reimbursement"
                  />
                </div>
                <div className="sm:col-span-2">
                  <Label htmlFor="type-description">Description (optional)</Label>
                  <Textarea
                    id="type-description"
                    value={String(typeForm.description ?? "")}
                    onChange={(event) =>
                      setTypeForm({ ...typeForm, description: event.target.value })
                    }
                    rows={2}
                  />
                </div>
                <div>
                  <Label htmlFor="type-cap">Annual cap</Label>
                  <Input
                    id="type-cap"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={String(typeForm.annualCap ?? "")}
                    onChange={(event) =>
                      setTypeForm({ ...typeForm, annualCap: Number(event.target.value) })
                    }
                  />
                </div>
                <div className="flex items-end gap-2 pb-1">
                  <Checkbox
                    id="type-evidence"
                    checked={Boolean(typeForm.requiresEvidence)}
                    onCheckedChange={(checked) =>
                      setTypeForm({ ...typeForm, requiresEvidence: Boolean(checked) })
                    }
                  />
                  <Label htmlFor="type-evidence" className="mb-0">
                    Requires evidence
                  </Label>
                </div>
                {editingTypeId ? (
                  <div className="flex items-end gap-2 pb-1">
                    <Checkbox
                      id="type-active"
                      checked={Boolean(typeForm.isActive)}
                      onCheckedChange={(checked) =>
                        setTypeForm({ ...typeForm, isActive: Boolean(checked) })
                      }
                    />
                    <Label htmlFor="type-active" className="mb-0">
                      Active
                    </Label>
                  </div>
                ) : null}
              </div>
              <div className="flex gap-2">
                {editingTypeId ? (
                  <Button variant="outline" onClick={() => { setEditingTypeId(null); setTypeForm(emptyType()); }}>
                    Cancel edit
                  </Button>
                ) : null}
                <Button onClick={submitType} disabled={busy}>
                  {editingTypeId ? "Save type" : "Create type"}
                </Button>
              </div>
              <div className="mt-1 text-sm font-medium">Existing types</div>
              <div className="space-y-2">
                {types.loading ? <p className="text-muted-foreground">Loading types…</p> : null}
                {types.error ? <p className="text-destructive text-sm">{types.error}</p> : null}
                {types.data ? (
                  (types.data as TypeRow[]).length ? (
                    (types.data as TypeRow[]).map((row) => (
                      <div
                        key={String(row.id)}
                        className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm"
                      >
                        <div>
                          <span className="font-medium">{String(row.name)}</span>{" "}
                          <span className="text-muted-foreground">({String(row.code)})</span>{" "}
                          <span className="text-muted-foreground">
                            · cap {String(row.annualCap)}
                            {Boolean(row.requiresEvidence) ? " · evidence" : ""}
                          </span>
                          <span
                            className={
                              row.isActive
                                ? "ml-2 rounded bg-emerald-100 px-1.5 py-0.5 text-xs font-medium text-emerald-700"
                                : "ml-2 rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700"
                            }
                          >
                            {row.isActive ? "active" : "inactive"}
                          </span>
                        </div>
                        <div className="flex gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs"
                            onClick={() => startEditType(row)}
                            disabled={busy}
                          >
                            Edit
                          </Button>
                          {row.isActive ? (
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-7 text-xs text-destructive"
                              onClick={() => deleteType(String(row.id))}
                              disabled={busy}
                            >
                              Deactivate
                            </Button>
                          ) : null}
                        </div>
                      </div>
                    ))
                  ) : (
                    <p className="text-muted-foreground text-sm">No benefit types yet.</p>
                  )
                ) : null}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Employee allowances</CardTitle>
              <CardDescription>
                Override the annual cap for one employee per benefit type and year. When an override
                exists, claims are capped by it instead of the type's default annual cap.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <Label>Employee</Label>
                  {employeeOptions.length ? (
                    <Select value={allowanceWorker || undefined} onValueChange={setAllowanceWorker}>
                      <SelectTrigger>
                        <SelectValue placeholder="Search employee…" />
                      </SelectTrigger>
                      <SelectContent className="max-h-60">
                        {employeeOptions.map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Input
                      value={allowanceWorker}
                      onChange={(event) => setAllowanceWorker(event.target.value)}
                      placeholder="Employee ID"
                    />
                  )}
                </div>
                <div>
                  <Label>Benefit type</Label>
                  {activeTypes.length ? (
                    <Select value={allowanceType || undefined} onValueChange={setAllowanceType}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select type…" />
                      </SelectTrigger>
                      <SelectContent>
                        {typeOptions(activeTypes)}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Input
                      value={allowanceType}
                      onChange={(event) => setAllowanceType(event.target.value)}
                      placeholder="e.g. medical"
                    />
                  )}
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <Label htmlFor="allowance-year">Year</Label>
                    <Input
                      id="allowance-year"
                      type="number"
                      value={allowanceYear}
                      onChange={(event) => setAllowanceYear(event.target.value)}
                    />
                  </div>
                  <div>
                    <Label htmlFor="allowance-amount">Annual amount</Label>
                    <Input
                      id="allowance-amount"
                      type="number"
                      min="0"
                      step="0.01"
                      value={allowanceAmount}
                      onChange={(event) => setAllowanceAmount(event.target.value)}
                    />
                  </div>
                </div>
              </div>
              <Button onClick={submitAllowance} disabled={busy}>
                Save allowance
              </Button>
              <div className="mt-1 text-sm font-medium">Overrides set</div>
              <div className="max-h-64 space-y-1 overflow-auto">
                {allowances.loading ? <p className="text-muted-foreground text-sm">Loading…</p> : null}
                {allowances.error ? <p className="text-destructive text-sm">{allowances.error}</p> : null}
                {allowances.data ? (
                  (allowances.data as AllowanceRow[]).length ? (
                    (allowances.data as AllowanceRow[]).map((row) => (
                      <p key={String(row.id)} className="text-sm text-muted-foreground">
                        {String(row.workerName ?? "—")}
                        {row.employeeNo ? ` (${String(row.employeeNo)})` : ""} · {String(row.benefitTypeName)} ·{" "}
                        {String(row.annualAmount)} {String(row.year)}
                      </p>
                    ))
                  ) : (
                    <p className="text-muted-foreground text-sm">
                      No overrides — the type's annual cap applies to everyone.
                    </p>
                  )
                ) : null}
              </div>
            </CardContent>
          </Card>

          <Card className="xl:col-span-2">
            <CardHeader>
              <CardTitle>Submit a claim</CardTitle>
              <CardDescription>
                HR can raise a claim on an employee's behalf. Evidence-attached must be true when the
                type requires it, otherwise the backend refuses the claim.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-3">
              <div className="sm:col-span-2">
                <Label>Employee</Label>
                {employeeOptions.length ? (
                  <Select value={claimWorker || undefined} onValueChange={setClaimWorker}>
                    <SelectTrigger>
                      <SelectValue placeholder="Search employee…" />
                    </SelectTrigger>
                    <SelectContent className="max-h-60">
                      {employeeOptions.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    value={claimWorker}
                    onChange={(event) => setClaimWorker(event.target.value)}
                    placeholder="Employee ID"
                  />
                )}
              </div>
              <div>
                <Label>Benefit type</Label>
                {activeTypes.length ? (
                  <Select value={claimType || undefined} onValueChange={setClaimType}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select type…" />
                    </SelectTrigger>
                    <SelectContent>
                      {typeOptions(activeTypes)}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    value={claimType}
                    onChange={(event) => setClaimType(event.target.value)}
                    placeholder="e.g. medical"
                  />
                )}
              </div>
              <div>
                <Label htmlFor="claim-amount">Amount claimed</Label>
                <Input
                  id="claim-amount"
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={claimAmount}
                  onChange={(event) => setClaimAmount(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="claim-currency">Currency</Label>
                <Select value={claimCurrency} onValueChange={setClaimCurrency}>
                  <SelectTrigger id="claim-currency">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="ZMW">ZMW</SelectItem>
                    <SelectItem value="USD">USD</SelectItem>
                    <SelectItem value="EUR">EUR</SelectItem>
                    <SelectItem value="GBP">GBP</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="claim-note">Note (optional)</Label>
                <Input
                  id="claim-note"
                  value={claimNote}
                  onChange={(event) => setClaimNote(event.target.value)}
                  placeholder="e.g. clinic visit receipt"
                />
              </div>
              <div className="flex items-end gap-2 pb-1">
                <Checkbox
                  id="claim-evidence"
                  checked={claimEvidence}
                  onCheckedChange={(checked) => setClaimEvidence(Boolean(checked))}
                />
                <Label htmlFor="claim-evidence" className="mb-0">
                  Evidence attached
                </Label>
              </div>
              <Button className="sm:col-span-3" onClick={submitClaim} disabled={busy}>
                Submit claim
              </Button>
            </CardContent>
          </Card>

          <Card className="xl:col-span-2">
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <CardTitle>Claims inbox</CardTitle>
                  <CardDescription>
                    Decide each submitted claim — approve with a final amount, reject with a reason,
                    or return it for more evidence — then mark it paid once the money has gone out.
                  </CardDescription>
                </div>
                <div className="flex items-center gap-2">
                  <Label htmlFor="claim-status" className="whitespace-nowrap">
                    Filter
                  </Label>
                  <Select value={statusFilter || "all"} onValueChange={(value) => setStatusFilter(value === "all" ? "" : value)}>
                    <SelectTrigger id="claim-status" className="w-40">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All statuses</SelectItem>
                      {Object.entries(STATUS_LABEL).map(([key, label]) => (
                        <SelectItem key={key} value={key}>
                          {label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </CardHeader>
            <CardContent className="overflow-x-auto">
              {claims.loading ? (
                <p className="text-sm text-muted-foreground">Loading claims…</p>
              ) : claims.error ? (
                <p className="text-sm text-destructive">{claims.error}</p>
              ) : !claims.data?.items?.length ? (
                <p className="text-sm text-muted-foreground">No claims yet.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Benefit</TableHead>
                      <TableHead className="text-right">Claimed</TableHead>
                      <TableHead>Amount approved</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Note</TableHead>
                      <TableHead>Decided</TableHead>
                      <TableHead>Paid</TableHead>
                      <TableHead>Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(claims.data.items as ClaimRow[]).map((row) => (
                      <TableRow key={String(row.id)}>
                        <TableCell className="text-sm">
                          <div>
                            {String(row.workerName ?? "—")}
                            {row.employeeNo ? <div className="text-muted-foreground">{String(row.employeeNo)}</div> : null}
                          </div>
                        </TableCell>
                        <TableCell className="text-sm">
                          {String(row.benefitTypeName ?? row.benefitTypeCode ?? "—")}
                        </TableCell>
                        <TableCell className="text-right text-sm">
                          {String(row.amountClaimed)} {String(row.currency ?? "ZMW")}
                        </TableCell>
                        <TableCell className="text-sm">
                          {row.approvedAmount ? `${String(row.approvedAmount)} ${String(row.currency ?? "ZMW")}` : "—"}
                        </TableCell>
                        <TableCell>
                          <span className={STATUS_CLASS[String(row.status)] ?? ""}>
                            {STATUS_LABEL[String(row.status)] ?? String(row.status)}
                          </span>
                        </TableCell>
                        <TableCell className="max-w-40 truncate text-sm text-muted-foreground">
                          {row.note ? String(row.note) : "—"}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {row.decisionReason ? String(row.decisionReason) : "—"}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {row.paidAt ? String(row.paidAt).slice(0, 10) : "—"}
                        </TableCell>
                        <TableCell>
                          {row.status === "submitted" || row.status === "returned" ? (
                            <div className="flex flex-wrap gap-1">
                              <Button
                                size="sm"
                                variant="outline"
                                className="h-7 text-xs"
                                onClick={() => decide(String(row.id), "approve")}
                                disabled={busy}
                              >
                                Approve
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                className="h-7 text-xs"
                                onClick={() => decide(String(row.id), "reject")}
                                disabled={busy}
                              >
                                Reject
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                className="h-7 text-xs"
                                onClick={() => decide(String(row.id), "return")}
                                disabled={busy}
                              >
                                Return
                              </Button>
                              {row.status === "returned" ? (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  className="h-7 text-xs"
                                  onClick={() => decide(String(row.id), "approve")}
                                  disabled={busy}
                                >
                                  Approve
                                </Button>
                              ) : null}
                            </div>
                          ) : row.status === "approved" ? (
                            <Button
                              size="sm"
                              className="h-7 text-xs"
                              onClick={() => pay(String(row.id))}
                              disabled={busy}
                            >
                              Mark paid
                            </Button>
                          ) : null}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}
