// Purpose: Defines AddonManifest data exchanged between app features and external Stremio-compatible services.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public sealed class AddonManifest
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("logo")] public string? Logo { get; set; }
        [JsonPropertyName("types")] public List<string>? Types { get; set; }
        [JsonPropertyName("resources")] public List<JsonElement>? Resources { get; set; }
        [JsonPropertyName("catalogs")] public List<CatalogDescriptor>? Catalogs { get; set; }
        [JsonPropertyName("idPrefixes")] public List<string>? IdPrefixes { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

        public IReadOnlyList<string> GetResourceNames()
        {
            if (Resources == null || Resources.Count == 0) return Array.Empty<string>();
            var names = new List<string>();
            foreach (var resource in Resources)
            {
                if (resource.ValueKind == JsonValueKind.String)
                {
                    var value = resource.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) names.Add(value);
                }
                else if (resource.ValueKind == JsonValueKind.Object &&
                         resource.TryGetProperty("name", out var nameProperty) &&
                         nameProperty.ValueKind == JsonValueKind.String)
                {
                    var value = nameProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) names.Add(value);
                }
            }
            return names;
        }

        public bool SupportsResource(string resourceName) =>
            GetResourceNames().Any(name => string.Equals(name, resourceName, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class CatalogDescriptor
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("extra")] public List<CatalogExtra>? Extra { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    }

    public sealed class CatalogExtra
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("isRequired")] public bool IsRequired { get; set; }
        [JsonPropertyName("options")] public List<string>? Options { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    }

    public sealed class InstalledAddon
    {
        public string ManifestUrl { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Logo { get; set; }
        public List<string> Types { get; set; } = new();
        public List<string> Resources { get; set; } = new();
        public List<CatalogDescriptor> Catalogs { get; set; } = new();
        public List<string> IdPrefixes { get; set; } = new();
        public List<JsonElement> ManifestResources { get; set; } = new();
        public Dictionary<string, JsonElement> ManifestAdditionalProperties { get; set; } = new();
        public bool IsOfficial { get; set; }
        public string SyncState { get; set; } = "Pending";
        public string? LastSyncError { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        [JsonIgnore] public string LastSyncedDisplay => LastSyncedAt?.ToLocalTime().ToString("g") ?? "Not synced yet";
        [JsonIgnore] public string ResourcesDisplay => string.Join(" · ", Resources);
    }
}
