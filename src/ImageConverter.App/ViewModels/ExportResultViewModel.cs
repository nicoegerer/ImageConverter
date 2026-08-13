using System.IO;
using ImageConverter.Core.Models;

namespace ImageConverter.App.ViewModels;

public sealed record ExportResultViewModel(string Source, string Destination, string Status, string Details)
{
    public static ExportResultViewModel From(ExportResult result)
    {
        return new ExportResultViewModel(result.Job.Source.File.FileName, Path.GetFileName(result.Job.DestinationPath), result.Success ? "Erfolgreich" : "Fehler", result.Success ? $"{result.OutputWidth} × {result.OutputHeight} · {ImageAssetViewModel.FormatBytes(result.OutputBytes)}" : (result.ErrorMessage ?? "Unbekannter Fehler"));
    }
}
