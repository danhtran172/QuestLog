import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CheckCircle2,
  CircleAlert,
  Cloud,
  KeyRound,
  Loader2,
  PlugZap,
  RefreshCw,
} from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import {
  integrationsApi,
  type IntegrationCredentialsWrite,
  type IntegrationSummary,
} from "@/lib/integrationsApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { TextInput } from "./ui";

/** Mask shown in a saved secret field — the value itself never reaches the browser. */
const SAVED_SECRET_MASK = "••••••••••••";

/** Secret fields start blank; public identifiers (cloud name, client ID) start at their saved value. */
function initialValues(integration: IntegrationSummary): Record<string, string> {
  return Object.fromEntries(integration.requiredFields.map((field) => [field, integration.identifiers?.[field] ?? ""]));
}

function message(error: unknown): string {
  return error instanceof ApiRequestError || error instanceof Error ? error.message : String(error);
}

function statusTone(status: IntegrationSummary["lastTest"]["status"]): string {
  if (status === "passed") return "border-primary/35 bg-primary/10 text-primary";
  if (status === "failed") return "border-danger/40 bg-danger/10 text-danger";
  return "border-border bg-surface-2/60 text-muted-fg";
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function IntegrationCard({ integration }: { integration: IntegrationSummary }) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [values, setValues] = useState<Record<string, string>>(() => initialValues(integration));

  useEffect(() => {
    // Only public identifiers are hydrated; saved secrets are never sent to the browser.
    setValues(initialValues(integration));
  }, [integration]);

  const complete = useMemo(
    () => integration.requiredFields.every((field) => values[field]?.trim()),
    [integration.requiredFields, values],
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin-integrations"] });
  const save = useMutation({
    mutationFn: () => integrationsApi.replace(
      integration.provider,
      values as IntegrationCredentialsWrite["values"],
    ),
    onSuccess: async () => {
      await invalidate();
      toast.success(t("admin.integrations.saved"));
    },
    onError: (error) => toast.error(message(error)),
  });
  const test = useMutation({
    mutationFn: () => integrationsApi.test(integration.provider),
    onSuccess: async (result) => {
      await invalidate();
      toast.success(t(result.lastTest.status === "passed"
        ? "admin.integrations.testPassed"
        : "admin.integrations.testFailed"));
    },
    onError: (error) => toast.error(message(error)),
  });

  const busy = save.isPending || test.isPending;
  const configured = new Set(integration.configuredFields);
  const identifiers = integration.identifiers ?? {};
  const error = save.error ?? test.error;

  return (
    <section className="surface-card flex min-w-0 flex-col overflow-hidden">
      <div className="flex items-start justify-between gap-3 border-b border-border px-4 py-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <PlugZap size={16} className="text-primary" aria-hidden="true" />
            <h2 className="text-sm font-extrabold text-fg">
              {t(`admin.integrations.provider.${integration.provider}`)}
            </h2>
          </div>
          <p className="mt-1 text-xs leading-5 text-muted-fg">
            {t(`admin.integrations.providerHint.${integration.provider}`)}
          </p>
        </div>
        <span
          className={cn(
            "shrink-0 rounded-full border px-2 py-1 text-[10px] font-black uppercase tracking-[0.1em]",
            integration.configured ? "border-primary/35 bg-primary/10 text-primary" : "border-border bg-surface-2/60 text-muted-fg",
          )}
        >
          {t(integration.configured ? "admin.integrations.configured" : "admin.integrations.notConfigured")}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-3 p-4">
        <div className="grid gap-2 sm:grid-cols-2">
          {integration.requiredFields.map((field) => (
            <label key={field} className="block space-y-1.5">
              <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wide text-muted-fg">
                <KeyRound size={12} aria-hidden="true" />
                {t(`admin.integrations.field.${field}`)}
                {configured.has(field) ? (
                  <span className="rounded-full border border-primary/30 bg-primary/10 px-1.5 py-0.5 text-[9px] text-primary">
                    {t("admin.integrations.savedBadge")}
                  </span>
                ) : null}
              </span>
              <TextInput
                type={field in identifiers || field === "cloudName" ? "text" : "password"}
                autoComplete="off"
                value={values[field] ?? ""}
                disabled={busy}
                className={configured.has(field) ? "placeholder:tracking-[0.2em] placeholder:text-fg/70" : undefined}
                placeholder={configured.has(field)
                  ? SAVED_SECRET_MASK
                  : t("admin.integrations.enterPlaceholder")}
                onChange={(event) => setValues((current) => ({ ...current, [field]: event.target.value }))}
              />
            </label>
          ))}
        </div>

        <div className="mt-auto flex flex-wrap items-center gap-2 border-t border-border/70 pt-3">
          <button
            type="button"
            disabled={busy || !complete}
            onClick={() => save.mutate()}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-primary px-3 text-xs font-extrabold text-primary-fg transition-opacity disabled:cursor-not-allowed disabled:opacity-45"
          >
            {save.isPending ? <Loader2 size={14} className="animate-spin" /> : <KeyRound size={14} />}
            {t("admin.integrations.save")}
          </button>
          <button
            type="button"
            disabled={busy || !integration.configured}
            onClick={() => test.mutate()}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-border px-3 text-xs font-extrabold text-fg transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-45"
          >
            {test.isPending ? <Loader2 size={14} className="animate-spin" /> : <RefreshCw size={14} />}
            {t("admin.integrations.test")}
          </button>
          <span className={cn("ml-auto inline-flex items-center gap-1.5 rounded-full border px-2 py-1 text-[10px] font-bold", statusTone(integration.lastTest.status))}>
            {integration.lastTest.status === "passed" ? <CheckCircle2 size={12} /> : <CircleAlert size={12} />}
            {t(`admin.integrations.status.${integration.lastTest.status}`)}
          </span>
        </div>

        {integration.lastTest.errorCode ? (
          <p className="text-xs text-danger" role="status">
            {t("admin.integrations.lastError")}: {integration.lastTest.errorCode}
          </p>
        ) : null}
        <div className="flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-muted-fg">
          <span>{t("admin.integrations.lastTestAt")}: {formatTimestamp(integration.lastTest.testedAt)}</span>
          <span>{t("admin.integrations.updatedAt")}: {formatTimestamp(integration.updatedAt)}</span>
        </div>
        {error ? <p className="text-xs text-danger" role="alert">{message(error)}</p> : null}
      </div>
    </section>
  );
}

export function AdminIntegrationsSection() {
  const { t } = useI18n();
  const integrations = useQuery({
    queryKey: ["admin-integrations"],
    queryFn: () => integrationsApi.list(),
  });

  if (integrations.isPending) {
    return (
      <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg">
        <Loader2 size={16} className="animate-spin" /> {t("admin.integrations.loading")}
      </p>
    );
  }

  if (integrations.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <CircleAlert size={22} className="text-danger" />
        <p className="text-sm text-fg">{message(integrations.error)}</p>
        <button type="button" onClick={() => void integrations.refetch()} className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg">
          {t("error.retry")}
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <div className="flex items-start gap-3 rounded-xl border border-primary/30 bg-primary/10 p-4">
        <Cloud size={18} className="mt-0.5 shrink-0 text-primary" aria-hidden="true" />
        <div>
          <h1 className="text-sm font-extrabold text-fg">{t("admin.integrations.title")}</h1>
          <p className="mt-1 text-xs leading-5 text-muted-fg">{t("admin.integrations.description")}</p>
        </div>
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        {integrations.data.map((integration) => <IntegrationCard key={integration.provider} integration={integration} />)}
      </div>
    </div>
  );
}
