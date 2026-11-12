// Purpose: Declares the ILibraryService service contract used to keep features independent of concrete implementations. Implements ILibraryService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System.Collections.Generic;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface ILibraryService
    {
        Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync();
        Task<bool> IsSavedAsync(string type, string id);
        Task SaveAsync(Movie movie);
        Task RemoveAsync(string type, string id);
        Task UpdateProgressAsync(string type, string id, double positionSeconds, double durationSeconds, string? episodeId = null, Movie? movie = null);
    }
}
