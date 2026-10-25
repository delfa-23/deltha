using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Deltha.Services;

public sealed record UpdateInfo(Version Version, Uri DownloadUri);

public sealed class UpdateChecker
{
    public static readonly Uri DefaultVersionJsonUri = new(
        "https://github.com/delfa-23/deltha/releases/latest/download/version.json");

    private readonly HttpClient _httpClient;
    private readonly Uri _versionJsonUri;
    private readonly Version _currentVersion;

    public UpdateChecker(
        Uri versionJsonUri,
        Version currentVersion,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(versionJsonUri);
        ArgumentNullException.ThrowIfNull(currentVersion);
        _versionJsonUri = versionJsonUri;
        _currentVersion = currentVersion;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Deltha", currentVersion.ToString()));
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            _versionJsonUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<VersionManifest>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("File version.json tidak valid.");

        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            throw new InvalidDataException("Versi di version.json tidak valid.");
        }

        if (latestVersion <= _currentVersion)
        {
            return null;
        }

        var downloadUri = Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var explicitUri)
            ? explicitUri
            : new Uri("https://github.com/delfa-23/deltha/releases/latest");
        if (downloadUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidDataException("Link update harus menggunakan HTTP atau HTTPS.");
        }

        return new UpdateInfo(latestVersion, downloadUri);
    }

    private sealed class VersionManifest
    {
        [JsonPropertyName("version")]
        public string? Version { get; init; }

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; init; }
    }
}
