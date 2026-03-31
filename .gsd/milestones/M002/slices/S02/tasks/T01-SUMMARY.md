---
id: T01
parent: S02
milestone: M002
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/Azure.InMemory.csproj", ".gsd/DECISIONS.md", ".gsd/KNOWLEDGE.md", ".gsd/milestones/M002/slices/S02/tasks/T01-SUMMARY.md"]
key_decisions: ["D039: keep an explicit `1.0.0` baseline package version in the library project so downstream local-feed proof can override it intentionally.", "Pack the authoritative root `README.md` directly into the package instead of maintaining a packaging-only duplicate document."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln`, `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`, and a direct `python3` package inspection against `./artifacts/pack/Azure.InMemory.1.0.0.nupkg`. All checks passed. The package inspection confirmed `README.md` was included and the generated nuspec contains `<readme>README.md</readme>`, `<license type="expression">MIT</license>`, and `https://github.com/Forgate-Labs/Azure.InMemory.git`."
completed_at: 2026-03-31T03:17:35.804Z
blocker_discovered: false
---

# T01: Added intentional Azure.InMemory package metadata, packed the root README into the nupkg, and proved the 1.0.0 package artifact and solution regression locally.

> Added intentional Azure.InMemory package metadata, packed the root README into the nupkg, and proved the 1.0.0 package artifact and solution regression locally.

## What Happened
---
id: T01
parent: S02
milestone: M002
key_files:
  - src/Azure.InMemory/Azure.InMemory.csproj
  - .gsd/DECISIONS.md
  - .gsd/KNOWLEDGE.md
  - .gsd/milestones/M002/slices/S02/tasks/T01-SUMMARY.md
key_decisions:
  - D039: keep an explicit `1.0.0` baseline package version in the library project so downstream local-feed proof can override it intentionally.
  - Pack the authoritative root `README.md` directly into the package instead of maintaining a packaging-only duplicate document.
duration: ""
verification_result: passed
completed_at: 2026-03-31T03:17:35.806Z
blocker_discovered: false
---

# T01: Added intentional Azure.InMemory package metadata, packed the root README into the nupkg, and proved the 1.0.0 package artifact and solution regression locally.

**Added intentional Azure.InMemory package metadata, packed the root README into the nupkg, and proved the 1.0.0 package artifact and solution regression locally.**

## What Happened

Hardened `src/Azure.InMemory/Azure.InMemory.csproj` from a default class library into a deliberate internal package by adding explicit package identity and metadata: `PackageId`, `Version`, title, description, authors/company, tags, project URL, repository URL/type, package readme, and MIT license expression. Wired the authoritative root `README.md` into the package through the project file so the `.nupkg` carries a single-source package readme at `README.md`. Recorded D039 so the package now has an explicit `1.0.0` baseline version that downstream S03 proof can override intentionally instead of relying on default versioning or NuGet cache luck. Verified the full solution still passes, `dotnet pack` now emits `artifacts/pack/Azure.InMemory.1.0.0.nupkg` without the missing-readme warning, and direct package inspection confirmed the embedded readme plus intended nuspec metadata.

## Verification

Ran `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln`, `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`, and a direct `python3` package inspection against `./artifacts/pack/Azure.InMemory.1.0.0.nupkg`. All checks passed. The package inspection confirmed `README.md` was included and the generated nuspec contains `<readme>README.md</readme>`, `<license type="expression">MIT</license>`, and `https://github.com/Forgate-Labs/Azure.InMemory.git`.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 4302ms |
| 2 | `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` | 0 | ✅ pass | 2425ms |
| 3 | `python3 -c "import zipfile, pathlib; p = pathlib.Path('./artifacts/pack/Azure.InMemory.1.0.0.nupkg'); z = zipfile.ZipFile(p); names = z.namelist(); assert any(name.endswith('README.md') for name in names); nuspec_name = next(name for name in names if name.endswith('.nuspec')); nuspec = z.read(nuspec_name).decode(); assert '<readme>README.md</readme>' in nuspec; assert '<license type=\"expression\">MIT</license>' in nuspec; assert 'https://github.com/Forgate-Labs/Azure.InMemory.git' in nuspec"` | 0 | ✅ pass | 64ms |


## Deviations

Used `python3` instead of the slice-plan `python` spelling for package inspection because this WSL environment does not provide a `python` alias. No product-scope deviation was required.

## Known Issues

None.

## Files Created/Modified

- `src/Azure.InMemory/Azure.InMemory.csproj`
- `.gsd/DECISIONS.md`
- `.gsd/KNOWLEDGE.md`
- `.gsd/milestones/M002/slices/S02/tasks/T01-SUMMARY.md`


## Deviations
Used `python3` instead of the slice-plan `python` spelling for package inspection because this WSL environment does not provide a `python` alias. No product-scope deviation was required.

## Known Issues
None.
