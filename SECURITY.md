# Sicherheitsrichtlinie

## Unterstützte Versionen

Sicherheitskorrekturen werden für die jeweils aktuelle veröffentlichte Version
bereitgestellt.

## Schwachstellen melden

Bitte keine ausnutzbaren Details in einem öffentlichen Issue veröffentlichen.
Bevorzugt wird GitHubs Funktion **Private vulnerability reporting** verwendet.
Falls diese im Repository noch nicht aktiviert ist, kann ein öffentliches Issue
ohne technische Details eröffnet werden, um einen privaten Kontaktweg anzufragen.

Eine gute Meldung enthält:

- betroffene Version und Windows-Build
- betroffene Datei oder Funktion
- reproduzierbare Schritte
- erwartetes und beobachtetes Verhalten
- mögliche Auswirkungen
- einen Diagnosebericht, sofern er keine vertraulichen Bild- oder Pfaddaten enthält

## Release-Sicherheit

- Release-Artefakte werden ausschließlich aus der versionierten Quelle durch die
  Windows-GitHub-Actions-Pipeline erzeugt.
- Der Installer enthält die vollständige self-contained Publish-Ausgabe.
- Build-Logs und temporäre Dateien sind kein Bestandteil des Releases.
- Prüfsummen werden gemeinsam mit jedem GitHub-Release veröffentlicht.
- Für eine öffentliche Distribution sollte die Pipeline zusätzlich mit einem
  vertrauenswürdigen Code-Signing-Zertifikat konfiguriert werden.

Warnungen von Windows, SmartScreen oder Endpoint-Schutzsystemen sollen nicht
umgangen werden. Ein auffälliges Artefakt sollte stattdessen anhand seiner
Prüfsumme und des zugehörigen reproduzierbaren Builds untersucht werden.
