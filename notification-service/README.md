# DevHunt Notification Service

Express 5 (Node.js) service that sends email, SMS, and push notifications. Also handles incoming alert webhooks from OpenObserve. Consumes `devhunt.notifications` events from RabbitMQ.

## Local development

```bash
docker compose up notification-service
```

Health: `http://localhost:5003/health`

SMTP/SendGrid/Twilio/FCM credentials are optional locally — the service starts without them; delivery will just fail for those channels.
