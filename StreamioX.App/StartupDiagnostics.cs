// Temporary startup diagnostics. Remove after the startup failure is isolated.
using System;
using System.IO;

namespace StreamioX.App
{
    internal static class StartupDiagnostics
    {
        private static readonly object Sync = new();

        internal static void Write(string stage, Exception? exception = null)
        {
            try
            {
                // Keep the first startup log independent of WinRT package storage.
                // ApplicationData.Current can itself require COM activation while App is starting.
                var path = Path.Combine(Path.GetTempPath(), "StreamioX-startup-diagnostics.log");
                var detail = exception == null ? string.Empty : Environment.NewLine + exception;
                lock (Sync)
                    File.AppendAllText(path,
                        $"[{DateTimeOffset.Now:O}] {stage}{detail}{Environment.NewLine}");
            }
            catch
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "StreamioX-startup-diagnostics.log"),
                        $"[{DateTimeOffset.Now:O}] {stage}{Environment.NewLine}{exception}{Environment.NewLine}");
                }
                catch { }
            }
        }
    }
}
