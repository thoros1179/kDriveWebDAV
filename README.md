# kDriveWebDav

A .NET 10 CLI that provides a **WebDAV server** wrapping the [Infomaniak kDrive REST API](https://developer.infomaniak.com/docs/api). It supports **multi-tenancy** — you can configure multiple kDrive accounts and access each one as a separate directory under a single WebDAV root.

> **Scope:** This tool was developed for use in a private home network together with a Synology NAS. It runs over plain **HTTP** — HTTPS has not been tested. It is not intended to be exposed to the public internet.

## Features

- WebDAV 1 & 2 compliant server powered by [NWebDav.Server](https://github.com/ramondeklein/nwebdav)
- Full multi-tenancy: each account is exposed as `http://<host>:<port>/<account-name>/`
- Supports PROPFIND, GET, PUT, MKCOL, DELETE, COPY, MOVE, LOCK, UNLOCK
- Account credentials stored in a local JSON configuration file
- ASP.NET Core / Kestrel HTTP host
- Optional request/response logging for diagnostics (`--verbose`)

## Tested clients

| Client | Status |
|--------|--------|
| Synology CloudSync | ✅ working |
| Synology HyperBackup | ✅ working |

Other WebDAV clients may work as well but have not been tested. If you encounter issues, run the server with `--verbose` to inspect the HTTP traffic.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build
```

## Usage

### Start the WebDAV server

```bash
dotnet run --project src/kDriveWebDav -- start [options]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--port` / `-p` | `8080` | TCP port to listen on |
| `--host` / `-H` | `localhost` | Host/IP address to bind |
| `--config` / `-c` | `~/.config/kdrive-webdav/accounts.json` | Path to accounts config file |
| `--verbose` / `-v` | off | Log every WebDAV request and response to stdout |

### Diagnostics / verbose logging

Start the server with `--verbose` to print every request and response to stdout:

```bash
kdrive-webdav start --port 8080 --verbose
```

Example output:

```
→ PROPFIND  /myaccount/ [Depth:0]
← PROPFIND  207  /myaccount/
→ PUT       /myaccount/backup/data.bin [CL:1048576]
← PUT       200  /myaccount/backup/data.bin
```

This is useful when connecting a new client and diagnosing unexpected failures.

### Manage accounts

#### Add / update an account

```bash
dotnet run --project src/kDriveWebDav -- account add \
  --name myaccount \
  --token <infomaniak-api-token> \
  --drive-id <numeric-kdrive-id>
```

After adding the account, the drive is accessible at `http://localhost:8080/myaccount/`.

#### List accounts

```bash
dotnet run --project src/kDriveWebDav -- account list
```

#### Remove an account

```bash
dotnet run --project src/kDriveWebDav -- account remove --name myaccount
```

## Multi-tenancy example

```bash
# Add two kDrive accounts
kdrive-webdav account add --name alice --token <alice-token> --drive-id 1001
kdrive-webdav account add --name bob   --token <bob-token>   --drive-id 1002

# Start server
kdrive-webdav start --port 8080

# Now accessible at:
#   http://localhost:8080/alice/  ← Alice's kDrive
#   http://localhost:8080/bob/    ← Bob's kDrive
```

## WebDAV URL structure

```
http://<host>:<port>/                  ← root (lists all accounts)
http://<host>:<port>/<account>/        ← root of that account's kDrive
http://<host>:<port>/<account>/<path>  ← files/folders inside that drive
```

## Configuration file format

Accounts are stored as JSON (default: `~/.config/kdrive-webdav/accounts.json`):

```json
[
  {
    "Name": "alice",
    "Token": "your-infomaniak-api-token",
    "DriveId": 1001
  }
]
```

## Scope & contributions

This project was built for a specific personal use case and is published as-is under the MIT licence.

- **No support** — issues and questions will not be answered.
- **No roadmap** — there are no plans to extend the tool for scenarios beyond the original use case.
- **No pull requests** — external code contributions are not accepted.

Feel free to fork the repository and adapt it to your own needs.

## Project structure

```
src/kDriveWebDav/
├── Program.cs                  CLI entry point (System.CommandLine)
├── Config/
│   ├── AccountConfig.cs        Account model
│   └── ConfigManager.cs        JSON config read/write
├── KDrive/
│   ├── KDriveApiClient.cs      Infomaniak kDrive REST API client
│   └── Models/
│       ├── KDriveFile.cs       File/directory DTO
│       └── ApiResponse.cs      API response envelope
└── WebDav/
    ├── AspNetCoreHttpContext.cs NWebDav ↔ ASP.NET Core adapter
    ├── MultiTenantStore.cs     IStore – URI → store item resolution
    ├── RootCollection.cs       Virtual root listing all accounts
    ├── KDriveCollection.cs     IStoreCollection wrapping kDrive dirs
    └── KDriveDocument.cs       IStoreItem wrapping kDrive files
```
