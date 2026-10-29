# Image Converter

Ein lokales Windows-Tool zum Verkleinern, Zuschneiden, Konvertieren und Exportieren von Bildern.

## Warum ich das Projekt entwickelt habe

Für meine Website [nicoegerer.de](https://nicoegerer.de) habe ich ein Tool gebraucht, mit dem ich die Dateigrößen meiner Bilder reduzieren, eigene Presets für wiederkehrende Bildformate festlegen und die fertigen Dateien exportieren kann.

Dafür habe ich **Codex** genutzt und Image Converter gemeinsam mit ihm entwickelt. Die Idee und die Anforderungen kamen aus meinem eigenen Website-Workflow; Codex hat mich bei der Umsetzung unterstützt.

## Vorschau

![Nico Image Converter 1.1.1 mit Bildvorschau und Einstellungen für den WebP-Export](docs/images/app-overview.png)

Das Beispiel zeigt ein geladenes JPEG-Bild und die Einstellungen für den Export als WebP. Größe, Zuschnitt, Ausgabeformat und Qualität lassen sich für den jeweiligen Einsatzzweck anpassen.

## Funktionen

- **Dateigröße reduzieren:** Qualität und Kompression für den Export einstellen.
- **Zuschneiden und skalieren:** Bildausschnitt, Seitenverhältnis und Zielgröße festlegen oder die Originalgröße beibehalten.
- **Presets nutzen:** Wiederkehrende Einstellungen schnell anwenden, zum Beispiel Portfolio Card, Web Hero oder Original → WebP.
- **Formate konvertieren:** JPEG, PNG, WebP, HEIC/HEIF, AVIF, TIFF und BMP einlesen; unter anderem als WebP, JPEG, PNG oder AVIF exportieren.
- **Mehrere Bilder verarbeiten:** Das aktuelle Bild, eine Auswahl oder alle geladenen Bilder exportieren.
- **Dateien umbenennen:** Neue Dateinamen vorab prüfen und mehrere Bilder gemeinsam umbenennen.
- **Transparenz und Metadaten steuern:** Transparenz erhalten oder einen Hintergrund wählen sowie EXIF- und GPS-Daten beim Export behalten oder entfernen.
- **Lokal arbeiten:** Die Bildverarbeitung findet vollständig auf dem eigenen Rechner statt.
- **Updates installieren:** Neue Versionen auf Wunsch direkt aus der App herunterladen und per SHA-256-Prüfsumme verifizieren.

## Installation

**Voraussetzung:** Windows 11 x64.

1. Die [aktuelle Version unter GitHub Releases](https://github.com/nicoegerer/ImageConverter/releases/latest) öffnen.
2. Unter **Assets** die Datei **ImageConverter-Setup.exe** herunterladen.
3. Den Installer ausführen und Image Converter anschließend über das Startmenü starten.

Der Installer installiert die Anwendung für den aktuellen Benutzer nach `%LOCALAPPDATA%\Programs\Image Converter`. Eine Desktop-Verknüpfung kann optional erstellt werden.

Die Anwendung bringt ihre benötigten Laufzeitdateien mit. Für die Nutzung müssen weder .NET noch Visual Studio oder Python separat installiert werden.

## Entwicklung

Die Anwendung ist mit **C#, .NET 8 und WinUI 3** umgesetzt.

### Voraussetzungen

- Windows 11 x64
- .NET SDK **9.0.307** oder eine gemäß [global.json](global.json) kompatible Version
- Optional: **Inno Setup 6**, um den Installer zu erstellen

### Projekt herunterladen

```powershell
git clone https://github.com/nicoegerer/ImageConverter.git
cd ImageConverter
```

### Bauen und testen

```powershell
dotnet restore ImageConverter.sln -p:Platform=x64
dotnet build ImageConverter.sln -c Release -p:Platform=x64 --no-restore
dotnet test tests/ImageConverter.Core.Tests/ImageConverter.Core.Tests.csproj -c Release -p:Platform=x64 --no-build --no-restore
```

### Release erstellen

Ein vollständiger Release-Build einschließlich Installer:

```powershell
.\scripts\build-release.ps1 -Version 1.1.1
```

Ohne lokal installiertes Inno Setup:

```powershell
.\scripts\build-release.ps1 -Version 1.1.1 -SkipInstaller
```

Die Versionsnummer kann für einen neuen Release angepasst werden. Die Programmdateien liegen anschließend unter `artifacts\publish`, die Release-Dateien unter `artifacts\release`.

### Projektstruktur

```text
src/ImageConverter.App/          WinUI-3-Oberfläche und ViewModels
src/ImageConverter.Core/         Modelle und lokale Anwendungsdienste
tests/ImageConverter.Core.Tests/ Automatisierte Tests
installer/                      Inno-Setup-Definition
scripts/                        Release-Build
docs/                           Architektur- und Testdokumentation
.github/workflows/              CI- und Release-Pipeline
```

Weitere Informationen: [Architektur](docs/ARCHITECTURE.md) · [Tests](docs/TESTING.md) · [Beitragen](CONTRIBUTING.md)

## Datenschutz und Sicherheit

Bilder werden lokal verarbeitet und für die Bearbeitung nicht auf einen Server hochgeladen. Im normalen Betrieb sind weder ein Benutzerkonto noch eine Internetverbindung erforderlich; für die Update-Funktion wird GitHub kontaktiert.

Sicherheitsprobleme können gemäß [SECURITY.md](SECURITY.md) gemeldet werden.

## Lizenz

Der eigene Quellcode steht unter der [MIT-Lizenz](LICENSE). Hinweise zu den verwendeten Abhängigkeiten stehen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
