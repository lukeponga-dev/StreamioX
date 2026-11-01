using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    public sealed class AddonManifest
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }

        [JsonPropertyName("resources")]
        public List<JsonElement>? Resources { get; set; }

        [JsonPropertyName("catalogs")]
        public List<CatalogDescriptor>? Catalogs { get; set; }

        [JsonPropertyName("idPrefixes")]
        public List<string>? IdPrefixes { get; set; }

        public IReadOnlyList<string> GetResourceNames()
        {
            if (Resources == null || Resources.Count == 0)
                return Array.Empty<string>();

            var names = new List<string>();
            foreach (var r in Resources)
            {
                if (r.ValueKind == JsonValueKind.String)
                {
                    var s = r.GetString();
                    if (!string.IsNullOrEmpty(s)) names.Add(s);
                }
                else if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("name", out var nameProp))
                {
                    var s = nameProp.GetString();
                    if (!string.IsNullOrEmpty(s)) names.Add(s);
                }
            }
            return names;
        }

        public bool SupportsResource(string resourceName) =>
            GetResourceNames().Any(n => string.Equals(n, resourceName, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class CatalogDescriptor
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
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
        public bool IsOfficial { get; set; }
    }
}
