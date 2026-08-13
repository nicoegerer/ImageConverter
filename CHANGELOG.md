# Changelog

Alle wichtigen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.
Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/)
und die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

## [1.1.1] - 2026-08-13

### Changed

- Herstellerangabe in Anwendung und Installer auf Nico Egerer vereinheitlicht
- Repository-, Support- und Updateadressen in die Installer-Metadaten aufgenommen
- Produktbeschreibung, Copyright und Versionsinformationen vervollständigt

## [1.1.0] - 2026-08-13

### Added

- Export nur des aktuell geöffneten Bildes
- TIFF, BMP und GIF als zusätzliche Ausgabeformate
- Transparenz beibehalten oder auf weißem/schwarzem Hintergrund reduzieren
- Wiederherstellung von Bildern, Auswahl, Zuschnitten und letzter Arbeitsposition
- integrierte GitHub-Updateprüfung mit SHA-256-verifiziertem Installer
- acht direkt ziehbare Griffe am Crop-Rahmen

### Changed

- Crop, Resize, Convert und Optimize zu einem verständlichen Arbeitsbereich
  mit den Gruppen Größe/Zuschnitt, Format/Kompression und Speichern zusammengeführt
- Originalpixel und Original-Seitenverhältnis werden immer sichtbar angezeigt
- doppelte Modi „Nur Dateiformat ändern“ und „Originalgröße behalten“ vereinheitlicht
- WebP-Encoder auf die gründlichere Methode 6 und bildgerechte Filterwerte umgestellt
- Dateigrößen-Schätzung basiert nun auf einer echten Testkodierung der Vorschau

### Fixed

- Fokus für WASD, Pfeiltasten und R im Crop-Editor repariert
- Verschieben und Vergrößern/Verkleinern des Crop-Rahmens mit der Maus repariert

## [1.0.0] - 2026-08-13

### Added

- WinUI-3-Desktopanwendung für Windows 11 x64
- Crop-, Resize-, Fit-, Convert-, Optimize- und Rename-Workflows
- HEIC-/HEIF-Decodierung und Export nach WebP, JPEG, PNG und AVIF
- Portfolio-Card-Preset mit 600 × 1050 Pixeln
- Batch-Export, individuelle Crop-Zustände und sichere Dateinamenskollisionen
- Light-, Dark- und System-Theme
- lokale Diagnose-, Logging- und Settings-Dienste
- automatisierte Tests, CI, Release-Pipeline und Inno-Setup-Installer
