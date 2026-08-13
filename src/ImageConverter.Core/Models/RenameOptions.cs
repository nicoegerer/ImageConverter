namespace ImageConverter.Core.Models;

public sealed record RenameOptions(string Prefix, int StartNumber, int NumberLength, RenameSortMode SortMode);
