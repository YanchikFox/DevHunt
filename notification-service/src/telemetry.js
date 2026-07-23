import { NodeSDK } from "@opentelemetry/sdk-node";
import { getNodeAutoInstrumentations } from "@opentelemetry/auto-instrumentations-node";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { resourceFromAttributes } from "@opentelemetry/resources";

// OpenObserve OTLP endpoint (Jaeger removed)
const otlpEndpoint =
  process.env.OTEL_EXPORTER_OTLP_ENDPOINT ??
  "http://openobserve:5080/api/default/v1/traces";

const traceExporter = new OTLPTraceExporter({ url: otlpEndpoint });
const spanProcessor = new BatchSpanProcessor(traceExporter);

const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    "service.name": process.env.OTEL_SERVICE_NAME || "notification-service",
    "service.namespace": "devhunt",
    "service.environment": process.env.ENVIRONMENT || "development",
  }),
  spanProcessor,
  instrumentations: [
    getNodeAutoInstrumentations({
      "@opentelemetry/instrumentation-http": {
        enabled: true,
        ignoreOutgoingPaths: [/\/metrics/],
      },
      "@opentelemetry/instrumentation-express": {
        enabled: true,
      },
    }),
  ],
});

const startTelemetry = async () => {
  try {
    await sdk.start();
  } catch (err) {
    console.error("Failed to start OpenTelemetry SDK", err);
  }
};

startTelemetry();

const shutdown = async () => {
  try {
    await sdk.shutdown();
  } catch (err) {
    console.error("Error shutting down OpenTelemetry SDK", err);
  }
  // Note: Don't call process.exit() here as this module may be imported elsewhere
  // The main application should handle process termination
};

process.once("SIGTERM", shutdown);
process.once("SIGINT", shutdown);
