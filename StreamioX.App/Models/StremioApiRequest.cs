// Purpose: Defines StremioApiRequest data exchanged between app features and external Stremio-compatible services.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    // Stremio puts the API method in the URL; the body contains only its parameters.
    public sealed class StremioApiRequest
    {
        [JsonPropertyName("email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        [JsonPropertyName("password"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Password { get; set; }

        [JsonPropertyName("authKey"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthKey { get; set; }

        [JsonPropertyName("update"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Update { get; set; }

        [JsonPropertyName("addFromURL"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? AddFromURL { get; set; }

        [JsonPropertyName("addons"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<StremioAddonDescriptor>? Addons { get; set; }
    }
}
