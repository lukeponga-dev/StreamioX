using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public sealed class SearchViewModel
    {
        private readonly ICatalogService _catalogService;

        public ObservableCollection<Movie> Results { get; } = new();
        public event EventHandler? ResultsChanged;
        public event EventHandler<string>? StatusChanged;

        public SearchViewModel(ICatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        }

        public async Task SearchAsync(string query)
        {
            query = query?.Trim() ?? string.Empty;
            if (query.Length == 0)
            {
                StatusChanged?.Invoke(this, "Type a movie or series title to search.");
                return;
            }

            Results.Clear();
            ResultsChanged?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, $"Searching add-ons for “{query}”...");

            try
            {
                var matches = await _catalogService.SearchInstalledAddonsAsync(query);
                foreach (var item in matches)
                    Results.Add(item);

                StatusChanged?.Invoke(this, Results.Count == 0
                    ? $"No results for “{query}”. Try another title or install another catalog add-on."
                    : $"{Results.Count} result(s) for “{query}”. Select a title to view details.");
                ResultsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Search failed: {ex.Message}");
            }
        }
    }
}