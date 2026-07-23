using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpotifyTaskbarWidget;

public record LyricLine(TimeSpan Time, string Text);

public class LrcLibResponse
{
    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }
}

public class LyricsService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    
    private string _lastTitle = "";
    private string _lastArtist = "";
    private List<LyricLine>? _lastLyrics;
    
    static LyricsService()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string vStr = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.3.2";
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"SpotifyTaskbarWidget/{vStr} (https://github.com/maxlmuller/spotify-taskbar-widget)");
    }

    public async Task<List<LyricLine>?> GetLyricsAsync(string title, string artist)
    {
        if (title == _lastTitle && artist == _lastArtist)
            return _lastLyrics;
            
        _lastTitle = title;
        _lastArtist = artist;
        _lastLyrics = null;
        
        try
        {
            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            var response = await Http.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsNotFound) };
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsTooManyRequests) };
            }

            if (!response.IsSuccessStatusCode) 
            {
                return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsServerError((int)response.StatusCode)) };
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var lrc = JsonSerializer.Deserialize<LrcLibResponse>(json);
            if (string.IsNullOrWhiteSpace(lrc?.SyncedLyrics)) 
            {
                return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsNoSync) };
            }
            
            _lastLyrics = ParseLrc(lrc.SyncedLyrics);
            if (_lastLyrics.Count == 0)
            {
                return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsParseError) };
            }

            return _lastLyrics;
        }
        catch (Exception ex)
        {
            Diag.Once("lyrics-fetch", $"Error fetching lyrics: {ex.Message}");
            return new List<LyricLine> { new LyricLine(TimeSpan.Zero, L.LyricsNetworkError) };
        }
    }
    
    private static List<LyricLine> ParseLrc(string lrcText)
    {
        var lines = new List<LyricLine>();
        var regex = new Regex(@"\[(\d{1,3}):(\d{1,2}(?:\.\d+)?)\](.*)");
        
        foreach (var line in lrcText.Split('\n'))
        {
            var match = regex.Match(line.Trim());
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int min) && 
                    double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec))
                {
                    lines.Add(new LyricLine(TimeSpan.FromSeconds(min * 60 + sec), match.Groups[3].Value.Trim()));
                }
            }
        }
        return lines;
    }
}
