// Models/Movie.cs
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    /// <summary>
    /// Represents a media item (Movie or Series) returned by a Stremio add-on catalog.
    /// </summary>
    public class Movie
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("name")]
        public string Title { get; set; }

        [JsonPropertyName("poster")]
        public string PosterUrl { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("releaseInfo")]
        public string ReleaseInfo { get; set; }
    }

    /// <summary>
    /// Wrapper container for Stremio catalog API responses.
    /// </summary>
    public class CatalogResponse
    {
        [JsonPropertyName("metas")]
        public Movie[] Metas { get; set; }
    }
}