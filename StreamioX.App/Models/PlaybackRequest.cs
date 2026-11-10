using System.Collections.Generic;

namespace StreamioX.App.Models
{
    public sealed class PlaybackRequest
    {
        public string StreamUrl { get; }
        public string Title { get; }
        public string? SeriesTitle { get; }
        public IReadOnlyDictionary<string, string>? RequestHeaders { get; }

        public PlaybackRequest(string streamUrl, string title, string? seriesTitle = null,
            IReadOnlyDictionary<string, string>? requestHeaders = null)
        {
            StreamUrl = streamUrl;
            Title = string.IsNullOrWhiteSpace(title) ? "Now Playing" : title;
            SeriesTitle = seriesTitle;
            RequestHeaders = requestHeaders == null
                ? null
                : new Dictionary<string, string>(requestHeaders);
        }
    }
}
