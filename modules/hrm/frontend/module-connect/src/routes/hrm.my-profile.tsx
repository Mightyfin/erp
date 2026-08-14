/**
 * M15 self-service profile.
 *
 * This is the page an employee lands on from "My profile". It shows the worker
 * record linked to the signed-in Keycloak subject and lets the employee edit
 * the fields HR allows self-service on (personal & statutory details,
 * emergency contacts, bank details). Admin-only fields such as name, grade,
 * job title and status are deliberately read-only.
 *
 * Identity is always resolved from the bearer token on the server
 * (PUT /hrm/me/profile re-reads the `sub` claim), so the client can never
 * save changes against another worker's record.
 */
import { useState } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import {
  AlertTriangle,
  BadgeCheck,
  Landmark,
  LifeBuoy,
  Save,
  ShieldCheck,
  UserRound,
} from "lucide-react";

import { hrmApi, type LinkedWorker, type SelfProfileUpdate } from "@/platform/api-client";
import { useAuth } from "@/platform/auth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";

export const Route = createFileRoute("/hrm/my-profile")({
  component: MyProfilePage,
});

function SectionCard({
  icon: Icon,
  title,
  children,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-sm">
          <Icon className="size-4" aria-hidden />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-2">{children}</CardContent>
    </Card>
  );
}

function Field({
  label,
  value,
  readOnly,
  onChange,
  hint,
}: {
  label: string;
  value: string;
  readOnly?: boolean;
  onChange?: (v: string) => void;
  hint?: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      <Input
        value={value}
        readOnly={readOnly}
        onChange={(e) => onChange?.(e.target.value)}
        className={readOnly ? "bg-muted/40" : ""}
        aria-label={label}
      />
      {hint ? <p className="text-[11px] text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

export function MyProfilePage() {
  const { worker, resolvingWorker } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState<SelfProfileUpdate>({});
  const [saving, setSaving] = useState(false);
  const hasChanges = Object.values(form).some(
    (v) => v !== undefined && v !== null && (Array.isArray(v) ? v.length > 0 : v !== ""),
  );

  const profile = worker as LinkedWorker | null;

  const set = (patch: Partial<SelfProfileUpdate>) =>
    setForm((f) => ({ ...f, ...patch }));

  const save = async () => {
    if (!hasChanges) return;
    setSaving(true);
    try {
      const res = await hrmApi.updateSelfProfile(form);
      toast.success("Profile updated", { description: "Your changes have been saved." });
      setForm({});
      navigate({ to: "/hrm" }).catch(() => undefined);
      void res;
    } catch (err) {
      const code = err instanceof Error && "code" in err ? (err as { code?: string }).code : undefined;
      toast.error(
        code === "not-linked" ? "Not linked to a worker record" : "Could not save changes",
        {
          description:
            code === "not-linked"
              ? "Ask HR to link your login to your worker record."
              : err instanceof Error
                ? err.message
                : "Please try again.",
        },
      );
    } finally {
      setSaving(false);
    }
  };

  if (resolvingWorker) {
    return (
      <div className="space-y-4 p-6">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }
  if (!profile) {
    return (
      <div className="flex min-h-64 flex-col items-center justify-center gap-3 p-6 text-center">
        <AlertTriangle className="size-8 text-muted-foreground" aria-hidden />
        <p className="font-medium">No worker record is linked to your login</p>
        <p className="max-w-sm text-sm text-muted-foreground">
          Ask HR to link your Keycloak account to your worker record, then reload this page.
        </p>
      </div>
    );
  }

  const w = profile as unknown as {
    preferredName?: string | null;
    email?: string | null;
    phone?: string | null;
    nrc?: string | null;
    passportNo?: string | null;
    tpin?: string | null;
    napsaNumber?: string | null;
    nhimaNumber?: string | null;
    nationality?: string | null;
    dateOfBirth?: string | null;
  };
  const base: Record<string, string | null | undefined> = w;

  const val = (key: keyof SelfProfileUpdate) => {
    const pending = form[key];
    if (pending !== undefined)
      return Array.isArray(pending) ? undefined : (pending as string | undefined);
    const current = base[String(key)] as string | null | undefined;
    return current ?? undefined;
  };
  const v = (key: keyof typeof base) => String(base[key] ?? "");

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-primary/10">
          <UserRound className="size-6 text-primary" aria-hidden />
        </span>
        <div>
          <h1 className="text-xl font-semibold">My profile</h1>
          <p className="text-sm text-muted-foreground">
            {profile.fullName} · {profile.employeeNo} · {profile.jobTitle ?? "—"}
          </p>
        </div>
        <span className="ml-auto flex items-center gap-1.5 rounded-full border bg-muted/50 px-3 py-1 text-xs text-muted-foreground">
          <ShieldCheck className="size-3.5" aria-hidden /> Self-service
        </span>
      </div>

      <SectionCard icon={BadgeCheck} title="Personal details">
        <Field label="Preferred name" value={val("preferredName") ?? ""} onChange={(s) => set({ preferredName: s })} />
        <Field label="Email" value={val("email") ?? v("email")} onChange={(s) => set({ email: s })} />
        <Field label="Phone" value={val("phone") ?? v("phone")} onChange={(s) => set({ phone: s })} />
        <Field label="Date of birth" value={val("dateOfBirth") ?? v("dateOfBirth")} onChange={(s) => set({ dateOfBirth: s })} hint="YYYY-MM-DD" />
        <Field label="Nationality" value={val("nationality") ?? v("nationality")} onChange={(s) => set({ nationality: s })} />
      </SectionCard>

      <SectionCard icon={ShieldCheck} title="Statutory & identification (Zambia)">
        <Field label="NRC" value={val("nrc") ?? v("nrc")} onChange={(s) => set({ nrc: s })} />
        <Field label="TPIN" value={val("tpin") ?? v("tpin")} onChange={(s) => set({ tpin: s })} />
        <Field label="NAPSA number" value={val("napsaNumber") ?? v("napsaNumber")} onChange={(s) => set({ napsaNumber: s })} />
        <Field label="NHIMA number" value={val("nhimaNumber") ?? v("nhimaNumber")} onChange={(s) => set({ nhimaNumber: s })} />
        <Field label="Passport no." value={val("passportNo") ?? v("passportNo")} onChange={(s) => set({ passportNo: s })} />
      </SectionCard>

      <SectionCard icon={Landmark} title="Bank details">
        <div className="rounded-md border bg-muted/40 p-4 text-sm text-muted-foreground sm:col-span-2">
          Bank and payment details are managed by payroll. To change your payout account,
          raise a request with the payroll team — updating it yourself requires payroll approval.
        </div>
      </SectionCard>

      <SectionCard icon={LifeBuoy} title="Emergency contacts">
        <div className="rounded-md border bg-muted/40 p-4 text-sm text-muted-foreground sm:col-span-2">
          Emergency contacts are managed by HR. Contact the HR office to add or update an
          emergency contact for your file.
        </div>
      </SectionCard>

      {hasChanges && (
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => setForm({})} disabled={saving}>
            Discard
          </Button>
          <Button onClick={save} disabled={saving}>
            <Save className="size-4" aria-hidden />
            {saving ? "Saving…" : "Save changes"}
          </Button>
        </div>
      )}
    </div>
  );
}
