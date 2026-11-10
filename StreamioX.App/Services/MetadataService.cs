using System;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public sealed class MetadataService : IMetadataService
    {
        private static readonly TimeSpan MetadataTtl = TimeSpan.FromMinutes(2);
        private readonly StremioClient _client;
        private readonly IAddonService _addons;

        public MetadataService(StremioClient client, IAddonService addons)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _addons = addons ?? throw new ArgumentNullException(nameof(addons));
        }

        public async Task<Movie?> GetMetaAsync(string type, string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
                return null;

            await _addons.InitializeAsync(cancellationToken).ConfigureAwait(false);
            foreach (var addon in _addons.GetMetaProviders(type, id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var url = $"{addon.BaseUrl.TrimEnd('/')}/meta/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(id)}.json";
                    var response = await MemoryTtlCache.GetOrCreateAsync(
                        "addon-meta:" + url,
                        MetadataTtl,
                        async () => await _client.GetFromJsonAsync<MetaResponse>(url, cancellationToken).ConfigureAwait(false))
                        .ConfigureAwait(false);

                    if (response?.Meta != null)
                        return response.Meta;
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    // A metadata provider can be offline or not know this ID. Try the next compatible provider.
                }
            }

            return null;
        }
    }
}