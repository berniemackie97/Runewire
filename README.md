# Runewire

A programmable, testable orchestration layer for **Windows process injection experiments**.

Runewire is designed for:

- Authorized security research
- Detection engineering
- Reverse engineering on systems you own or are authorized to test

It is **not** a malware builder or an "AV bypass" toolkit.

## Projects

- `src/Runewire.Core` - domain model + orchestration abstractions.
- `src/Runewire.Cli` - CLI front-end for running recipes.
- `tests/Runewire.Core.Tests` - xUnit tests for the core domain.
- `native/Runewire.Injector` - C++20 native injection engine (CMake-based).

CLI quick start:

```bash
dotnet run --project src/Runewire.Cli -- validate demo-recipe.yaml   # validate a recipe
dotnet run --project src/Runewire.Cli -- run demo-recipe.yaml        # dry-run execution
dotnet run --project src/Runewire.Cli -- validate demo-recipe.json   # JSON works too
dotnet run --project src/Runewire.Cli -- techniques                  # list built-in techniques
dotnet run --project src/Runewire.Cli -- run demo-recipe.yaml --json # machine-readable output
```

## Building

```bash
dotnet build Runewire.sln
```
