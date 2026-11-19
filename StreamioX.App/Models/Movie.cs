// Purpose: Defines Movie data exchanged between app features and external Stremio-compatible services.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinRT;

namespace StreamioX.App.Models
{
    /// <summary>Stremio metadata preview and full metadata object for movies and series.</summary>
    [GeneratedBindableCustomProperty]
    public partial class Movie
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("name")] public string? Title { get; set; }
        [JsonPropertyName("poster")] public string? PosterUrl { get; set; }
        [JsonIgnore] 
        public string SafePosterUrl => Uri.TryCreate(PosterUrl, UriKind.Absolute, out _) ? PosterUrl!
        : "ms-appx:///Assets/Images/poster-placeholder.png";
        [JsonPropertyName("background")] public string? BackgroundUrl { get; set; }
        [JsonPropertyName("logo")] public string? LogoUrl { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        // Alternate names used by TMDB-backed and non-Cinemeta metadata providers.
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("plot")] public string? Plot { get; set; }
        [JsonIgnore] public string Synopsis =>
            !string.IsNullOrWhiteSpace(Description) ? Description :
            !string.IsNullOrWhiteSpace(Overview) ? Overview :
            Plot ?? string.Empty;
        [JsonPropertyName("trailer")] public JsonElement? Trailer { get; set; }
        [JsonPropertyName("trailerUrl")] public string? TrailerUrl { get; set; }
        [JsonIgnore] public string? TrailerLink =>
            !string.IsNullOrWhiteSpace(TrailerUrl) ? TrailerUrl :
            Trailer.HasValue && Trailer.Value.ValueKind == JsonValueKind.String
                ? Trailer.Value.GetString()
                : Trailer.HasValue && Trailer.Value.ValueKind == JsonValueKind.Object &&
                  Trailer.Value.TryGetProperty("url", out var trailerUrl) &&
                  trailerUrl.ValueKind == JsonValueKind.String
                    ? trailerUrl.GetString()
                    : null;
        [JsonPropertyName("releaseInfo")] public string? ReleaseInfo { get; set; }
        [JsonPropertyName("imdbRating")] public string? ImdbRating { get; set; }
        [JsonPropertyName("runtime")] public string? Runtime { get; set; }
        [JsonPropertyName("genres")] public List<string>? Genres { get; set; }
        [JsonPropertyName("cast")] public List<string>? Cast { get; set; }
        [JsonPropertyName("director")] public List<string>? Director { get; set; }
        [JsonPropertyName("videos")] public List<Video>? Videos { get; set; }
    }

    [GeneratedBindableCustomProperty]
    public partial class Video
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("season")] public int? Season { get; set; }
        [JsonPropertyName("episode")] public int? Episode { get; set; }
        [JsonPropertyName("released")] public string? Released { get; set; }
        [JsonPropertyName("runtime")] public string? Runtime { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
        [JsonIgnore] public double ProgressPercent { get; set; }
        [JsonIgnore] public string ProgressText { get; set; } = "Not started";
        [JsonIgnore] public bool IsCompleted { get; set; }
        [JsonIgnore] public string EpisodeCode => Season.HasValue && Episode.HasValue ? $"S{Season:00}E{Episode:00}" : "Episode";
        [JsonIgnore] public string AirDateText => DateTimeOffset.TryParse(Released, out var date) ? date.ToLocalTime().ToString("d MMM yyyy") : string.Empty;
        [JsonIgnore] public string RuntimeText => string.IsNullOrWhiteSpace(Runtime) ? string.Empty : Runtime.Trim();
    }

    public sealed class EpisodeWatchProgress
    {
        [JsonPropertyName("positionSeconds")] public double PositionSeconds { get; set; }
        [JsonPropertyName("durationSeconds")] public double DurationSeconds { get; set; }
        [JsonPropertyName("lastPlayedUtc")] public DateTimeOffset? LastPlayedUtc { get; set; }
        [JsonIgnore] public double ProgressPercent => DurationSeconds > 0 ? Math.Clamp(PositionSeconds / DurationSeconds * 100, 0, 100) : 0;
    }

    public class CatalogResponse
    {
        [JsonPropertyName("metas")] public Movie[]? Metas { get; set; }
    }

    public class MetaResponse
    {
        [JsonPropertyName("meta")] public Movie? Meta { get; set; }
    }
}
