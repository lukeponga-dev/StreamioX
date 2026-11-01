using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public sealed class DeviceLinkSession
    {
        public string Code { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
    }

    public sealed class StremioUser
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }

    public sealed class StremioApiEnvelope<T>
    {
        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("error")]
        public StremioApiError? Error { get; set; }
    }

    public sealed class StremioApiError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public sealed class DeviceLinkCreateResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("qrcode")]
        public string? Qrcode { get; set; }
    }

    public sealed class DeviceLinkReadResult
    {
        [JsonPropertyName("authKey")]
        public string? AuthKey { get; set; }

        [JsonPropertyName("user")]
        public StremioUser? User { get; set; }

        // Some responses nest under result.result
        [JsonPropertyName("result")]
        public DeviceLinkReadResult? Nested { get; set; }
    }

    public sealed class LoginResult
    {
        [JsonPropertyName("authKey")]
        public string? AuthKey { get; set; }

        [JsonPropertyName("user")]
        public StremioUser? User { get; set; }
    }
}
