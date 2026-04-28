# Release / Setup Build

Short guide for creating a new setup build and GitHub release.

## GitHub Release with MSI/ZIP

The workflow is located at `.github/workflows/build-and-release.yml`.

It reacts to:
- pushes to `main`
- pull requests
- tags starting with `v`
- manual runs via `workflow_dispatch`

Important:
- For a real GitHub Release, push a new tag starting with `v`.
- RC tags are valid, for example `v1.0.0.0-rc2`.
- Do not reuse the same tag. Create a new tag for every new release build.

Example:

```powershell
git checkout main
git pull
git tag v1.0.0.0-rc2
git push origin main
git push origin v1.0.0.0-rc2
```

GitHub will then create:
- MSI
- ZIP
- checksums
- GitHub Release with attached files

## Run workflow manually

If you only need a build/artifact:

1. Open GitHub `Actions`
2. Select `Build And Release`
3. Click `Run workflow`

Note:
- This builds the release artifacts.
- The actual GitHub Release job only runs for tag pushes matching `v*`.

## Local release build

Full local release build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1
```

Setup project only:

```powershell
dotnet build XTouchVMBridge.Setup/XTouchVMBridge.Setup.wixproj -c Release
```

## Output locations

Typical artifacts:
- `artifacts/release/Release/`
- `XTouchVMBridge.Setup/bin/x64/Release/XTouchVMBridge.Setup.msi`

## Mini checklist

1. Commit changes
2. Optionally run `scripts/build-release.ps1` locally
3. Create a new `v...` tag
4. Push the tag
5. Verify GitHub Actions / release assets
