using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public sealed class ExportPlanService
{
    public IReadOnlyList<ExportJob> CreatePlan(IReadOnlyList<ExportSource> sources, ExportProfile profile, string destinationFolder, NamingOptions naming)
    {
        profile.Validate();
        Directory.CreateDirectory(destinationFolder);
        HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ExportJob> list = new List<ExportJob>(sources.Count);
        string extension = GetExtension(profile.Format);
        foreach (ExportSource item in sources.OrderBy((ExportSource x) => x.Order))
        {
            string text = ((naming.Mode != NamingMode.PrefixSequence) ? Path.GetFileNameWithoutExtension(item.File.FileName) : (naming.Prefix + (naming.StartNumber + item.Order).ToString().PadLeft(Math.Max(1, naming.NumberLength), '0')));
            string name = text;
            name = SanitizeFileName(name);
            string text2 = ResolveCollision(Path.Combine(destinationFolder, name + extension), hashSet);
            hashSet.Add(text2);
            string temporaryPath = Path.Combine(destinationFolder, ".image-converter-export-" + Guid.NewGuid().ToString("N") + extension);
            list.Add(new ExportJob(item, profile, text2, temporaryPath));
        }
        return list;
    }

    public static string GetExtension(OutputImageFormat format)
    {
        return format switch
        {
            OutputImageFormat.WebP => ".webp",
            OutputImageFormat.Jpeg => ".jpg",
            OutputImageFormat.Png => ".png",
            OutputImageFormat.Avif => ".avif",
            _ => throw new ArgumentOutOfRangeException("format"),
        };
    }

    private static string ResolveCollision(string desired, HashSet<string> reserved)
    {
        if (!File.Exists(desired) && !reserved.Contains(desired))
        {
            return desired;
        }
        string directoryName = Path.GetDirectoryName(desired) ?? throw new InvalidOperationException("The export destination has no parent directory.");
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(desired);
        string extension = Path.GetExtension(desired);
        for (int i = 2; i < int.MaxValue; i++)
        {
            string text = Path.Combine(directoryName, $"{fileNameWithoutExtension}_{i}{extension}");
            if (!File.Exists(text) && !reserved.Contains(text))
            {
                return text;
            }
        }
        throw new IOException("Could not create a collision-free name for '" + desired + "'.");
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (char oldChar in invalidFileNameChars)
        {
            name = name.Replace(oldChar, '_');
        }
        name = name.Trim().TrimEnd('.');
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return "Bild";
    }
}
