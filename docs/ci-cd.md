CI/CD (GitHub Actions → Docker Hub)

Workflow that automatically publishes the image to Docker Hub when pushing to the `main` branch.

- Workflow: `.github/workflows/dockerhub-publish.yml`
- Event: push to `main`
- Tags published:
  - `latest` (default branch only)
  - `sha-<commit>` (commit SHA tag)
- Platforms: `linux/amd64`, `linux/arm64`
- Cache: enabled via `cache-from/cache-to` (GHA)

Required repository secrets (Settings → Secrets and variables → Actions)
- `DOCKERHUB_USERNAME`: Docker Hub username (e.g., `medeiroshudson`)
- `DOCKERHUB_TOKEN`: Docker Hub access token (create at hub.docker.com → Security → New Access Token)
