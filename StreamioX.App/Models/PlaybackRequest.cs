using System.Collections.Generic;

namespace StreamioX.App.Models
{
    public sealed class PlaybackRequest
    {
        public string StreamUrl { get; }
        public string Title { get; }
        public string? SeriesTitle { get; }
        public IReadOnlyDictionary<string, string>? RequestHeaders { get; }

        // Identity used to save watch progress; null when the source is not a catalog title.
        public Movie? Movie { get; }
        public string? EpisodeId { get; }

        public PlaybackRequest(string streamUrl, string title, string? seriesTitle = null,
            IReadOnlyDictionary<string, string>? requestHeaders = null,
            Movie? movie = null, string? episodeId = null)
        {
            Movie = movie;
            EpisodeId = episodeId;
            StreamUrl = streamUrl;
            Title = string.IsNullOrWhiteSpace(title) ? "Now Playing" : title;
            SeriesTitle = seriesTitle;
            RequestHeaders = requestHeaders == null
                ? null
                : new Dictionary<string, string>(requestHeaders);
        }
    }
}
