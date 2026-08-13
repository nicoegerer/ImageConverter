using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public sealed class RenameService
{
    public IReadOnlyList<RenamePlanItem> CreateSinglePlan(ImageFileInfo file, string newFileName)
    {
        string directory = Path.GetDirectoryName(file.Path) ?? throw new InvalidOperationException("The source file has no parent directory.");
        newFileName = newFileName.Trim();
        if (string.IsNullOrWhiteSpace(newFileName) || !string.Equals(Path.GetFileName(newFileName), newFileName, StringComparison.Ordinal) || newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Enter a valid file name without a folder path.", "newFileName");
        }
        string extension = Path.GetExtension(file.Path);
        string text = Path.GetExtension(newFileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            newFileName += extension.ToLowerInvariant();
            text = extension;
        }
        if (!string.Equals(extension, text, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Renaming cannot change the image format. Keep the existing extension or use Export to convert it.", "newFileName");
        }
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(newFileName)))
        {
            throw new ArgumentException("The file name must contain a name before the extension.", "newFileName");
        }
        string text2 = Path.Combine(directory, newFileName);
        if (string.Equals(file.Path, text2, StringComparison.Ordinal))
        {
            throw new IOException("The new file name is identical to the current file name.");
        }
        if (File.Exists(text2) && !string.Equals(file.Path, text2, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The target file already exists: " + text2);
        }
        string temporaryPath = Path.Combine(directory, ".image-converter-rename-" + Guid.NewGuid().ToString("N") + extension.ToLowerInvariant());
        return new[] { new RenamePlanItem(file.Path, temporaryPath, text2, 0) };
    }

    public IReadOnlyList<RenamePlanItem> CreatePlan(IReadOnlyList<ImageFileInfo> files, RenameOptions options)
    {
        ImageFileInfo[] array = Sort(files, options.SortMode).ToArray();
        HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<RenamePlanItem> list = new List<RenamePlanItem>(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            ImageFileInfo imageFileInfo = array[i];
            string directory = Path.GetDirectoryName(imageFileInfo.Path) ?? throw new InvalidOperationException("The source file has no parent directory.");
            string text = (options.StartNumber + i).ToString().PadLeft(Math.Max(1, options.NumberLength), '0');
            string text2 = Path.GetExtension(imageFileInfo.Path).ToLowerInvariant();
            string destination = Path.Combine(directory, options.Prefix + text + text2);
            if (!hashSet.Add(destination))
            {
                throw new IOException("The rename plan contains a duplicate target: " + destination);
            }
            if (File.Exists(destination) && !files.Any((ImageFileInfo x) => string.Equals(x.Path, destination, StringComparison.OrdinalIgnoreCase)))
            {
                throw new IOException("The target already exists and is not part of this rename operation: " + destination);
            }
            string temporaryPath = Path.Combine(directory, ".image-converter-rename-" + Guid.NewGuid().ToString("N") + text2);
            list.Add(new RenamePlanItem(imageFileInfo.Path, temporaryPath, destination, i));
        }
        return list;
    }

    public void Execute(IReadOnlyList<RenamePlanItem> plan)
    {
        List<RenamePlanItem> list = new List<RenamePlanItem>();
        List<RenamePlanItem> list2 = new List<RenamePlanItem>();
        try
        {
            foreach (RenamePlanItem item in plan)
            {
                File.Move(item.SourcePath, item.TemporaryPath);
                list.Add(item);
            }
            foreach (RenamePlanItem item2 in plan)
            {
                File.Move(item2.TemporaryPath, item2.DestinationPath);
                list2.Add(item2);
            }
        }
        catch
        {
            foreach (RenamePlanItem item3 in from x in list2.AsEnumerable().Reverse()
                                             where File.Exists(x.DestinationPath)
                                             select x)
            {
                try
                {
                    if (!File.Exists(item3.SourcePath))
                    {
                        File.Move(item3.DestinationPath, item3.SourcePath);
                    }
                }
                catch
                {
                }
            }
            foreach (RenamePlanItem item4 in list.Where((RenamePlanItem x) => File.Exists(x.TemporaryPath)))
            {
                try
                {
                    File.Move(item4.TemporaryPath, item4.SourcePath);
                }
                catch
                {
                }
            }
            throw;
        }
    }

    private static IEnumerable<ImageFileInfo> Sort(IEnumerable<ImageFileInfo> files, RenameSortMode sortMode)
    {
        return sortMode switch
        {
            RenameSortMode.CurrentFileName => files.OrderBy<ImageFileInfo, string>((ImageFileInfo x) => x.FileName, StringComparer.CurrentCultureIgnoreCase),
            RenameSortMode.CaptureTime => from x in files
                                          orderby x.CaptureTime ?? x.ModifiedTime, x.FileName
                                          select x,
            RenameSortMode.ModifiedTime => from x in files
                                           orderby x.ModifiedTime, x.FileName
                                           select x,
            RenameSortMode.CreatedTime => from x in files
                                          orderby x.CreatedTime, x.FileName
                                          select x,
            RenameSortMode.ManualOrder => files,
            _ => files,
        };
    }
}
