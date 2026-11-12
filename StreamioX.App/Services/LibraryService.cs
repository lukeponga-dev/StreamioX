// Purpose: Implements LibraryService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed class LibraryService : ILibraryService
    {
        private const string FileName = "library.json";
        private readonly SemaphoreSlim _gate = new(1, 1);
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            TypeInfoResolver = StremioJsonContext.Default
        };

        public async Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { return (await LoadAsync().ConfigureAwait(false)).OrderByDescending(x => x.LastPlayedUtc ?? x.AddedUtc).ToList(); }
            finally { _gate.Release(); }
        }

        public async Task<bool> IsSavedAsync(string type, string id)
        {
            var entries = await GetEntriesAsync().ConfigureAwait(false);
            return entries.Any(x => x.IsSaved && Matches(x, type, id));
        }

        public async Task SaveAsync(Movie movie)
        {
            if (movie == null || string.IsNullOrWhiteSpace(movie.Id)) throw new ArgumentException("A title ID is required.", nameof(movie));
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var entries = await LoadAsync().ConfigureAwait(false);
                var existing = entries.FirstOrDefault(x => Matches(x, movie.Type ?? "movie", movie.Id));
                if (existing == null)
                    entries.Add(new LibraryEntry { Movie = movie, AddedUtc = DateTimeOffset.UtcNow });
                else
                {
                    existing.Movie = movie;
                    existing.IsSaved = true;
                }
                await SaveFileAsync(entries).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        public async Task RemoveAsync(string type, string id)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var entries = await LoadAsync().ConfigureAwait(false);
                entries.RemoveAll(x => Matches(x, type, id));
                await SaveFileAsync(entries).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Stores the latest playback position and duration for resume-watching behavior.</summary>
        public async Task UpdateProgressAsync(string type, string id, double positionSeconds, double durationSeconds,
            string? episodeId = null, Movie? movie = null)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
                return;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var entries = await LoadAsync().ConfigureAwait(false);
                var entry = entries.FirstOrDefault(x => Matches(x, type, id));

                // Playback creates a Continue Watching entry even when the title
                // has not separately been added to the user's saved library.
                if (entry == null && movie != null && !string.IsNullOrWhiteSpace(movie.Id))
                {
                    entry = new LibraryEntry
                    {
                        Movie = movie,
                        IsSaved = false,
                        AddedUtc = DateTimeOffset.UtcNow
                    };
                    entries.Add(entry);
                }

                if (entry != null)
                {
                    if (movie != null)
                        entry.Movie = movie;
                    entry.PositionSeconds = Math.Max(0, positionSeconds);
                    entry.DurationSeconds = Math.Max(0, durationSeconds);
                    entry.LastEpisodeId = episodeId;
                    entry.LastPlayedUtc = DateTimeOffset.UtcNow;
                    await SaveFileAsync(entries).ConfigureAwait(false);
                }
            }
            finally { _gate.Release(); }
        }

        private static bool Matches(LibraryEntry entry, string type, string id) =>
            string.Equals(entry.Movie?.Id, id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Movie?.Type ?? "movie", type, StringComparison.OrdinalIgnoreCase);

        /// <summary>Reads the persisted library entries from the app local data folder.</summary>
        private static async Task<List<LibraryEntry>> LoadAsync()
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
                if (!File.Exists(path)) return new List<LibraryEntry>();
                var info = StremioJsonContext.Default.GetTypeInfo(typeof(List<LibraryEntry>)) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<LibraryEntry>>
                    ?? throw new NotSupportedException("Library JSON metadata is unavailable.");
                return JsonSerializer.Deserialize(await File.ReadAllTextAsync(path).ConfigureAwait(false), info) ?? new List<LibraryEntry>();
            }
            catch (FileNotFoundException) { return new List<LibraryEntry>(); }
        }

        /// <summary>Writes the updated library snapshot to local storage.</summary>
        private static async Task SaveFileAsync(List<LibraryEntry> entries)
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
            var info = StremioJsonContext.Default.GetTypeInfo(typeof(List<LibraryEntry>)) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<LibraryEntry>>
                ?? throw new NotSupportedException("Library JSON metadata is unavailable.");
            var json = JsonSerializer.Serialize(entries, info);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json).ConfigureAwait(false);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
