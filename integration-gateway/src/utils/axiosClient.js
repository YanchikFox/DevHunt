import axios from "axios";
import { observeOutgoingHttp } from "../metrics.js";

const client = axios.create();

client.interceptors.request.use((config) => {
  // Propagate traceparent if available
  if (config.traceparent) {
    config.headers["traceparent"] = config.traceparent;
  }
  return config;
});

client.interceptors.response.use(
  (response) => {
    const target = response.config?.url || "unknown";
    const status = response.status?.toString() || "0";
    const started = response.config?.metadata?.startTime;
    if (started) {
      const duration = (Date.now() - started) / 1000;
      observeOutgoingHttp(target, status, duration);
    }
    return response;
  },
  (error) => {
    const config = error.config || {};
    const target = config.url || "unknown";
    const status = (error.response?.status || 0).toString();
    const started = config.metadata?.startTime;
    if (started) {
      const duration = (Date.now() - started) / 1000;
      observeOutgoingHttp(target, status, duration);
    }
    return Promise.reject(error);
  }
);

// Attach metadata to measure duration
client.interceptors.request.use((config) => {
  config.metadata = { startTime: Date.now() };
  return config;
});

export { client as axiosClient };
