## Multi-stage build for Azure DevOps MCP Server (.NET 9)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project for layer caching
COPY AzDevOpsMcp.sln ./
COPY AzDevOpsMcp/AzDevOpsMcp.csproj AzDevOpsMcp/

# Restore
RUN dotnet restore AzDevOpsMcp/AzDevOpsMcp.csproj

# Copy source
COPY . .

# Publish
RUN dotnet publish AzDevOpsMcp/AzDevOpsMcp.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Defaults; override at runtime
ENV AZDO_ORG_URL="" \
    AZDO_PAT="" \
    AZDO_PROJECT="" \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/out .

# MCP communicates over stdio; keep foreground
ENTRYPOINT ["dotnet", "AzDevOpsMcp.dll"]
