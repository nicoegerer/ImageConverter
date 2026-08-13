using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public sealed class GitHubUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/nicoegerer/ImageConverter/releases/latest";
    private const string InstallerAssetName = "ImageConverter-Setup.exe";
    private const string ChecksumsAssetName = "SHA256SUMS.txt";

    private static readonly HttpClient Client = CreateClient();

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

    public async Task<UpdateRelease?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.GetAsync(LatestReleaseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubRelease release = await JsonSerializer.DeserializeAsync<GitHubRelease>(content, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an empty release response.");
        if (release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out Version? latest)
            || latest <= Normalize(currentVersion))
        {
            return null;
        }

        GitHubAsset? installer = release.Assets.FirstOrDefault(asset => asset.Name.Equals(InstallerAssetName, StringComparison.OrdinalIgnoreCase));
        GitHubAsset? checksums = release.Assets.FirstOrDefault(asset => asset.Name.Equals(ChecksumsAssetName, StringComparison.OrdinalIgnoreCase));
        if (installer is null || checksums is null)
        {
            throw new InvalidDataException("The latest release does not contain the installer and checksum assets.");
        }

        EnsureAllowedDownloadUri(installer.DownloadUrl);
        EnsureAllowedDownloadUri(checksums.DownloadUrl);
        return new UpdateRelease(latest!, release.TagName, release.HtmlUrl, installer.DownloadUrl, checksums.DownloadUrl);
    }

    public async Task<string> DownloadInstallerAsync(
        UpdateRelease release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string directory = Path.Combine(Path.GetTempPath(), "NicoImageConverter", "Updates", release.TagName);
        Directory.CreateDirectory(directory);
        string installerPath = Path.Combine(directory, InstallerAssetName);
        string temporaryPath = installerPath + ".download";

        string checksums = await Client.GetStringAsync(release.ChecksumsUrl, cancellationToken);
        string expectedHash = ParseExpectedHash(checksums, InstallerAssetName);
        using HttpResponseMessage response = await Client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (FileStream destination = File.Create(temporaryPath))
        {
            byte[] buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                if (totalBytes > 0)
                {
                    progress?.Report((double)received / totalBytes.Value);
                }
            }
        }

        await using (FileStream stream = File.OpenRead(temporaryPath))
        {
            string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temporaryPath);
                throw new InvalidDataException("The downloaded installer did not match the published SHA-256 checksum.");
            }
        }

        File.Move(temporaryPath, installerPath, overwrite: true);
        progress?.Report(1.0);
        return installerPath;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NicoImageConverter", "1.1.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static bool TryParseVersion(string tagName, out Version? version)
    {
        string value = tagName.Trim().TrimStart('v', 'V').Split('-', 2)[0];
        return Version.TryParse(value, out version);
    }

    private static Version Normalize(Version version)
    {
        return new Version(Math.Max(0, version.Major), Math.Max(0, version.Minor), Math.Max(0, version.Build));
    }

    private static string ParseExpectedHash(string checksums, string assetName)
    {
        foreach (string line in checksums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1].TrimStart('*').Equals(assetName, StringComparison.OrdinalIgnoreCase)
                && parts[0].Length == 64)
            {
                return parts[0];
            }
        }
        throw new InvalidDataException($"No SHA-256 checksum was published for {assetName}.");
    }

    private static void EnsureAllowedDownloadUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("GitHub returned an unexpected download address.");
        }
    }
}
