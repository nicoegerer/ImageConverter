namespace ImageConverter.Core.Models;

public sealed record NamingOptions(NamingMode Mode, string Prefix, int StartNumber, int NumberLength);
