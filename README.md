# Runewire

Runewire is a process injection lab platform. It is for controlled, repeatable experiments on systems you own or are allowed to test. It is not a malware builder and not an AV bypass toolkit.

The platform is split into clear layers so it stays testable and easy to extend as we add more techniques, agents, and front ends.

## What it does

- Recipe driven: YAML or JSON recipes describe what to inject, where, how, and under which safety rules.
- Orchestrated: The orchestrator turns recipes into injection requests and talks to pluggable engines (dry-run or native).
- Preflight: Validation plus target/payload checks before any engine call. JSON output is available for automation.
- Cross platform mindset: Windows first, with Linux and macOS on the roadmap. Native interop uses a narrow C ABI.
- Tooling ready: CLI today, Studio/Server/Agent later. Shared services keep behavior consistent across hosts.
- Native split: The .NET side looks for the native injector via env vars `RUNEWIRE_INJECTOR_PATH` (full path) or `RUNEWIRE_INJECTOR_DIR` (directory). This keeps the native repo/builds separate from the managed solution.
  - MSBuild will copy a native injector binary into the output if it finds `Runewire.Injector.dll` / `libRunewire.Injector.(so|dylib)` under `native/bin/<rid>` or a custom `NativeInjectorDir`.

## Projects

- `src/Runewire.Domain` — pure domain types and validation logic (no IO, no interop).
- `src/Runewire.Orchestrator` — orchestration abstractions and request/response shapes.
- `src/Runewire.Orchestrator.Infrastructure` — native interop, preflight, engines, shared services.
- `src/Runewire.Core.Infrastructure` — recipe loading (YAML/JSON), technique registries, validator factory.
- `src/Runewire.Cli` — CLI front end using System.CommandLine.
- `src/Runewire.Testing` — shared test helpers.
- `native/Runewire.Injector` — C++20 native injector (CMake).
- Tests live under `tests/` mirroring the project structure.

## Commands (CLI)

```bash
# Validate a recipe (YAML or JSON)
dotnet run --project src/Runewire.Cli -- validate demo-recipe.yaml

# Preflight only (validation + target/payload checks, no engine)
dotnet run --project src/Runewire.Cli -- preflight demo-recipe.yaml

# Execute (dry-run by default)
dotnet run --project src/Runewire.Cli -- run demo-recipe.yaml

# Native engine
dotnet run --project src/Runewire.Cli -- run --native demo-recipe.yaml

# Preflight only (no engine) to check validation + target/payload readiness
dotnet run --project src/Runewire.Cli -- preflight demo-recipe.yaml

# Machine-readable JSON output
dotnet run --project src/Runewire.Cli -- validate demo-recipe.json --json
dotnet run --project src/Runewire.Cli -- run demo-recipe.yaml --json
dotnet run --project src/Runewire.Cli -- preflight demo-recipe.yaml --json

# List built-in techniques
dotnet run --project src/Runewire.Cli -- techniques
```

## Architecture rules

- Domain stays pure: no file IO, no console, no P/Invoke, no DI containers.
- Orchestrator coordinates domain types and engines; no platform-specific code there.
- Infrastructure hosts platform-specific bits, interop, and recipe loading.
- Front ends (CLI/Studio/Server/Agent) call into orchestrator and domain, not the other way around.
- Keep safety guardrails in infrastructure (preflight, payload existence/permissions, target reachability).

JSON responses include version metadata and a preflight summary (target success/errors, payload success/errors, architecture hints).

### JSON output shape (CLI)

Validate / Preflight (success):
```json
{
  "status": "valid",
  "recipeName": "demo-recipe",
  "meta": { "version": "1.0.0.0" },
  "preflight": {
    "targetSuccess": true,
    "targetErrors": [],
    "payloadSuccess": true,
    "payloadErrors": [],
    "payloadArchitecture": "X64",
    "processArchitecture": "X64"
  }
}
```

Validate / Preflight (invalid):
```json
{
  "status": "invalid",
  "meta": { "version": "1.0.0.0" },
  "errors": [
    { "code": "TARGET_PID_NOT_FOUND", "message": "Process with ID 999999 was not found." }
  ]
}
```

Run (success):
```json
{
  "status": "succeeded",
  "recipeName": "demo-recipe",
  "engine": "dry-run",
  "meta": { "version": "1.0.0.0" },
  "preflight": { ...same as above... },
  "result": {
    "success": true,
    "errorCode": null,
    "errorMessage": null,
    "startedAtUtc": "2025-01-01T00:00:00Z",
    "completedAtUtc": "2025-01-01T00:00:00Z"
  }
}
```

Run (failed):
```json
{
  "status": "failed",
  "recipeName": "demo-recipe",
  "engine": "native",
  "meta": { "version": "1.0.0.0" },
  "preflight": { ... },
  "result": {
    "success": false,
    "errorCode": "NATIVE_INVOKE_FAILED",
    "errorMessage": "Runewire.Injector.dll not found",
    "startedAtUtc": "...",
    "completedAtUtc": "..."
  }
}
```

## Building and testing

```bash
dotnet build Runewire.sln
dotnet test Runewire.sln
```

## Samples

- `demo-recipe.yaml` — simple CreateRemoteThread DLL injection recipe.
- `demo-recipe.json` — same recipe in JSON form.

## Safety and intent

Runewire is for authorized security research, detection engineering, and reverse engineering labs. Use it only on systems you own or are allowed to test. Keep safety flags (consent, kernel drivers) intentional and do not remove preflight checks.
