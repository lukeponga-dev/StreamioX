using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Security.Credentials;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed class AuthService : IAuthService
    {
        private const string AuthKeySetting = "StremioAuthKey";
        private const string UserSetting = "StremioUserJson";
        private const string VaultResource = "StreamioX.Stremio";
        private const string VaultUserName = "account-auth-key";
        private const string ApiBase = "https://api.strem.io/api/";
        private const string LinkCreateUrl = "https://link.stremio.com/api/v2/create?type=Create";

        private readonly HttpClient _http;
        private readonly IAddonService _addonService;
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly SemaphoreSlim _initializeLock = new(1, 1);
        private bool _initialized;

        public event EventHandler? SessionChanged;

        public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AuthKey);
        public string? AuthKey { get; private set; }
        public StremioUser? User { get; private set; }
        public string LastAddonSyncMessage { get; private set; } = "Add-on account sync has not run yet.";

        public AuthService(HttpClient httpClient, IAddonService addonService)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task InitializeAsync()
        {
            await _initializeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized) return;
                await LoadSavedSessionAsync().ConfigureAwait(false);

                if (IsLoggedIn)
                {
                    try
                    {
                        await RefreshUserAsync().ConfigureAwait(false);
                        await _addonService.InitializeAsync().ConfigureAwait(false);
                        await SyncAddonCollectionAsync().ConfigureAwait(false);
                    }
                    catch (HttpRequestException)
                    {
                        LastAddonSyncMessage = "Offline: saved Stremio account is available, but cloud sync could not connect.";
                    }
                    catch (TaskCanceledException)
                    {
                        LastAddonSyncMessage = "Stremio account sync timed out. Try again when the connection is stable.";
                    }
                    catch (Exception ex)
                    {
                        LastAddonSyncMessage = "Could not refresh the saved Stremio session: " + ex.Message;
                    }
                }

                _initialized = true;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        private Task LoadSavedSessionAsync()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            AuthKey = null;

            // Treat a stale PasswordVault entry as a missing session. On some Windows builds
            // FindAllByResource succeeds but RetrievePassword throws 0x80070490 because the
            // underlying secret has already disappeared.
            PasswordVault? vault = null;
            try
            {
                vault = new PasswordVault();
                var credentials = vault.FindAllByResource(VaultResource);
                var credential = credentials.FirstOrDefault(item => item.UserName == VaultUserName);
                if (credential != null)
                {
                    try
                    {
                        credential.RetrievePassword();
                        if (!string.IsNullOrWhiteSpace(credential.Password))
                            AuthKey = credential.Password;
                    }
                    catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException)
                    {
                        // Remove only the broken credential; never let a stale vault record
                        // prevent the app from starting or migrating an older session.
                        try { vault.Remove(credential); }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception)
            {
                // PasswordVault can be unavailable in restricted app states. Continue signed out
                // and allow the legacy-token migration below.
                AuthKey = null;
            }

            // Migrate the previous LocalSettings token into the encrypted credential vault.
            if (string.IsNullOrWhiteSpace(AuthKey) &&
                settings.TryGetValue(AuthKeySetting, out var legacyKey) &&
                legacyKey is string legacy && !string.IsNullOrWhiteSpace(legacy))
            {
                AuthKey = legacy;
                if (PersistAuthKey())
                    settings.Remove(AuthKeySetting);
            }

            if (settings.TryGetValue(UserSetting, out var userObj) &&
                userObj is string userJson && !string.IsNullOrWhiteSpace(userJson))
            {
                try { User = JsonSerializer.Deserialize(userJson, StremioJsonContext.Default.StremioUser); }
                catch { User = null; }
            }

            return Task.CompletedTask;
        }

        public async Task<DeviceLinkSession> StartDeviceLinkAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _http.GetAsync(LinkCreateUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var envelopeTypeInfo = StremioJsonContext.Default.GetTypeInfo(typeof(StremioApiEnvelope<DeviceLinkCreateResult>))
                as System.Text.Json.Serialization.Metadata.JsonTypeInfo<StremioApiEnvelope<DeviceLinkCreateResult>>
                ?? throw new NotSupportedException("Device-link response metadata is unavailable.");
            var envelope = await JsonSerializer.DeserializeAsync(stream, envelopeTypeInfo, cancellationToken).ConfigureAwait(false);

            if (envelope?.Error != null)
                throw new InvalidOperationException(envelope.Error.Message ?? "Failed to create link code");

            var result = envelope?.Result ?? throw new InvalidOperationException("Empty link create response");
            if (string.IsNullOrWhiteSpace(result.Code))
                throw new InvalidOperationException("Stremio did not return a device-link code.");

            var link = result.Link ?? "https://link.stremio.com/" + result.Code;
            var qr = result.Qrcode ??
                "https://api.qrserver.com/v1/create-qr-code/?size=280x280&data=" + Uri.EscapeDataString(link);

            return new DeviceLinkSession { Code = result.Code, LinkUrl = link, QrImageUrl = qr };
        }

        public async Task<bool> PollDeviceLinkAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;

            var url = "https://link.stremio.com/api/v2/read?type=Read&code=" +
                Uri.EscapeDataString(code.Trim().ToUpperInvariant());
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("error", out _)) return false;
            if (!document.RootElement.TryGetProperty("result", out var result)) return false;

            var authKey = TryGetString(result, "authKey");
            var user = TryGetUser(result);
            if (authKey == null && result.TryGetProperty("result", out var nested))
            {
                authKey = TryGetString(nested, "authKey");
                user ??= TryGetUser(nested);
            }
            if (string.IsNullOrWhiteSpace(authKey)) return false;

            AuthKey = authKey;
            User = user;
            Persist();
            SessionChanged?.Invoke(this, EventArgs.Empty);
            if (User == null)
            {
                try { await RefreshUserAsync(cancellationToken).ConfigureAwait(false); }
                catch { /* Account key is still valid if the optional profile request is temporarily unavailable. */ }
            }

            await SyncAfterLoginAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task LoginWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Email and password are required.");

            // Stremio puts the API method in the URL; login's JSON body is { email, password }.
            var result = await PostApiAsync<LoginResult>("login",
                new StremioApiRequest { Email = email.Trim(), Password = password },
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result?.AuthKey))
                throw new InvalidOperationException("Stremio login did not return an auth key.");

            AuthKey = result.AuthKey;
            User = result.User;
            Persist();
            SessionChanged?.Invoke(this, EventArgs.Empty);
            await SyncAfterLoginAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task LoginWithAuthKeyAsync(string authKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(authKey))
                throw new ArgumentException("An auth key is required.", nameof(authKey));

            var previousKey = AuthKey;
            var previousUser = User;
            AuthKey = authKey.Trim();
            try
            {
                await RefreshUserAsync(cancellationToken).ConfigureAwait(false);
                Persist();
                SessionChanged?.Invoke(this, EventArgs.Empty);
                await SyncAfterLoginAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                AuthKey = previousKey;
                User = previousUser;
                throw;
            }
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoggedIn)
            {
                try
                {
                    await PostApiAsync<JsonElement>("logout",
                        new StremioApiRequest { AuthKey = AuthKey }, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Always clear the local session, even if the service is offline.
                }
            }

            AuthKey = null;
            User = null;
            LastAddonSyncMessage = "Signed out of Stremio.";
            ClearPersist();
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task RefreshUserAsync(CancellationToken cancellationToken = default)
        {
            if (!IsLoggedIn) return;
            var user = await PostApiAsync<StremioUser>("getUser",
                new StremioApiRequest { AuthKey = AuthKey }, cancellationToken).ConfigureAwait(false);
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
                throw new InvalidDataException("Stremio returned an invalid account profile.");

            User = user;
            Persist();
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<AddonAccountSyncResult> SyncAddonCollectionAsync(CancellationToken cancellationToken = default)
        {
            if (!IsLoggedIn) throw new InvalidOperationException("Sign in to Stremio before synchronizing account add-ons.");

            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var remote = await PostApiAsync<StremioAddonCollectionResult>("addonCollectionGet",
                    new StremioApiRequest
                    {
                        AuthKey = AuthKey,
                        Update = true,
                        AddFromURL = new List<string>()
                    }, cancellationToken).ConfigureAwait(false);

                if (remote?.Addons == null)
                    throw new InvalidDataException("Stremio returned an invalid add-on collection.");

                var result = new AddonAccountSyncResult();
                foreach (var descriptor in remote.Addons)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var transportUrl = descriptor.TransportUrl?.Trim();
                    if (string.IsNullOrWhiteSpace(transportUrl) || descriptor.Manifest == null)
                    {
                        result.Failed++;
                        continue;
                    }

                    if (!Uri.TryCreate(transportUrl, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                        !uri.AbsolutePath.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Legacy and IPFS transports need their own transport adapters.
                        result.Failed++;
                        continue;
                    }

                    try
                    {
                        await _addonService.InstallFromManifestUrlAsync(transportUrl, cancellationToken).ConfigureAwait(false);
                        result.Pulled++;
                    }
                    catch
                    {
                        result.Failed++;
                    }
                }

                // Never overwrite the cloud collection if any remote item failed to import.
                if (result.Failed > 0)
                {
                    result.Message = $"Downloaded {result.Pulled} account add-on(s); {result.Failed} could not be imported. Cloud collection was left unchanged.";
                    LastAddonSyncMessage = result.Message;
                    return result;
                }

                var localDescriptors = _addonService.InstalledAddons
                    .Select(ToDescriptor)
                    .Where(item => item != null)
                    .Cast<StremioAddonDescriptor>()
                    .ToList();

                // Keep trusted/protected flags for descriptors already present in the account.
                var remoteByUrl = remote.Addons
                    .Where(item => !string.IsNullOrWhiteSpace(item.TransportUrl))
                    .GroupBy(item => item.TransportUrl!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var localDescriptor in localDescriptors)
                {
                    if (localDescriptor.TransportUrl != null &&
                        remoteByUrl.TryGetValue(localDescriptor.TransportUrl, out var existingRemote) &&
                        existingRemote.Flags.HasValue)
                    {
                        localDescriptor.Flags = existingRemote.Flags.Value.Clone();
                    }
                }

                await PostApiAsync<JsonElement>("addonCollectionSet",
                    new StremioApiRequest { AuthKey = AuthKey, Addons = localDescriptors },
                    cancellationToken).ConfigureAwait(false);

                result.Pushed = localDescriptors.Count;
                result.Message = $"Stremio account synchronized: {result.Pulled} downloaded, {result.Pushed} add-ons saved to your account.";
                LastAddonSyncMessage = result.Message;
                return result;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task SyncAfterLoginAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await SyncAddonCollectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LastAddonSyncMessage = "Signed in. Add-on cloud sync will need a retry: " + ex.Message;
            }
        }

        private static StremioAddonDescriptor? ToDescriptor(InstalledAddon addon)
        {
            if (string.IsNullOrWhiteSpace(addon.ManifestUrl) || string.IsNullOrWhiteSpace(addon.Id))
                return null;

            return new StremioAddonDescriptor
            {
                TransportUrl = addon.ManifestUrl,
                Manifest = new AddonManifest
                {
                    Id = addon.Id,
                    Name = addon.Name,
                    Description = addon.Description,
                    Version = addon.Version,
                    Logo = addon.Logo,
                    Types = addon.Types.ToList(),
                    Resources = addon.ManifestResources.Count > 0
                        ? addon.ManifestResources.Select(resource => resource.Clone()).ToList()
                        : addon.Resources.Select(resource => JsonSerializer.SerializeToElement(resource, StremioJsonContext.Default.String)).ToList(),
                    Catalogs = addon.Catalogs.ToList(),
                    IdPrefixes = addon.IdPrefixes.ToList(),
                    AdditionalProperties = addon.ManifestAdditionalProperties.ToDictionary(pair => pair.Key, pair => pair.Value.Clone())
                }
            };
        }

        private async Task<T?> PostApiAsync<T>(string method, StremioApiRequest body, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body, StremioJsonContext.Default.StremioApiRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(ApiBase + method, content, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Stremio API {method} returned {(int)response.StatusCode}: {responseBody}");

            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var message = TryGetString(error, "message") ?? $"Stremio API {method} failed.";
                throw new InvalidOperationException(message);
            }
            if (!document.RootElement.TryGetProperty("result", out var result))
                throw new InvalidDataException($"Stremio API {method} response did not contain a result.");

            var typeInfo = StremioJsonContext.Default.GetTypeInfo(typeof(T))
                as System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>
                ?? throw new NotSupportedException($"No source-generated API response metadata registered for {typeof(T).FullName}.");

            return JsonSerializer.Deserialize(result.GetRawText(), typeInfo);
        }

        private void Persist()
        {
            PersistAuthKey();
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (User != null)
                settings[UserSetting] = JsonSerializer.Serialize(User, StremioJsonContext.Default.StremioUser);
            else
                settings.Remove(UserSetting);
        }

        private bool PersistAuthKey()
        {
            if (string.IsNullOrWhiteSpace(AuthKey)) return false;
            try
            {
                var vault = new PasswordVault();
                RemoveVaultCredentials(vault);
                vault.Add(new PasswordCredential(VaultResource, VaultUserName, AuthKey));
                ApplicationData.Current.LocalSettings.Values.Remove(AuthKeySetting);
                return true;
            }
            catch
            {
                // Never write a new auth key in plaintext LocalSettings.
                return false;
            }
        }

        private void ClearPersist()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings.Remove(AuthKeySetting);
            settings.Remove(UserSetting);
            try { RemoveVaultCredentials(new PasswordVault()); }
            catch { }
        }

        private static void RemoveVaultCredentials(PasswordVault vault)
        {
            IReadOnlyList<PasswordCredential> credentials;
            try
            {
                // FindAllByResource throws HRESULT 0x80070490 when the resource
                // has no entries. This is expected on a first login or after logout.
                credentials = vault.FindAllByResource(VaultResource);
            }
            catch (System.Runtime.InteropServices.COMException ex)
                when (ex.HResult == unchecked((int)0x80070490))
            {
                return;
            }
            catch (Exception)
            {
                // Credential cleanup is best-effort. The vault may be empty,
                // unavailable, or report a WinRT-specific failure. Do not let
                // that prevent saving a newly received authentication key.
                return;
            }

            foreach (var credential in credentials)
            {
                try
                {
                    vault.Remove(credential);
                }
                catch (Exception)
                {
                    // Entries can disappear between enumeration and removal.
                    // Ignore cleanup failures; PersistAuthKey will still try Add.
                }
            }
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
                return property.GetString();
            return null;
        }

        private static StremioUser? TryGetUser(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("user", out var userElement))
                return null;
            return JsonSerializer.Deserialize(userElement.GetRawText(), StremioJsonContext.Default.StremioUser);
        }
    }
}
