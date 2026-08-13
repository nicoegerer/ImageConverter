# Beitragen

Beiträge, Fehlerberichte und nachvollziehbare Verbesserungsvorschläge sind
willkommen.

## Entwicklungsablauf

1. Einen Branch von `main` erstellen.
2. Änderungen klein und thematisch zusammenhängend halten.
3. Neue Logik möglichst durch Tests abdecken.
4. Vor dem Commit Restore, Build und Tests ausführen.
5. Einen Pull Request mit Problem, Lösung und Testnachweis eröffnen.

```powershell
dotnet restore ImageConverter.sln -p:Platform=x64
dotnet build ImageConverter.sln -c Release -p:Platform=x64 --no-restore
dotnet test tests/ImageConverter.Core.Tests/ImageConverter.Core.Tests.csproj `
  -c Release -p:Platform=x64 --no-build --no-restore
```

## Code-Richtlinien

- Nullable Reference Types bleiben aktiviert.
- UI, ViewModels und Bildverarbeitung bleiben voneinander getrennt.
- Dateizugriffe und Bildverarbeitung blockieren nicht den UI-Thread.
- Originaldateien werden standardmäßig nicht überschrieben.
- neue Dateiformate benötigen mindestens einen Decode-/Encode-Test.
- sichtbare Texte müssen in Light und Dark Mode lesbar bleiben.
- keine privaten Bilder, persönlichen Pfade, Zugangsdaten oder Build-Artefakte
  committen.

## Fehlerberichte

Bitte das Bug-Template verwenden und, wenn möglich, die anonymisierten Angaben
aus `%LOCALAPPDATA%\Image Converter\Diagnostics` beifügen. Private Bilder und
personenbezogene Pfade sollten vor dem Hochladen entfernt werden.
