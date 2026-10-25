using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Deltha.Services;

public sealed class LyricsService
{
    private readonly HttpClient _httpClient;

    public LyricsService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetLyricsAsync(
        string trackName,
        string artistName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName) || string.IsNullOrWhiteSpace(artistName))
        {
            return "Lirik tidak tersedia untuk media saat ini.";
        }

        var url = $"https://lrclib.net/api/get?track_name={WebUtility.UrlEncode(trackName.Trim())}" +
            $"&artist_name={WebUtility.UrlEncode(artistName.Trim())}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Deltha", "1.0"));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return "Lirik tidak ditemukan untuk lagu saat ini.";
            }

            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                contentStream,
                cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && TryReadLyrics(document.RootElement, out var lyrics))
            {
                return lyrics;
            }

            return "Lirik tidak tersedia untuk lagu saat ini.";
        }
        catch (HttpRequestException)
        {
            return "Koneksi lirik gagal. Coba lagi nanti.";
        }
        catch (JsonException)
        {
            return "Format lirik tidak valid.";
        }
    }

    private static bool TryReadLyrics(JsonElement element, out string lyrics)
    {
        if (element.TryGetProperty("plainLyrics", out var plainLyrics)
            && plainLyrics.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(plainLyrics.GetString()))
        {
            lyrics = plainLyrics.GetString()!;
            return true;
        }

        if (element.TryGetProperty("syncedLyrics", out var syncedLyrics)
            && syncedLyrics.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(syncedLyrics.GetString()))
        {
            lyrics = syncedLyrics.GetString()!;
            return true;
        }

        lyrics = string.Empty;
        return false;
    }
}
