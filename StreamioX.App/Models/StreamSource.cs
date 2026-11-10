using System.Collections.Generic;
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

        [JsonPropertyName("sources")]
        public List<string>? Sources { get; set; }

        [JsonPropertyName("behaviorHints")]
        public StreamBehaviorHints? BehaviorHints { get; set; }

        [JsonIgnore]
        public string SourceKind =>
            !string.IsNullOrWhiteSpace(Url) && !Url.StartsWith("magnet:", System.StringComparison.OrdinalIgnoreCase)
                ? "Direct stream"
                : !string.IsNullOrWhiteSpace(InfoHash) || (!string.IsNullOrWhiteSpace(Url) && Url.StartsWith("magnet:", System.StringComparison.OrdinalIgnoreCase))
                    ? "Torrent stream"
                    : "Unsupported source";
    }

    public sealed class StreamBehaviorHints
    {
        [JsonPropertyName("proxyHeaders")]
        public StreamProxyHeaders? ProxyHeaders { get; set; }
    }

    public sealed class StreamProxyHeaders
    {
        [JsonPropertyName("request")]
        public Dictionary<string, string>? Request { get; set; }
    }

    public class StreamResponse
    {
        [JsonPropertyName("streams")]
        public StreamSource[]? Streams { get; set; }
    }
}
