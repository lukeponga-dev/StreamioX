using Windows.Media.Playback;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Owns the app-wide MediaPlayer so its native playback pipeline is created
    /// before the user opens the player page.
    /// </summary>
    public sealed class PlaybackSessionService
    {
        public MediaPlayer Player { get; }

        public PlaybackSessionService()
        {
            Player = new MediaPlayer();
        }

        /// <summary>
        /// Touches the playback session during app startup to activate the
        /// underlying media pipeline before the first Play action.
        /// Call from the UI thread because the app hosts this player in XAML.
        /// </summary>
        public void WarmUp()
        {
            _ = Player.PlaybackSession.PlaybackState;
            PlaybackDiagnostics.Write("[Startup] Shared MediaPlayer session pre-warmed.");
        }
    }
}
