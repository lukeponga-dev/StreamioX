// Purpose: Defines AuthModels data exchanged between app features and external Stremio-compatible services.

using System.Collections.Generic;
using System.Text.Json;
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
        [JsonPropertyName("_id")] public string? Id { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    }

    public sealed class StremioApiEnvelope<T>
    {
        [JsonPropertyName("result")] public T? Result { get; set; }
        [JsonPropertyName("error")] public StremioApiError? Error { get; set; }
    }

    public sealed class StremioApiError
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    public sealed class DeviceLinkCreateResult
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("qrcode")] public string? Qrcode { get; set; }
    }

    public sealed class LoginResult
    {
        [JsonPropertyName("authKey")] public string? AuthKey { get; set; }
        [JsonPropertyName("user")] public StremioUser? User { get; set; }
    }

    // Stremio's portable add-on collection descriptor format.
    public sealed class StremioAddonDescriptor
    {
        [JsonPropertyName("manifest")] public AddonManifest? Manifest { get; set; }
        [JsonPropertyName("transportUrl")] public string? TransportUrl { get; set; }
        [JsonPropertyName("flags"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Flags { get; set; }
    }

    public sealed class StremioAddonCollectionResult
    {
        [JsonPropertyName("addons")] public List<StremioAddonDescriptor>? Addons { get; set; }
        [JsonPropertyName("lastModified")] public JsonElement LastModified { get; set; }
    }

    public sealed class AddonAccountSyncResult
    {
        public int Pulled { get; set; }
        public int Pushed { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
