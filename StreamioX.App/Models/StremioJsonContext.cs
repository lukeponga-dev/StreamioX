using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    // All JSON contracts used by the app are registered here so NativeAOT never
    // needs reflection-based serialization metadata.
    [JsonSourceGenerationOptions(
        PropertyNameCaseInsensitive = true,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(CatalogResponse))]
    [JsonSerializable(typeof(Movie))]
    [JsonSerializable(typeof(Movie[]))]
    [JsonSerializable(typeof(StreamResponse))]
    [JsonSerializable(typeof(StreamSource[]))]
    [JsonSerializable(typeof(AddonManifest))]
    [JsonSerializable(typeof(InstalledAddon))]
    [JsonSerializable(typeof(List<InstalledAddon>))]
    [JsonSerializable(typeof(CatalogDescriptor))]
    [JsonSerializable(typeof(List<CatalogDescriptor>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(StremioUser))]
    [JsonSerializable(typeof(DeviceLinkSession))]
    [JsonSerializable(typeof(DeviceLinkCreateResult))]
    [JsonSerializable(typeof(DeviceLinkReadResult))]
    [JsonSerializable(typeof(LoginResult))]
    [JsonSerializable(typeof(StremioApiError))]
    [JsonSerializable(typeof(StremioApiEnvelope<DeviceLinkCreateResult>))]
    [JsonSerializable(typeof(StremioApiEnvelope<LoginResult>))]
    [JsonSerializable(typeof(StremioApiEnvelope<StremioUser>))]
    [JsonSerializable(typeof(StremioApiEnvelope<object>))]
    [JsonSerializable(typeof(StremioApiRequest))]
    public partial class StremioJsonContext : JsonSerializerContext
    {
    }
}