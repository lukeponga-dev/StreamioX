using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public class StreamSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("infoHash")]
        public string? InfoHash { get; set; }

        [JsonPropertyName("fileIdx")]
        public int? FileIdx { get; set; }
    }

    public class StreamResponse
    {
        [JsonPropertyName("streams")]
        public StreamSource[]? Streams { get; set; }
    }
}
