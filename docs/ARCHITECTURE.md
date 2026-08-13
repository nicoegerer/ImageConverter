# Architektur

## Überblick

Die Lösung trennt die native Windows-Oberfläche von der testbaren
Bildverarbeitungs- und Anwendungslogik.

```text
WinUI Views
    ↓ Bindings und UI-Ereignisse
ViewModels
    ↓ Anwendungsfälle
Core Services
    ↓
Magick.NET / Dateisystem / JSON-Settings / Logging
```

## Projekte

### ImageConverter.App

Unpackaged WinUI-3-Anwendung mit einem Hauptfenster. `MainWindow` koordiniert
Import, Auswahl, Tastenkürzel und Dialoge. `CropEditor` rendert eine bereits
herunterskalierte Preview und verändert während Drag und Zoom ausschließlich den
Crop-Zustand sowie das Overlay. Eine vollständige Dekodierung und hochwertige
Skalierung erfolgt erst im Exportdienst.

Die ViewModels halten die sichtbaren Zustände und pro Bild einen eigenen
`CropState`. Dadurch bleiben Position und Zoom beim Wechsel zwischen Dateien
erhalten.

### ImageConverter.Core

- `ImageLoaderService`: Metadaten lesen und Preview-/Thumbnail-Cache erzeugen
- `ImageProcessingService`: Crop, Resize, Format, Qualität und Metadaten anwenden
- `ExportService`: Batch-Jobs planen, parallelisiert ausführen und Fortschritt melden
- `ExportSizeEstimator`: Ausgabegröße ohne erneutes Encodieren bei jeder UI-Änderung schätzen
- `RenameService`: Sortierung, Vorschau, Kollisionserkennung und zweiphasiges Rename
- `SettingsService`: persistente JSON-Einstellungen
- `LoggingService` und `DiagnosticService`: strukturierte Fehler- und Umgebungsdaten
- `CapabilityService`: benötigte ImageMagick-Codecs beim Start prüfen

## Crop-Koordinaten

Der Crop wird normalisiert im Bereich `0..1` gespeichert. Ein positiver
Y-Unterschied der Maus erhöht die Y-Position des Crops; Ziehen nach unten bewegt
ihn deshalb auch sichtbar nach unten. Der Zoom-Wert beschreibt eine absolute
Skalierung und wird nicht wiederholt auf den bisherigen Zustand multipliziert.

Ändert sich das Zielseitenverhältnis, passt `CropState` den Rahmen an das neue
Verhältnis an und bewahrt Mittelpunkt sowie relative Vergrößerung soweit möglich.
Die eingegebene Zielauflösung bestimmt im Crop/Cover-Modus direkt das Verhältnis
des sichtbaren Rahmens.

## Daten und Datenschutz

Die Anwendung verwendet keine Netzwerk-API. Standardmäßig liegen lokale Daten
unter `%LOCALAPPDATA%\Image Converter`:

- `Cache`: wiederverwendbare Thumbnails und Previews
- `Logs`: technische Laufzeitfehler
- `Diagnostics`: ausführliche Diagnoseberichte
- `settings.json`: nicht vertrauliche Benutzereinstellungen

## Veröffentlichung

Der Publish ist `win-x64`, unpackaged und self-contained. WinUI/Windows App SDK,
.NET und native Bildcodecs werden gemeinsam in `artifacts\publish` ausgegeben.
`PublishSingleFile` bleibt absichtlich deaktiviert: native Bibliotheken und
Windows-App-SDK-Dateien werden als vollständige, überprüfbare Publish-Ausgabe
installiert.

Inno Setup erstellt daraus einen per-user installierbaren Setup-Assistenten mit
Startmenü-Eintrag, optionaler Desktop-Verknüpfung und Uninstaller. Temporäre
Build-Daten liegen außerhalb des Publish-Verzeichnisses.
