Docker Hub

Useful commands to build and publish the Azure DevOps MCP image to Docker Hub.

- Login: `docker login -u <username>`
- Local build: `docker build -t medeiroshudson/azdevops-mcp:latest .`
- Optional tag: `docker tag medeiroshudson/azdevops-mcp:latest medeiroshudson/azdevops-mcp:v1.0.0`
- Push latest: `docker push medeiroshudson/azdevops-mcp:latest`
- Push version: `docker push medeiroshudson/azdevops-mcp:v1.0.0`
- Run (stdio):
  - `docker run -i --rm \
     -e AZDO_ORG_URL="https://dev.azure.com/<org>" \
     -e AZDO_PAT="<pat>" \
     -e AZDO_PROJECT="<project_name>" \
     medeiroshudson/azdevops-mcp:latest`
