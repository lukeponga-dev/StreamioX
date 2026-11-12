// Purpose: Implements PlaybackDiagnostics application behavior and coordinates the related Stremio, playback, or platform APIs.

using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Writes playback diagnostics to the app's LocalState folder so Xbox and
    /// packaged runs can be inspected without an attached Visual Studio debugger.
    /// </summary>
    public static class PlaybackDiagnostics
    {
        private static readonly object Sync = new();
        private const string FileName = "playback-diagnostics.log";

        public static string LogPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);

        public static void Write(string message)
        {
            var line = $"{DateTimeOffset.Now:O} {message}";
            Debug.WriteLine(line);
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackDiagnostics] File logging failed: 0x{ex.HResult:X8} {ex.Message}");
            }
        }
    }
}
