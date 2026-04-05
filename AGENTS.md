# kDriveWebDav – Project Guidelines

## Overview

.NET 10 CLI that exposes Infomaniak kDrive accounts as a WebDAV server. Designed for use in a private home network together with a Synology NAS. Uses plain HTTP — HTTPS is untested and out of scope.

## Build & Run

```bash
dotnet build src/kDriveWebDav/kDriveWebDav.csproj --configuration Debug
dotnet run --project src/kDriveWebDav -- start --port 8080 --verbose
```

No test project exists yet. After any change, build must succeed with 0 errors and 0 warnings.

## Architecture

```
Program.cs                  CLI entry (System.CommandLine) – start + account commands
Config/                     JSON account config read/write (~/.config/kdrive-webdav/accounts.json)
KDrive/                     Infomaniak REST API client + DTOs
WebDav/
  AspNetCoreHttpContext.cs  NWebDav ↔ ASP.NET Core adapter
  MultiTenantStore.cs       IStore – resolves URI to per-account collection
  RootCollection.cs         Virtual root – lists all configured accounts
  KDriveCollection.cs       IStoreCollection wrapping a kDrive directory
  KDriveDocument.cs         IStoreItem wrapping a kDrive file
```

The first URL path segment is always the account name (`/Ron/...`). `MultiTenantStore` routes from there.

## Key Conventions

- `NWebDav.Server` version **0.1.36** — `DavStatusCode` enum is incomplete; use `(DavStatusCode)405` etc. for missing values rather than relying on enum names.
- MKCOL on an existing collection must return **405** (not 412) per RFC 4918 §9.3.1.
- PROPFIND with no body must be treated as `allprop` — handled in `AspNetCoreHttpContext.Stream`.
- Logging middleware is opt-in via `--verbose` / `-v` on the `start` command.
- No authentication is implemented — rely on network-level access control.

## Dependencies

| Package | Version | License |
|---------|---------|---------|
| `NWebDav.Server` | 0.1.36 | MIT |
| `System.CommandLine` | 2.0.0-beta4 | MIT |
| ASP.NET Core (via SDK) | 10.0 | MIT |

## Tested Clients

- Synology CloudSync ✅
- Synology HyperBackup ✅

Other WebDAV clients are out of scope. The codebase is a MIT-licensed baseline — adapt as needed.
