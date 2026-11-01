using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed class AuthService : IAuthService
    {
        private const string AuthKeySetting = "StremioAuthKey";
        private const string UserSetting = "StremioUserJson";
        private const string ApiBase = "https://api.strem.io/api/";
        private const string LinkCreateUrl = "https://link.stremio.com/api/v2/create?type=Create";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;

        public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AuthKey);
        public string? AuthKey { get; private set; }
        public StremioUser? User { get; private set; }

        public AuthService(HttpClient httpClient)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task InitializeAsync()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.TryGetValue(AuthKeySetting, out var keyObj) && keyObj is string key
                    && !string.IsNullOrWhiteSpace(key))
                {
                    AuthKey = key;
                }

                if (settings.TryGetValue(UserSetting, out var userObj) && userObj is string userJson
                    && !string.IsNullOrWhiteSpace(userJson))
                {
                    User = JsonSerializer.Deserialize<StremioUser>(userJson, JsonOptions);
                }
            }
            catch
            {
                // ignore corrupt storage
            }

            return Task.CompletedTask;
        }

        public async Task<DeviceLinkSession> StartDeviceLinkAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _http.GetAsync(LinkCreateUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var envelope = await JsonSerializer.DeserializeAsync<StremioApiEnvelope<DeviceLinkCreateResult>>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (envelope?.Error != null)
                throw new InvalidOperationException(envelope.Error.Message ?? "Failed to create link code");

            var result = envelope?.Result
                         ?? throw new InvalidOperationException("Empty link create response");

            if (string.IsNullOrWhiteSpace(result.Code))
                throw new InvalidOperationException("No link code returned");

            var link = result.Link ?? $"https://link.stremio.com/{result.Code}";
            var qr = result.Qrcode
                     ?? $"https://api.qrserver.com/v1/create-qr-code/?size=280x280&data={Uri.EscapeDataString(link)}";

            return new DeviceLinkSession
            {
                Code = result.Code!,
                LinkUrl = link,
                QrImageUrl = qr
            };
        }

        public async Task<bool> PollDeviceLinkAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var url = $"https://link.stremio.com/api/v2/read?type=Read&code={Uri.EscapeDataString(code.ToUpperInvariant())}";

            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("error", out _))
                return false;

            string? authKey = null;
            StremioUser? user = null;

            if (doc.RootElement.TryGetProperty("result", out var resultEl))
            {
                // result may be the auth payload or nested
                authKey = TryGetString(resultEl, "authKey");
                if (resultEl.TryGetProperty("user", out var userEl))
                    user = JsonSerializer.Deserialize<StremioUser>(userEl.GetRawText(), JsonOptions);

                if (authKey == null && resultEl.TryGetProperty("result", out var nested))
                {
                    authKey = TryGetString(nested, "authKey");
                    if (nested.TryGetProperty("user", out var nestedUser))
                        user = JsonSerializer.Deserialize<StremioUser>(nestedUser.GetRawText(), JsonOptions);
                }

                // Sometimes authKey is the whole result as string? unlikely
            }

            if (string.IsNullOrWhiteSpace(authKey))
                return false;

            AuthKey = authKey;
            User = user;
            Persist();

            if (User == null)
            {
                try { await RefreshUserAsync(cancellationToken).ConfigureAwait(false); }
                catch { /* optional */ }
            }

            return true;
        }

        public async Task LoginWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Email and password are required.");

            var payload = new
            {
                type = "Login",
                email,
                password,
                facebook = false
            };

            var result = await PostApiAsync<LoginResult>("login", payload, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result?.AuthKey))
                throw new InvalidOperationException("Login failed: no auth key returned.");

            AuthKey = result!.AuthKey;
            User = result.User;
            Persist();
        }

        public async Task LoginWithAuthKeyAsync(string authKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(authKey))
                throw new ArgumentException("Auth key is required.");

            AuthKey = authKey.Trim();
            Persist();
            await RefreshUserAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(AuthKey))
            {
                try
                {
                    await PostApiAsync<object>("logout", new { type = "Logout", authKey = AuthKey }, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // still clear local session
                }
            }

            AuthKey = null;
            User = null;
            ClearPersist();
        }

        public async Task RefreshUserAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(AuthKey))
                return;

            var user = await PostApiAsync<StremioUser>("getUser", new { type = "GetUser", authKey = AuthKey }, cancellationToken)
                .ConfigureAwait(false);
            if (user != null)
            {
                User = user;
                Persist();
            }
        }

        private async Task<T?> PostApiAsync<T>(string method, object body, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(ApiBase + method, content, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var envelope = await JsonSerializer.DeserializeAsync<StremioApiEnvelope<T>>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (envelope?.Error != null)
                throw new InvalidOperationException(envelope.Error.Message ?? "API error");

            return envelope is null ? default : envelope.Result;
        }

        private void Persist()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (AuthKey != null)
                settings[AuthKeySetting] = AuthKey;
            else
                settings.Remove(AuthKeySetting);

            if (User != null)
                settings[UserSetting] = JsonSerializer.Serialize(User, JsonOptions);
            else
                settings.Remove(UserSetting);
        }

        private void ClearPersist()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings.Remove(AuthKeySetting);
            settings.Remove(UserSetting);
        }

        private static string? TryGetString(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            return null;
        }
    }
}
