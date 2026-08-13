# Teststrategie

## Automatisierte Tests

Die Core-Test-Suite deckt unter anderem ab:

- Crop-Bewegung in alle Richtungen
- absoluten und reversiblen Zoom
- Wechsel des Zielseitenverhältnisses
- exakten Crop/Export mit 600 × 1050 Pixeln
- Resize, Fit/Contain, Format-only und Schutz vor Hochskalierung
- WebP-, JPEG-, PNG- und AVIF-Export
- echte HEIC- und HEIF-Dekodierung
- Batch-Planung und fortlaufende Dateinamenskollisionen
- Rename-Vorschau, Sortierung und zweiphasiges Umbenennen
- Settings-Persistenz und Codec-Verfügbarkeit

Die HEIC-Testdatei stammt aus dem MIT-lizenzierten Beispielbestand des
`libheif`-Projekts. Herkunft und Lizenz sind direkt neben dem Fixture dokumentiert.

```powershell
dotnet test tests/ImageConverter.Core.Tests/ImageConverter.Core.Tests.csproj `
  -c Release -p:Platform=x64 --logger "trx;LogFileName=tests.trx"
```

## Manueller Windows-Test

Vor einem Release sollte die veröffentlichte Anwendung mindestens gegen folgende
Matrix geprüft werden:

1. JPG, PNG, WebP, HEIC und HEIF importieren
2. Crop mit Maus nach links, rechts, oben und unten bewegen
3. Zoom auf Minimum, Maximum und zurück auf 100 Prozent stellen
4. Zielgröße und Seitenverhältnis mehrfach ändern
5. WebP, JPEG, PNG und AVIF exportieren
6. Format-only ohne geänderte Pixelabmessungen prüfen
7. mehrere Bilder mit individuellem Crop im Batch exportieren
8. Rename-Vorschau und Kollisionen prüfen
9. lange Dateinamen in Light und Dark Mode prüfen
10. Diagnosebericht eines absichtlich fehlerhaften Imports prüfen

## Isolierter Smoke-Test

Für einen zusätzlichen Starttest kann die self-contained Publish-Ausgabe in
Windows Sandbox schreibgeschützt eingebunden werden. Der Test prüft, dass der
Prozess ein sichtbares Hauptfenster mit dem erwarteten Titel erzeugt, offen bleibt
und eine echte HEIC-Datei importieren kann. Die Sandbox ersetzt keine Tests der
Installation auf einem verwalteten Zielsystem, reduziert aber das Risiko beim
ersten Start eines neu erzeugten Builds.

## Installer-Test vor Veröffentlichung

1. `ImageConverter-Setup.exe` in einer sauberen Windows-11-VM oder Sandbox starten.
2. Per-user installieren und den Start nach Setup prüfen.
3. App schließen und erneut über das Startmenü öffnen.
4. Export mit einer Testdatei durchführen.
5. unter „Installierte Apps“ deinstallieren.
6. Setup-, App- und Uninstall-Logs archivieren.

Dieser Ablauf ist getrennt vom erfolgreichen Kompilieren des Installers zu
dokumentieren. Ein Build-Erfolg allein gilt nicht als Installationstest.
