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
  - List Work Item Types (optional `project`)
    - Returns the exact type names available for the target project's process
  - Create Work Item (optional `project`)
    - MCP-friendly parameters: `type`, `fields`, optional `parentId`, optional `validateOnly`
    - Resolves generic backlog aliases like `User Story` to the project's actual requirement type when needed
  - Update Work Item (optional `project`)
    - MCP-friendly parameters: `id`, optional `fields`, optional `history`, optional `parentId`, optional `rev`, optional `validateOnly`
    - Supports optimistic concurrency with `rev` and idempotent parent assignment

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

## Mutation tools

### List valid types first

Use `ListWorkItemTypes` to discover the exact work item type names available in the target project.

### Create a work item

Minimal example:

```json
{
  "project": "WAPP",
  "type": "User Story",
  "fields": {
    "System.Title": "Criar integração com VAN X",
    "System.Description": "Criada via MCP",
    "Microsoft.VSTS.Common.Priority": 1
  },
  "parentId": 12345
}
```

Notes:
- `fields` must be a JSON object using Azure DevOps field reference names.
- `System.Title` is required by Azure DevOps when creating a work item.
- `parentId` creates a `System.LinkTypes.Hierarchy-Reverse` relation using the canonical parent work-item URL.
- If the project does not use Agile, a generic request like `User Story` is resolved to the project's requirement/backlog type when possible.
- Use `validateOnly=true` to preflight the payload without saving.

### Update a work item

Example:

```json
{
  "id": 12345,
  "project": "WAPP",
  "fields": {
    "System.Title": "Novo título",
    "System.State": "Active"
  },
  "history": "Atualizado via MCP",
  "parentId": 10000,
  "rev": 7
}
```

Notes:
- Provide at least one field, a history entry, or `parentId`.
- `rev` adds a `test /rev` patch operation to avoid overwriting newer revisions.
- Reapplying the same `parentId` is idempotent. Assigning a different parent is rejected instead of implicitly reparenting the work item.
- Parent links are relations; do not send `System.Parent` in `fields`.
- Error messages are sanitized and designed to point to the likely cause: invalid type, missing required fields, invalid values, or missing permissions.

## Getting Started

### Prerequisites
- .NET 10 SDK (stdio) or Docker
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
      "command": "/full/path/to/src/AzDevOpsMcp/bin/Debug/net10.0/AzDevOpsMcp",
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
      "command": "/full/path/to/src/AzDevOpsMcp/bin/Debug/net10.0/AzDevOpsMcp",
      "env": {
        "AZDO_PAT": "<your_pat>",
        "AZDO_PROJECT": "<project_name>",
        "AZDO_ORG_URL": "https://dev.azure.com/<org>"
      }
    }
  }
}
```

OpenAI Codex (GitHub Container Registry)
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
  "ghcr.io/medeiroshudson/mcp-devops:main"
]
```

Claude Desktop/Claude Code and VS Code can use the same `docker run` command with a stdio MCP configuration:

```json
{
  "mcpServers": {
    "az-devops": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm",
        "-e", "AZDO_PAT=<your_pat>",
        "-e", "AZDO_PROJECT=<project_name>",
        "-e", "AZDO_ORG_URL=https://dev.azure.com/<org>",
        "ghcr.io/medeiroshudson/mcp-devops:main"
      ]
    }
  }
}
```

Notes
- Ensure the PAT scopes align with the tools you plan to use.
- For on-prem TFS/Azure DevOps Server, set `AZDO_ORG_URL` to your collection URL.
- Default project resolution precedence is: explicit tool `project` parameter -> `AZDO_PROJECT` -> startup argument `--project`.
- If `AZDO_PROJECT` or `--project` is configured, project-aware tools automatically target that project when `project` is omitted.

Additional Docs
- Docker: see `docs/docker.md`
- CI/CD (GitHub Actions → GitHub Container Registry): see `docs/ci-cd.md`
