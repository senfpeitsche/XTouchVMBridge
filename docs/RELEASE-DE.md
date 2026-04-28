# Release / Setup Build

Kurzanleitung fuer einen neuen Setup-Build und GitHub-Release.

## GitHub Release mit MSI/ZIP

Der Workflow liegt in `.github/workflows/build-and-release.yml`.

Er reagiert auf:
- Push auf `main`
- Pull Requests
- Tags, die mit `v` beginnen
- manuellen Start ueber `workflow_dispatch`

Wichtig:
- Fuer einen echten GitHub Release muss ein neuer Tag gepusht werden, der mit `v` beginnt.
- Ein RC-Tag ist okay, z. B. `v1.0.0.0-rc2`.
- Nicht denselben Tag erneut verwenden. Fuer einen neuen Release-Build immer einen neuen Tag anlegen.

Beispiel:

```powershell
git checkout main
git pull
git tag v1.0.0.0-rc2
git push origin main
git push origin v1.0.0.0-rc2
```

Danach erstellt GitHub automatisch:
- MSI
- ZIP
- Checksummen
- GitHub Release mit Anhaengen

## Workflow manuell starten

Wenn nur ein Build/Artifact benoetigt wird:

1. GitHub `Actions` oeffnen
2. Workflow `Build And Release` auswaehlen
3. `Run workflow` klicken

Hinweis:
- Das baut die Release-Artefakte.
- Der eigentliche GitHub-Release-Job laeuft im Workflow nur bei Tag-Pushes `v*`.

## Lokaler Release-Build

Kompletter lokaler Release-Build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1
```

Nur das Setup-Projekt:

```powershell
dotnet build XTouchVMBridge.Setup/XTouchVMBridge.Setup.wixproj -c Release
```

## Ausgabeorte

Typische Artefakte:
- `artifacts/release/Release/`
- `XTouchVMBridge.Setup/bin/x64/Release/XTouchVMBridge.Setup.msi`

## Mini-Checkliste

1. Aenderungen committen
2. Optional lokal `scripts/build-release.ps1` ausfuehren
3. Neuen Tag `v...` anlegen
4. Tag pushen
5. GitHub Actions / Release Assets pruefen
