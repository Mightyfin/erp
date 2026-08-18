import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/my-preferences")({ component: MyPreferences });

/** Known preference keys the UI toggles; unknown keys pass through untouched. */
interface PreferencesShape {
  email?: boolean;
  inApp?: boolean;
  topics?: {
    payroll?: boolean;
    leave?: boolean;
    requests?: boolean;
    general?: boolean;
  };
  [key: string]: unknown;
}

function parsePreferences(raw: string | null): PreferencesShape {
  if (!raw) return { email: true, inApp: true };
  try {
    return JSON.parse(raw) as PreferencesShape;
  } catch {
    return { email: true, inApp: true };
  }
}

function MyPreferences() {
  const prefs = useApi(() => realApi.myPreferences(), []);

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Employee self-service"
          title="Notification preferences"
          description="Choose how you want to be notified about HR updates. Organisation defaults apply until you set your own."
        />
        <Async state={prefs}>
          {(data) => (
            <PreferencesForm
              initial={parsePreferences(data.preferences ?? null)}
              onSave={() => prefs.reload()}
            />
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}

function PreferencesForm({ initial, onSave }: { initial: PreferencesShape; onSave: () => void }) {
  const [email, setEmail] = useState(initial.email ?? true);
  const [inApp, setInApp] = useState(initial.inApp ?? true);
  const [topics, setTopics] = useState(initial.topics ?? {
    payroll: true,
    leave: true,
    requests: true,
    general: true,
  });
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // Sync when the fetch resolves with fresh server data.
  useEffect(() => {
    setEmail(initial.email ?? true);
    setInApp(initial.inApp ?? true);
    setTopics(initial.topics ?? { payroll: true, leave: true, requests: true, general: true });
  }, [initial]);

  const save = async () => {
    setSaving(true);
    try {
      await realApi.updateMyPreferences({ email, inApp, topics });
      setSaved(true);
      onSave();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-xl space-y-4" data-testid="my-preferences">
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Channels</CardTitle>
          <CardDescription>How notifications reach you.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <Label htmlFor="pref-email">Email notifications</Label>
              <p className="text-xs text-muted-foreground">
                Receive HR updates at your work email address.
              </p>
            </div>
            <Switch id="pref-email" checked={email} onCheckedChange={setEmail} />
          </div>
          <div className="flex items-center justify-between">
            <div>
              <Label htmlFor="pref-inapp">In-app notifications</Label>
              <p className="text-xs text-muted-foreground">
                Show updates in your HR workspace inbox.
              </p>
            </div>
            <Switch id="pref-inapp" checked={inApp} onCheckedChange={setInApp} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Topics</CardTitle>
          <CardDescription>Which kinds of updates you want to receive.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 sm:grid-cols-2">
          {(Object.keys(topics) as Array<keyof NonNullable<PreferencesShape["topics"]>>).map(
            (key) => (
              <div key={key} className="flex items-center gap-2">
                <Checkbox
                  id={`topic-${key}`}
                  checked={Boolean(topics[key])}
                  onCheckedChange={(checked) =>
                    setTopics({ ...topics, [key]: Boolean(checked) })
                  }
                />
                <Label
                  htmlFor={`topic-${key}`}
                  className="text-sm font-normal capitalize"
                >
                  {key}
                </Label>
              </div>
            ),
          )}
        </CardContent>
      </Card>

      <div className="flex items-center gap-3">
        <Button onClick={save} disabled={saving}>
          {saving ? "Saving…" : "Save preferences"}
        </Button>
        {saved ? (
          <span className="text-xs text-muted-foreground">Saved.</span>
        ) : null}
      </div>
    </div>
  );
}
