using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public class Movie
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Title { get; set; }

        [JsonPropertyName("poster")]
        public string? PosterUrl { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("releaseInfo")]
        public string? ReleaseInfo { get; set; }
    }

    public class CatalogResponse
    {
        [JsonPropertyName("metas")]
        public Movie[]? Metas { get; set; }
    }
}
