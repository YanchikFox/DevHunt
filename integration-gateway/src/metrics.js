import client from "prom-client";
import { getThrottleMetrics } from "./middleware/throttle.js";

const register = new client.Registry();

register.setDefaultLabels({
  service: "integration-gateway",
});

client.collectDefaultMetrics({
  prefix: "integration_gateway_",
  register,
});

const httpRequestDuration = new client.Histogram({
  name: "integration_gateway_http_request_duration_seconds",
  help: "HTTP request duration in seconds",
  labelNames: ["method", "route", "status_code"],
  buckets: [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10],
  registers: [register],
});

const httpRequestsTotal = new client.Counter({
  name: "integration_gateway_http_requests_total",
  help: "Total number of HTTP requests",
  labelNames: ["method", "route", "status_code"],
  registers: [register],
});

const outgoingHttpDuration = new client.Histogram({
  name: "integration_gateway_outgoing_http_duration_seconds",
  help: "Outgoing HTTP request duration (external providers)",
  labelNames: ["target", "status_code"],
  buckets: [0.1, 0.25, 0.5, 1, 2, 5, 10],
  registers: [register],
});

const webhookVerifyTotal = new client.Counter({
  name: "integration_gateway_webhook_verify_total",
  help: "Webhook signature verification attempts",
  labelNames: ["provider", "result"],
  registers: [register],
});

const rabbitConnectionState = new client.Gauge({
  name: "integration_gateway_rabbitmq_connection_state",
  help: "RabbitMQ connection state (1 = connected, 0 = disconnected)",
  registers: [register],
});

const eventMessagesTotal = new client.Counter({
  name: "integration_gateway_event_messages_total",
  help: "Events consumed from RabbitMQ by status",
  labelNames: ["routing_key", "status"],
  registers: [register],
});

const throttleActiveGauge = new client.Gauge({
  name: "integration_gateway_throttle_active_requests",
  help: "Active requests currently processed by throttle middleware",
  registers: [register],
});

const throttleQueueGauge = new client.Gauge({
  name: "integration_gateway_throttle_queue_length",
  help: "Queued requests waiting for processing",
  registers: [register],
});

const throttleOverloadGauge = new client.Gauge({
  name: "integration_gateway_throttle_overloaded",
  help: "System overload flag reported by throttle middleware",
  registers: [register],
});

function updateThrottleGauges() {
  const stats = getThrottleMetrics();
  throttleActiveGauge.set(stats.activeRequests);
  throttleQueueGauge.set(stats.queueLength);
  throttleOverloadGauge.set(stats.isOverloaded ? 1 : 0);
}

function sanitizeRoute(req) {
  if (req.route?.path) {
    return req.route.path;
  }
  if (req.baseUrl) {
    return `${req.baseUrl}${req.path}`;
  }
  return req.originalUrl?.split("?")[0] || "unknown";
}

function metricsMiddleware(req, res, next) {
  const endTimer = httpRequestDuration.startTimer();

  res.on("finish", () => {
    const labels = {
      method: req.method,
      route: sanitizeRoute(req),
      status_code: res.statusCode,
    };
    httpRequestsTotal.inc(labels);
    endTimer(labels);
  });

  next();
}

async function metricsHandler(req, res) {
  updateThrottleGauges();
  res.set("Content-Type", register.contentType);
  res.end(await register.metrics());
}

function setRabbitConnectionState(isConnected) {
  rabbitConnectionState.set(isConnected ? 1 : 0);
}

function recordEventResult(routingKey, status) {
  eventMessagesTotal.inc({
    routing_key: routingKey || "unknown",
    status,
  });
}

function observeOutgoingHttp(target, statusCode, seconds) {
  outgoingHttpDuration.labels(target || "unknown", statusCode || "0").observe(seconds);
}

function recordWebhookVerify(provider, result) {
  webhookVerifyTotal.inc({
    provider: provider || "unknown",
    result: result || "unknown",
  });
}

export {
  metricsMiddleware,
  metricsHandler,
  setRabbitConnectionState,
  recordEventResult,
  observeOutgoingHttp,
  recordWebhookVerify,
};
