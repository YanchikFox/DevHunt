# Shared edge hardening (included from server blocks in nginx-http-only.conf and nginx.conf)

client_max_body_size 25m;
client_body_timeout 30s;
client_header_timeout 30s;
send_timeout 30s;
keepalive_timeout 65s;

proxy_connect_timeout 10s;
proxy_send_timeout 60s;
proxy_read_timeout 60s;

add_header X-Frame-Options "DENY" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;
# Baseline CSP at the edge; tighten further via Next.js headers when nonce pipeline exists.
add_header Content-Security-Policy "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; font-src 'self' data:; connect-src 'self' https: wss:; object-src 'none'; upgrade-insecure-requests" always;
