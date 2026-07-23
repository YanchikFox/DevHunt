# SeaweedFS (Object Storage)

S3-compatible blob storage for user avatars, project files, and task attachments. Runs as the `object-storage` service in docker-compose.

## Local development

1. Generate IAM config from `.env` (file is gitignored):

   ```bash
   ./scripts/seaweedfs/generate-s3-config.sh
   ```

2. Start storage:

   ```bash
   docker compose up object-storage
   ```

S3 API: `http://localhost:8333`  
Filer UI: `http://localhost:8888`

See `s3.json.example` for the schema. Credentials must match `OBJECT_STORAGE_ACCESS_KEY` / `OBJECT_STORAGE_SECRET_KEY` in `.env.example`.

Optional: `init-s3.sh` uploads IAM config to a running filer when not using the mounted `s3.json` volume.
