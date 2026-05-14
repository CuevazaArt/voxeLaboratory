# Headless test harness

The Unity Editor is not required to validate the pure‑C# parts of the
voxel engine.  This project compiles every `.cs` file under
`Assets/Scripts/**` against the .NET SDK and runs the NUnit tests in
this directory.

Run locally:

```bash
dotnet test Tests/Headless/VoxeLaboratory.Headless.Tests.csproj -c Release
```

The same command runs in CI on every pull request and on every push to
`main` (see `.github/workflows/ci.yml`).

## Why this works

All modules under `Assets/Scripts/` that are covered by the headless
build set `"noEngineReferences": true` in their `.asmdef`.  They contain
pure C# without any `UnityEngine` dependency, so they compile and run
identically inside Unity and inside `dotnet test`.  Modules that need
`UnityEngine`/`UnityEditor` (e.g. future `MeshingGPU` glue or
`EditorTools`) must live outside the headless compile glob in their own
folder and use a separate Unity‑only assembly definition.
