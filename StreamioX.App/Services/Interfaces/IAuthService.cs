using System;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface IAuthService
    {
        event EventHandler? SessionChanged;
        bool IsLoggedIn { get; }
        string? AuthKey { get; }
        StremioUser? User { get; }
        string LastAddonSyncMessage { get; }

        Task InitializeAsync();
        Task<DeviceLinkSession> StartDeviceLinkAsync(CancellationToken cancellationToken = default);
        Task<bool> PollDeviceLinkAsync(string code, CancellationToken cancellationToken = default);
        Task LoginWithEmailAsync(string email, string password, CancellationToken cancellationToken = default);
        Task LoginWithAuthKeyAsync(string authKey, CancellationToken cancellationToken = default);
        Task LogoutAsync(CancellationToken cancellationToken = default);
        Task RefreshUserAsync(CancellationToken cancellationToken = default);
        Task<AddonAccountSyncResult> SyncAddonCollectionAsync(CancellationToken cancellationToken = default);
        Task<bool> PushAddonCollectionAsync(CancellationToken cancellationToken = default);
    }
}
