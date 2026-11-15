// Purpose: Carries a media item and an optional auto-resume intent into the details route.

namespace StreamioX.App.Models
{
    public sealed class DetailsNavigationRequest
    {
        public Movie Movie { get; }
        public bool ResumeAutomatically { get; }

        public DetailsNavigationRequest(Movie movie, bool resumeAutomatically = false)
        {
            Movie = movie ?? throw new System.ArgumentNullException(nameof(movie));
            ResumeAutomatically = resumeAutomatically;
        }
    }
}
