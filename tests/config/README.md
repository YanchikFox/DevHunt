# Configuration Tests

Shell scripts that verify security properties of config files (not integration tests against running services).

## Running

```bash
chmod +x tests/config/*.sh
./tests/config/docker-compose-monitoring.test.sh   # checks read_only, tmpfs, security_opt
./tests/config/nginx-security.test.sh              # checks H2C protection, TLS, security headers
```

These run in CI on every PR that touches `nginx/nginx.conf` or `docker-compose.monitoring.yml`.
