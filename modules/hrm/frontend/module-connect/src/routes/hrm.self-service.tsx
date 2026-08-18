import { createFileRoute, Link } from "@tanstack/react-router";
import { Bell, CalendarDays, FileText, LogIn, LogOut, Mail, Settings, UserRound, WalletCards } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/self-service")({ component: SelfService });
type Row = Record<string, unknown>;

function SelfService() {
  const overview = useApi(async () => {
    const [dashboard, notifications, requests, payslips, documents, letters] = await Promise.all([
      realApi.myDashboard(),
      realApi.myNotifications(),
      realApi.myRequests(),
      realApi.myPayslips() as Promise<{ items: unknown[] }>,
      realApi.myDocuments(),
      realApi.myLetters(),
    ]);
    return { dashboard, notifications, requests, payslips, documents, letters };
  }, []);

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Employee self-service"
          title="My HR workspace"
          description="Your profile, pay, leave, requests, documents, letters and HR updates in one ownership-protected workspace."
        />
        <Async state={overview} rows={6}>
          {(data) => {
            const notifications = data.notifications.items as Row[];
            // M27 P0 UX audit: documents + letters now return linked-worker
            // envelopes { workerId, workerName, employeeNo, linked, items } so
            // unlinked identities get a friendly state instead of a 422.
            const documents = (data.documents as { linked?: boolean; items?: unknown[] } | null) ?? {};
            const letters = (data.letters as { linked?: boolean; items?: unknown[] } | null) ?? {};
            const linked = data.dashboard.linked || Boolean(documents.linked ?? letters.linked);
            const dash = data.dashboard;
            const cards = [
              {
                label: "HR requests",
                value: Array.isArray(data.requests?.items) ? data.requests.items.length : 0,
                to: "/hrm/requests",
                icon: Mail,
              },
              {
                label: "Leave requests",
                value: dash.linked ? (dash.balances?.length ?? 0) + " types" : "—",
                to: "/hrm/leave",
                icon: CalendarDays,
              },
              {
                label: "Payslips",
                value: Array.isArray(data.payslips?.items) ? data.payslips.items.length : 0,
                to: "/hrm/payslips",
                icon: WalletCards,
              },
              {
                label: "Documents",
                value: Array.isArray(documents.items) ? documents.items.length : 0,
                to: "/hrm/my-documents",
                icon: FileText,
              },
              {
                label: "Letters",
                value: Array.isArray(letters.items) ? letters.items.length : 0,
                to: "/hrm/experience/letters",
                icon: FileText,
              },
              {
                label: "Profile",
                value: linked ? "Linked" : "Not linked",
                to: "/hrm/my-profile",
                icon: UserRound,
              },
              {
                label: "Preferences",
                value: linked ? "Set" : "—",
                to: "/hrm/my-preferences",
                icon: Settings,
              },
              {
                label: "My performance",
                value: linked ? "Review" : "—",
                to: "/hrm/my-performance",
                icon: Bell,
              },
            ];
            return (
              <div className="space-y-6" data-testid="employee-self-service">
                {/* M35: prominent clock-in/out card — first thing the employee sees */}
                {linked && dash.todayPunch ? (
                  <Card className="border-primary/20 bg-primary/5">
                    <CardContent className="flex flex-wrap items-center gap-4 p-4">
                      <div className="flex-1 min-w-48">
                        <p className="text-sm font-medium text-muted-foreground">Today</p>
                        <p className="text-xl font-semibold">
                          {dash.todayPunch.state === "in" ? "Clocked in" : "Clocked out"}
                          {dash.todayPunch.clockIn
                            ? ` at ${dash.todayPunch.clockIn.slice(0, 5)}`
                            : ""}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {dash.todayPunch.state === "in" ? "Don't forget to clock out" : "Have a good evening"}
                          {dash.todayPunch.totalHours > 0
                            ? ` · ${dash.todayPunch.totalHours.toFixed(1)}h worked`
                            : ""}
                        </p>
                      </div>
                      <div className="flex gap-2">
                        {dash.todayPunch.state === "out" ? (
                          <Button
                            size="sm"
                            onClick={() => realApi.clockMyselfIn().then(overview.reload)}
                          >
                            <LogIn className="size-4" /> Clock in
                          </Button>
                        ) : (
                          <Button
                            size="sm"
                            variant="secondary"
                            onClick={() => realApi.clockMyselfOut().then(overview.reload)}
                          >
                            <LogOut className="size-4" /> Clock out
                          </Button>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                ) : linked ? (
                  <Card className="border-primary/20 bg-primary/5">
                    <CardContent className="flex items-center gap-4 p-4">
                      <div className="flex-1">
                        <p className="text-sm font-medium text-muted-foreground">Welcome back</p>
                        <p className="text-lg font-semibold">{dash.workerName}</p>
                        <p className="text-xs text-muted-foreground">
                          {dash.employeeNo ?? ""} · Click below to clock in for the day
                        </p>
                      </div>
                      <Button size="sm" onClick={() => realApi.clockMyselfIn().then(overview.reload)}>
                        <LogIn className="size-4" /> Clock in
                      </Button>
                    </CardContent>
                  </Card>
                ) : null}

                {/* M35: leave balances summary */}
                {linked && dash.balances?.length ? (
                  <Card>
                    <CardHeader className="flex-row items-center justify-between pb-2">
                      <div>
                        <CardTitle className="text-sm font-semibold">Leave balances</CardTitle>
                        <CardDescription>Remaining days per leave type.</CardDescription>
                      </div>
                      <Button variant="outline" size="sm" asChild>
                        <Link to="/hrm/leave/new">Request leave</Link>
                      </Button>
                    </CardHeader>
                    <CardContent>
                      <div className="flex flex-wrap gap-2">
                        {dash.balances.map((b) => (
                          <div
                            key={b.leaveTypeCode}
                            className="rounded-md border bg-surface px-3 py-2 text-center"
                          >
                            <p className="text-xs text-muted-foreground capitalize">
                              {b.leaveTypeName || b.leaveTypeCode}
                            </p>
                            <p className="text-lg font-semibold">{b.available}</p>
                            <p className="text-[10px] text-muted-foreground">days available</p>
                          </div>
                        ))}
                      </div>
                    </CardContent>
                  </Card>
                ) : null}

                {!linked ? (
                  <Card className="border-warning bg-warning/10">
                    <CardHeader>
                      <CardTitle className="text-sm font-semibold">Account not linked</CardTitle>
                      <CardDescription>
                        This identity is not connected to any worker record, so personal HR surfaces
                        (leave, documents, letters, payslips) are empty. An HR administrator can link
                        it from the employee's profile page — Employees → open the record → Account
                        → Link account. Until then only HR requests can be raised from here.
                      </CardDescription>
                    </CardHeader>
                  </Card>
                ) : null}
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {cards.map((card) => (
                    <Link
                      key={card.label}
                      to={card.to}
                      className="rounded-lg border bg-surface p-4 hover:bg-surface-muted"
                    >
                      <card.icon className="size-4 text-muted-foreground" aria-hidden />
                      <p className="mt-3 text-sm font-medium">{card.label}</p>
                      <p className="mt-1 text-2xl font-semibold">{card.value}</p>
                    </Link>
                  ))}
                </div>
                <Card>
                  <CardHeader className="flex-row items-start justify-between gap-4">
                    <div>
                      <CardTitle className="flex items-center gap-2">
                        <Bell className="size-4" aria-hidden /> Notifications
                      </CardTitle>
                      <CardDescription>
                        {data.notifications.unreadCount} unread HR update
                        {data.notifications.unreadCount === 1 ? "" : "s"}.
                      </CardDescription>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={data.notifications.unreadCount === 0}
                      onClick={() => realApi.markAllMyNotificationsRead().then(overview.reload)}
                    >
                      Mark all read
                    </Button>
                  </CardHeader>
                  <CardContent>
                    <ul className="divide-y rounded-md border" data-testid="notification-inbox">
                      {notifications.map((notification) => (
                        <li key={String(notification.id)} className="flex items-center gap-3 p-3">
                          <span className="min-w-0 flex-1">
                            <strong className="block text-sm">{String(notification.title)}</strong>
                            <span className="text-xs text-muted-foreground">
                              {String(notification.createdAt).slice(0, 16).replace("T", " ")}
                            </span>
                          </span>
                          <StatusBadge status={notification.isRead ? "read" : "unread"} />
                          <Button
                            size="sm"
                            variant="outline"
                            asChild
                            onClick={() =>
                              realApi
                                .markMyNotificationRead(String(notification.id))
                                .then(overview.reload)
                            }
                          >
                            <Link to={String(notification.actionUrl)}>Open</Link>
                          </Button>
                        </li>
                      ))}
                      {notifications.length === 0 ? (
                        <li className="p-6 text-center text-sm text-muted-foreground">
                          No HR notifications yet.
                        </li>
                      ) : null}
                    </ul>
                  </CardContent>
                </Card>
              </div>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
