import type { components } from "./api/types.gen";
import { httpRequest } from "./http";

/** Types originate from the backend-owned OpenAPI contract. */
export type IntegrationProvider = components["schemas"]["IntegrationProvider"];
export type IntegrationSummary = components["schemas"]["IntegrationSummary"];
export type IntegrationCredentialsWrite = components["schemas"]["IntegrationCredentialsWrite"];

export const integrationsApi = {
  list: () => httpRequest<IntegrationSummary[]>("/api/admin/integrations"),

  replace: (provider: IntegrationProvider, values: IntegrationCredentialsWrite["values"]) =>
    httpRequest<IntegrationSummary>(`/api/admin/integrations/${provider}`, {
      method: "PUT",
      body: JSON.stringify({ values }),
    }),

  test: (provider: IntegrationProvider) =>
    httpRequest<IntegrationSummary>(`/api/admin/integrations/${provider}/test`, {
      method: "POST",
    }),
};
