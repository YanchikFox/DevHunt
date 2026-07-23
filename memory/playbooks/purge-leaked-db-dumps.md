# Purge leaked database dumps from git history

If `backup.sql` or `backup_clean.sql` were ever committed, deleting them in a normal commit **does not** remove PII from git history.

## Immediate response

1. Rotate every credential that appeared in the dumps (DB passwords, user password hashes, reset tokens, API keys).
2. Remove tracked dumps: `git rm --cached backup.sql backup_clean.sql` (files are gitignored).
3. Run a full secret scan without dump exclusions (`.trufflehogignore` no longer lists those files).

## History rewrite (maintainer only)

Use [git-filter-repo](https://github.com/newren/git-filter-repo) or BFG Repo Cleaner, then force-push all branches and tags after team coordination:

```bash
pip install git-filter-repo
git filter-repo --path backup.sql --path backup_clean.sql --invert-paths
```

After rewrite: invalidate clones, re-run TruffleHog on full history, and assess breach notification if the repo was public.
