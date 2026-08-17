import { createFileRoute, Link } from "@tanstack/react-router";
import { Bell, CalendarDays, FileText, Mail, UserRound, WalletCards } from "lucide-react";
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
    const [notifications, requests, leave, payslips, documents, letters] = await Promise.all([
      realApi.myNotifications(),
      realApi.myRequests(),
      realApi.myLeave(),
      realApi.myPayslips() as Promise<{ items: unknown[] }>,
      realApi.myDocuments(),
      realApi.myLetters(),
    ]);
    return { notifications, requests, leave, payslips, documents, letters };
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
            const leave = (data.leave as { requests?: unknown[]; linked?: boolean } | null) ?? {};
            const linked = Boolean(documents.linked ?? leave.linked ?? letters.linked);
            const cards = [
              {
                label: "HR requests",
                value: Array.isArray(data.requests?.items) ? data.requests.items.length : 0,
                to: "/hrm/requests",
                icon: Mail,
              },
              {
                label: "Leave requests",
                value: leave.requests?.length ?? 0,
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
            ];
            return (
              <div className="space-y-6" data-testid="employee-self-service">
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
