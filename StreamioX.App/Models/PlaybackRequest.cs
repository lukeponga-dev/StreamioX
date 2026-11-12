// Purpose: Defines PlaybackRequest data exchanged between app features and external Stremio-compatible services.

using System.Collections.Generic;

namespace StreamioX.App.Models
{
    public sealed class PlaybackRequest
    {
        public string StreamUrl { get; }
        public string Title { get; }
        public string? SeriesTitle { get; }
        public IReadOnlyDictionary<string, string>? RequestHeaders { get; }
        public string? Type { get; }
        public string? Id { get; }
        public string? EpisodeId { get; }
        public Movie? Movie { get; }
        public double ResumePositionSeconds { get; }

        public PlaybackRequest(string streamUrl, string title, string? seriesTitle = null,
            IReadOnlyDictionary<string, string>? requestHeaders = null,
            string? type = null, string? id = null, string? episodeId = null,
            Movie? movie = null, double resumePositionSeconds = 0)
        {
            StreamUrl = streamUrl;
            Title = string.IsNullOrWhiteSpace(title) ? "Now Playing" : title;
            SeriesTitle = seriesTitle;
            RequestHeaders = requestHeaders == null
                ? null
                : new Dictionary<string, string>(requestHeaders);
            Type = type ?? movie?.Type;
            Id = id ?? movie?.Id;
            EpisodeId = episodeId;
            Movie = movie;
            ResumePositionSeconds = System.Math.Max(0, resumePositionSeconds);
        }
    }
}
