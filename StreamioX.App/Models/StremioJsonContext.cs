// Purpose: Defines StremioJsonContext data exchanged between app features and external Stremio-compatible services.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    // Explicit source-generated metadata keeps the app compatible with trimming/AOT.
    [JsonSourceGenerationOptions(
        PropertyNameCaseInsensitive = true,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(CatalogResponse))]
    [JsonSerializable(typeof(MetaResponse))]
    [JsonSerializable(typeof(Movie))]
    [JsonSerializable(typeof(Movie[]))]
    [JsonSerializable(typeof(LibraryEntry))]
    [JsonSerializable(typeof(List<LibraryEntry>))]
    [JsonSerializable(typeof(Video))]
    [JsonSerializable(typeof(List<Video>))]
    [JsonSerializable(typeof(StreamResponse))]
    [JsonSerializable(typeof(StreamSource[]))]
    [JsonSerializable(typeof(AddonManifest))]
    [JsonSerializable(typeof(InstalledAddon))]
    [JsonSerializable(typeof(List<InstalledAddon>))]
    [JsonSerializable(typeof(CatalogDescriptor))]
    [JsonSerializable(typeof(List<CatalogDescriptor>))]
    [JsonSerializable(typeof(CatalogExtra))]
    [JsonSerializable(typeof(List<CatalogExtra>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(StremioUser))]
    [JsonSerializable(typeof(DeviceLinkSession))]
    [JsonSerializable(typeof(DeviceLinkCreateResult))]
    [JsonSerializable(typeof(LoginResult))]
    [JsonSerializable(typeof(StremioApiError))]
    [JsonSerializable(typeof(StremioApiEnvelope<DeviceLinkCreateResult>))]
    [JsonSerializable(typeof(StremioApiEnvelope<LoginResult>))]
    [JsonSerializable(typeof(StremioApiEnvelope<StremioUser>))]
    [JsonSerializable(typeof(StremioApiEnvelope<object>))]
    [JsonSerializable(typeof(StremioApiEnvelope<StremioAddonCollectionResult>))]
    [JsonSerializable(typeof(StremioAddonCollectionResult))]
    [JsonSerializable(typeof(StremioAddonDescriptor))]
    [JsonSerializable(typeof(List<StremioAddonDescriptor>))]
    [JsonSerializable(typeof(System.Text.Json.JsonElement))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(Dictionary<string, System.Text.Json.JsonElement>))]
    [JsonSerializable(typeof(StremioApiRequest))]
    public partial class StremioJsonContext : JsonSerializerContext
    {
    }
}
