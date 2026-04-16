# Azure DevOps MCP Server

This is a .NET console application that implements a Model Context Protocol (MCP) server exposing Azure DevOps operations via the official Azure DevOps .NET client libraries.

## Features
- Connects with `AZDO_ORG_URL` and `AZDO_PAT` environment variables.
- Optional default project via `AZDO_PROJECT` or `--project`.
- MCP Tools:
  - List Projects
  - List Repositories (optional `project`; uses default if set)
  - List Build Definitions (optional `project`)
  - Queue Build (optional `project`)
  - WIQL Query (optional `project`)
    - Parameters: `fieldsCsv` (fields), `includeRelations` (bool), `skip`/`pageSize` for paging; `top` default 50.
    - Default response is compact and includes `totalMatched`, `returned`, `skip`, `pageSize`, `nextSkip`, `requestedFields`, `includeRelations`, `truncated`, `warnings`, and `workItems`.
    - `top` and `pageSize` are capped at 200 to stay within Azure DevOps work item batch limits.
  - My Work Items (optional `project`)
    - Safe convenience query using `@Project` and `@Me`
    - Optional `state`, `fieldsCsv`, `includeRelations`, `skip`, `pageSize`, `top`
  - My User Stories (optional `project`)
    - Safe convenience query using `@Project`, `@Me`, and `System.WorkItemType = 'User Story'`
    - Optional `state`, `fieldsCsv`, `includeRelations`, `skip`, `pageSize`, `top`

Notes:
- `top` limits the WIQL result window before local paging.
- `pageSize` and detail retrieval are capped at `200` to match Azure DevOps batch limits.
- `warnings` is used for normalized inputs, empty pages, truncated WIQL windows, non-flat queries, or omitted IDs.
- Non-flat WIQL queries return `relationReferences` and an empty `workItems` collection.

## Validation

```sh
dotnet build AzDevOpsMcp.sln
dotnet test AzDevOpsMcp.sln
```
  - Get Work Item
  - Create Work Item (optional `project`)
  - Update Work Item

## Getting Started

### Prerequisites
- .NET 9 SDK (stdio) or Docker
- Azure DevOps organization URL (e.g., https://dev.azure.com/<org>)
- Personal Access Token (PAT) with appropriate scopes (e.g., Project and Team, Work Items (Read/Write), Build (Read/Execute), Code (Read))

### Setup

1. **Build**

```
dotnet build AzDevOpsMcp.sln
```

2. **Run (stdio)**
```sh
export AZDO_PAT="<your_pat>"
export AZDO_ORG_URL="https://dev.azure.com/<org>"
export AZDO_PROJECT="<project_name>" # optional, sets default project
dotnet run --project src/AzDevOpsMcp/AzDevOpsMcp.csproj
```

VS Code MCP
```json
"mcp": {
  "servers": {
    "az-devops": {
      "type": "stdio",
      "command": "/full/path/to/src/AzDevOpsMcp/bin/Debug/net9.0/AzDevOpsMcp",
      "env": {
        "AZDO_PAT": "<your_pat>",
        "AZDO_PROJECT": "<project_name>",
        "AZDO_ORG_URL": "https://dev.azure.com/<org>"
      }
    }
  }
}
```

Claude Desktop/ Claude Code
```json
{
  "mcpServers": {
    "az-devops": {
      "command": "/full/path/to/src/AzDevOpsMcp/bin/Debug/net9.0/AzDevOpsMcp",
      "env": {
        "AZDO_PAT": "<your_pat>",
        "AZDO_PROJECT": "<project_name>",
        "AZDO_ORG_URL": "https://dev.azure.com/<org>"
      }
    }
  }
}
```

OpenAI Codex
```toml
[mcp_servers.az-devops]
command = "docker"
args = [
  "run", 
  "-i",
  "--rm",
  "--name", "az-devops",
  "-e", "AZDO_PAT=<your_pat>",
  "-e", "AZDO_PROJECT=<project_name>",
  "-e", "AZDO_ORG_URL=https://dev.azure.com/<org>",
  "medeiroshudson/azdevops-mcp:latest"
]
```

Notes
- Ensure the PAT scopes align with the tools you plan to use.
- For on-prem TFS/Azure DevOps Server, set `AZDO_ORG_URL` to your collection URL.

Additional Docs
- Docker: see `docs/docker.md`
- CI/CD (GitHub Actions → Docker Hub): see `docs/ci-cd.md`
