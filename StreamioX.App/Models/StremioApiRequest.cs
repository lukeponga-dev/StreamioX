using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public sealed class StremioApiRequest
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("facebook")]
        public bool Facebook { get; set; }

        [JsonPropertyName("authKey")]
        public string? AuthKey { get; set; }
    }
}