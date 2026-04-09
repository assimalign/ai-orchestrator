import {
  ApplicationInsights,
  DistributedTracingModes,
} from "@microsoft/applicationinsights-web";
import { runtimeConfig } from "./runtime-config";

let telemetryClient: ApplicationInsights | undefined;

function getCorrelationDomain() {
  try {
    return new URL(runtimeConfig.apiBaseUrl).host;
  } catch {
    return undefined;
  }
}

export function initializeTelemetry() {
  if (telemetryClient || !runtimeConfig.appInsightsConnectionString) {
    return telemetryClient;
  }

  const correlationDomain = getCorrelationDomain();

  telemetryClient = new ApplicationInsights({
    config: {
      connectionString: runtimeConfig.appInsightsConnectionString,
      disableAjaxTracking: false,
      disableFetchTracking: false,
      distributedTracingMode: DistributedTracingModes.AI_AND_W3C,
      enableAutoRouteTracking: true,
      enableCorsCorrelation: true,
      correlationHeaderDomains: correlationDomain ? [correlationDomain] : undefined,
    },
  });

  telemetryClient.loadAppInsights();
  telemetryClient.addTelemetryInitializer((item) => {
    item.tags = {
      ...(item.tags ?? {}),
      "ai.cloud.role": "Assimalign.AI.Orchestrator.Web",
      "ai.cloud.roleInstance": window.location.host,
    };
  });
  telemetryClient.trackPageView({});

  return telemetryClient;
}

export function getTelemetryClient() {
  return telemetryClient;
}
