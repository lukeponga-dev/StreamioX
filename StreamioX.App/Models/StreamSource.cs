using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    /// <summary>
    /// Represents a stream result returned by a Stremio stream add-on[cite: 1, 2].
    /// </summary>
    public class StreamSource
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("infoHash")]
        public string InfoHash { get; set; }

        [JsonPropertyName("fileIdx")]
        public int? FileIdx { get; set; }
    }

    public class StreamResponse
    {
        [JsonPropertyName("streams")]
        public StreamSource[] Streams { get; set; }
    }
}