using System.Text.Json.Serialization;

namespace StreamioX.App.Models
{
    [JsonSerializable(typeof(CatalogResponse))]
    [JsonSerializable(typeof(Movie))]
    [JsonSerializable(typeof(Movie[]))]
    public partial class StremioJsonContext : JsonSerializerContext
    {
    }
}