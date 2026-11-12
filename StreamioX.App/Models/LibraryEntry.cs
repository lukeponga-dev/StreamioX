// Purpose: Defines LibraryEntry data exchanged between app features and external Stremio-compatible services.

using System;
using System.Text.Json.Serialization;
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
        [JsonIgnore] public string DisplayTitle => Movie?.Title ?? "Unknown title";
        [JsonIgnore] public string? PosterUrl => Movie?.PosterUrl;
        [JsonIgnore] public double ProgressPercent => DurationSeconds > 0
            ? Math.Clamp(PositionSeconds / DurationSeconds * 100, 0, 100) : 0;
        [JsonIgnore] public string ProgressText => DurationSeconds > 0 && PositionSeconds > 0
            ? $"Continue · {ProgressPercent:0}% watched"
            : "Saved to your library";
    }
}
