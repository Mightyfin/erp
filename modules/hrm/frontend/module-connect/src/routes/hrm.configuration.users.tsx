import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { KeyRound, RefreshCw, UserPlus, Users, UserX } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { ApiError, hrmApi, type LocalAuthUser } from "@/platform/api-client";

export const Route = createFileRoute("/hrm/configuration/users")({
  head: () => ({
    meta: [
      { title: "Users — New World Cargo HRM" },
      { name: "description", content: "Manage local HRM accounts, roles, access and passwords." },
      { property: "og:title", content: "Users — New World Cargo HRM" },
      { property: "og:description", content: "Manage local HRM accounts, roles, access and passwords." },
    ],
  }),
  component: UsersConfiguration,
});

const roles = [
  ["hr_admin", "HR administrator"],
  ["hr_ops", "HR operations"],
  ["payroll", "Payroll"],
  ["manager", "Manager"],
  ["approver", "Approver"],
  ["auditor", "Auditor"],
] as const;

function messageFor(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : error instanceof Error ? error.message : fallback;
}

function UsersConfiguration() {
  const [users, setUsers] = useState<LocalAuthUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("hr_ops");
  const [creating, setCreating] = useState(false);
  const [selectedUser, setSelectedUser] = useState<LocalAuthUser | null>(null);
  const [resetPassword, setResetPassword] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);

  const activeCount = useMemo(() => users.filter((user) => user.isActive).length, [users]);

  const loadUsers = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await hrmApi.auth.users();
      setUsers(response.items ?? []);
    } catch (err) {
      setError(messageFor(err, "Unable to load local HRM users."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadUsers();
  }, []);

  const createUser = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setCreating(true);
    setError(null);
    setNotice(null);
    try {
      await hrmApi.auth.createUser({
        email: email.trim(),
        displayName: displayName.trim(),
        password,
        roles: [role],
      });
      setEmail("");
      setDisplayName("");
      setPassword("");
      setRole("hr_ops");
      setNotice("The local account was created. Give the user their temporary password securely.");
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to create the local account."));
    } finally {
      setCreating(false);
    }
  };

  const toggleUser = async (user: LocalAuthUser, isActive: boolean) => {
    setError(null);
    setNotice(null);
    try {
      await hrmApi.auth.updateUser(user.id, { isActive });
      setNotice(`${user.email} is now ${isActive ? "active" : "inactive"}.`);
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to update account status."));
    }
  };

  const changeRole = async (user: LocalAuthUser, nextRole: string) => {
    setError(null);
    setNotice(null);
    try {
      await hrmApi.auth.updateUser(user.id, { roles: [nextRole] });
      setNotice(`Role updated for ${user.email}.`);
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to update the account role."));
    }
  };

  const resetUserPassword = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedUser) return;
    setError(null);
    setNotice(null);
    try {
      await hrmApi.auth.resetPassword(selectedUser.id, resetPassword);
      setResetPassword("");
      setNotice(`Password reset for ${selectedUser.email}. Share the temporary password securely.`);
      setSelectedUser(null);
    } catch (err) {
      setError(messageFor(err, "Unable to reset the account password."));
    }
  };

  const changeOwnPassword = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSavingPassword(true);
    setError(null);
    setNotice(null);
    try {
      await hrmApi.auth.changePassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setNotice("Your password was changed. Your current session remains active.");
    } catch (err) {
      setError(messageFor(err, "Unable to change your password."));
    } finally {
      setSavingPassword(false);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Configuration · Security"
          title="Local users"
          description="Manage the accounts stored in this HRMS database. No IDP, OIDC provider or Mightyfin redirect is required."
          meta={<Button variant="outline" size="sm" onClick={() => void loadUsers()}><RefreshCw className="mr-2 size-3.5" aria-hidden />Refresh</Button>}
        />

        {error ? <div className="mb-4 rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive" role="alert">{error}</div> : null}
        {notice ? <div className="mb-4 rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm text-primary" role="status">{notice}</div> : null}

        <div className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
          <section className="rounded-lg border bg-surface p-5" aria-labelledby="accounts-heading">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 id="accounts-heading" className="flex items-center gap-2 text-sm font-semibold"><Users className="size-4 text-primary" aria-hidden />Accounts</h2>
                <p className="mt-1 text-xs text-muted-foreground">{activeCount} active of {users.length} local account{users.length === 1 ? "" : "s"}.</p>
              </div>
            </div>
            <div className="mt-4 overflow-x-auto rounded-md border">
              <table className="w-full min-w-[700px] text-left text-sm">
                <thead className="bg-surface-muted text-xs uppercase tracking-wide text-muted-foreground">
                  <tr><th className="px-3 py-2 font-medium">User</th><th className="px-3 py-2 font-medium">Role</th><th className="px-3 py-2 font-medium">Status</th><th className="px-3 py-2 text-right font-medium">Actions</th></tr>
                </thead>
                <tbody className="divide-y">
                  {loading ? <tr><td className="px-3 py-5 text-muted-foreground" colSpan={4}>Loading local accounts…</td></tr> : null}
                  {!loading && users.length === 0 ? <tr><td className="px-3 py-5 text-muted-foreground" colSpan={4}>No local accounts found.</td></tr> : null}
                  {!loading && users.map((user) => {
                    const currentRole = user.roles[0] ?? "auditor";
                    return <tr key={user.id}>
                      <td className="px-3 py-3"><div className="font-medium">{user.displayName}</div><div className="text-xs text-muted-foreground">{user.email}</div>{user.mustChangePassword ? <div className="mt-1 text-[11px] text-amber-700">Password change required</div> : null}</td>
                      <td className="px-3 py-3"><select className="h-8 rounded-md border bg-background px-2 text-xs" value={currentRole} onChange={(event) => void changeRole(user, event.target.value)} aria-label={`Role for ${user.email}`}>{roles.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></td>
                      <td className="px-3 py-3"><StatusBadge status={user.isActive ? "active" : "inactive"} /></td>
                      <td className="px-3 py-3"><div className="flex justify-end gap-2"><Button variant="outline" size="sm" onClick={() => { setSelectedUser(user); setResetPassword(""); }}><KeyRound className="mr-1.5 size-3.5" aria-hidden />Reset</Button><Switch checked={user.isActive} onCheckedChange={(checked) => void toggleUser(user, checked)} aria-label={`${user.isActive ? "Deactivate" : "Activate"} ${user.email}`} /></div></td>
                    </tr>;
                  })}
                </tbody>
              </table>
            </div>
          </section>

          <div className="space-y-5">
            <section className="rounded-lg border bg-surface p-5" aria-labelledby="create-heading">
              <h2 id="create-heading" className="flex items-center gap-2 text-sm font-semibold"><UserPlus className="size-4 text-primary" aria-hidden />Create account</h2>
              <p className="mt-1 text-xs text-muted-foreground">Create a tenant-scoped local login. Passwords are hashed by the API and never stored in plain text.</p>
              <form className="mt-4 space-y-3" onSubmit={createUser}>
                <div><Label htmlFor="new-display-name">Name</Label><Input id="new-display-name" className="mt-1" value={displayName} onChange={(event) => setDisplayName(event.target.value)} required /></div>
                <div><Label htmlFor="new-email">Email</Label><Input id="new-email" type="email" autoComplete="off" className="mt-1" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
                <div><Label htmlFor="new-password">Temporary password</Label><Input id="new-password" type="password" autoComplete="new-password" className="mt-1" value={password} onChange={(event) => setPassword(event.target.value)} minLength={12} required /></div>
                <div><Label htmlFor="new-role">Role</Label><select id="new-role" className="mt-1 flex h-9 w-full rounded-md border bg-background px-3 text-sm" value={role} onChange={(event) => setRole(event.target.value)}>{roles.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
                <Button className="w-full" type="submit" disabled={creating}>{creating ? "Creating…" : "Create local account"}</Button>
              </form>
            </section>

            <section className="rounded-lg border bg-surface p-5" aria-labelledby="password-heading">
              <h2 id="password-heading" className="flex items-center gap-2 text-sm font-semibold"><KeyRound className="size-4 text-primary" aria-hidden />Change your password</h2>
              <form className="mt-4 space-y-3" onSubmit={changeOwnPassword}>
                <div><Label htmlFor="current-password">Current password</Label><Input id="current-password" type="password" autoComplete="current-password" className="mt-1" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required /></div>
                <div><Label htmlFor="new-own-password">New password</Label><Input id="new-own-password" type="password" autoComplete="new-password" className="mt-1" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} minLength={12} required /></div>
                <Button variant="outline" className="w-full" type="submit" disabled={savingPassword}>{savingPassword ? "Saving…" : "Change password"}</Button>
              </form>
            </section>
          </div>
        </div>

        {selectedUser ? <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true" aria-labelledby="reset-heading">
          <div className="w-full max-w-md rounded-lg border bg-background p-5 shadow-xl">
            <div className="flex items-start justify-between gap-4"><div><h2 id="reset-heading" className="font-semibold">Reset password</h2><p className="mt-1 text-sm text-muted-foreground">Set a temporary password for {selectedUser.email}.</p></div><Button variant="ghost" size="sm" onClick={() => setSelectedUser(null)} aria-label="Close">×</Button></div>
            <form className="mt-4 space-y-3" onSubmit={resetUserPassword}><div><Label htmlFor="reset-password">Temporary password</Label><Input id="reset-password" type="password" autoComplete="new-password" className="mt-1" value={resetPassword} onChange={(event) => setResetPassword(event.target.value)} minLength={12} required /></div><div className="flex justify-end gap-2"><Button variant="outline" type="button" onClick={() => setSelectedUser(null)}>Cancel</Button><Button type="submit">Reset password</Button></div></form>
          </div>
        </div> : null}
      </AppShell>
    </AuthGate>
  );
}
