using System;

namespace ImageConverter.Core.Models;

public sealed record UpdateRelease(
    Version Version,
    string TagName,
    string HtmlUrl,
    string InstallerUrl,
    string ChecksumsUrl);
