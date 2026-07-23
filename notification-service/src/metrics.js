import client from "prom-client";
import { getThrottleMetrics } from "./middleware/throttle.js";

const register = new client.Registry();

register.setDefaultLabels({
  service: "notification-service",
});

client.collectDefaultMetrics({
  prefix: "notification_service_",
  register,
});

const httpRequestDuration = new client.Histogram({
  name: "notification_service_http_request_duration_seconds",
  help: "HTTP request duration in seconds",
  labelNames: ["method", "route", "status_code"],
  buckets: [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10],
  registers: [register],
});

const httpRequestsTotal = new client.Counter({
  name: "notification_service_http_requests_total",
  help: "Total number of HTTP requests",
  labelNames: ["method", "route", "status_code"],
  registers: [register],
});

const notificationsTotal = new client.Counter({
  name: "notification_service_notifications_total",
  help: "Notifications processed by channel and status",
  labelNames: ["channel", "status"],
  registers: [register],
});

const notificationLatency = new client.Histogram({
  name: "notification_service_notification_duration_seconds",
  help: "Notification send duration in seconds",
  labelNames: ["channel", "status"],
  buckets: [0.1, 0.25, 0.5, 1, 2, 5, 10],
  registers: [register],
});

const rabbitConnectionState = new client.Gauge({
  name: "notification_service_rabbitmq_connection_state",
  help: "RabbitMQ connection state (1 = connected, 0 = disconnected)",
  registers: [register],
});

const eventMessagesTotal = new client.Counter({
  name: "notification_service_event_messages_total",
  help: "Events consumed from RabbitMQ",
  labelNames: ["routing_key", "status"],
  registers: [register],
});

const throttleActiveGauge = new client.Gauge({
  name: "notification_service_throttle_active_requests",
  help: "Active requests currently processed",
  registers: [register],
});

const throttleQueueGauge = new client.Gauge({
  name: "notification_service_throttle_queue_length",
  help: "Queued requests awaiting processing",
  registers: [register],
});

const throttleOverloadGauge = new client.Gauge({
  name: "notification_service_throttle_overloaded",
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

function recordNotificationAttempt(channel, status, count = 1) {
  notificationsTotal.inc(
    {
      channel: channel || "unknown",
      status,
    },
    count,
  );
}

function observeNotificationLatency(channel, status, seconds) {
  notificationLatency.labels(channel || "unknown", status).observe(seconds);
}

function setRabbitConnectionState(isConnected) {
  rabbitConnectionState.set(isConnected ? 1 : 0);
}

function recordEventMessage(routingKey, status) {
  eventMessagesTotal.inc({
    routing_key: routingKey || "unknown",
    status,
  });
}

export {
  metricsMiddleware,
  metricsHandler,
  recordNotificationAttempt,
  observeNotificationLatency,
  recordEventMessage,
  setRabbitConnectionState,
};
