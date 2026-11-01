using System.Collections.Generic;
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
        [JsonPropertyName("background")] public string? BackgroundUrl { get; set; }
        [JsonPropertyName("logo")] public string? LogoUrl { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
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
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
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
