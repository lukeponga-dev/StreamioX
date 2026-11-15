// Purpose: Defines LibraryEntry data exchanged between app features and external Stremio-compatible services.

using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using WinRT;

namespace StreamioX.App.Models
{
    [GeneratedBindableCustomProperty]
    public sealed partial class LibraryEntry
    {
        [JsonPropertyName("movie")] public Movie? Movie { get; set; }
        // Existing library files omit this field; default true preserves their saved state.
        [JsonPropertyName("isSaved")] public bool IsSaved { get; set; } = true;
        [JsonPropertyName("addedUtc")] public DateTimeOffset AddedUtc { get; set; }
        [JsonPropertyName("lastPlayedUtc")] public DateTimeOffset? LastPlayedUtc { get; set; }
        [JsonPropertyName("positionSeconds")] public double PositionSeconds { get; set; }
        [JsonPropertyName("durationSeconds")] public double DurationSeconds { get; set; }
        [JsonPropertyName("lastEpisodeId")] public string? LastEpisodeId { get; set; }
        [JsonPropertyName("episodeProgress")] public Dictionary<string, EpisodeWatchProgress> EpisodeProgress { get; set; } = new();
        [JsonIgnore] public string DisplayTitle => Movie?.Title ?? "Unknown title";
        [JsonIgnore] public string? PosterUrl => Movie?.PosterUrl;
        [JsonIgnore] public double ProgressPercent => DurationSeconds > 0
            ? Math.Clamp(PositionSeconds / DurationSeconds * 100, 0, 100) : 0;
        [JsonIgnore] public string ProgressText
        {
            get
            {
                if (DurationSeconds <= 0 || PositionSeconds <= 0)
                    return "Saved to your library";
                var episode = Movie?.Videos?.FirstOrDefault(item =>
                    string.Equals(item.Id, LastEpisodeId, StringComparison.OrdinalIgnoreCase));
                if (string.Equals(Movie?.Type, "series", StringComparison.OrdinalIgnoreCase) && episode != null)
                    return $"{episode.EpisodeCode} · {episode.Title} · {ProgressPercent:0}% watched";
                return $"Continue · {ProgressPercent:0}% watched";
            }
        }
        [JsonIgnore] public string ElapsedRuntimeText => DurationSeconds > 0
            ? $"{FormatTime(PositionSeconds)} / {FormatTime(DurationSeconds)}"
            : string.Empty;
        private static string FormatTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
        }
        [JsonIgnore] public string LastWatchedText => LastPlayedUtc.HasValue
            ? $"Last watched {LastPlayedUtc.Value.ToLocalTime():g}"
            : "Not watched yet";
    }
}
