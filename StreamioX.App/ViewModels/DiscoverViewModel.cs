// ViewModels/DiscoverViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// ViewModel for browsing Stremio add-on catalogs and displaying media cards.
    /// </summary>
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;

        /// <summary>
        /// Collection of movies fetched from the Stremio add-on catalog.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Movie> movies = new ObservableCollection<Movie>();

        /// <summary>
        /// Indicates whether a catalog fetch request is currently in progress[cite: 1, 2].
        /// </summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Default Cinemeta endpoint for public catalog metadata[cite: 1, 2].
        /// </summary>
        public string DefaultAddonUrl { get; set; } = "https://cinemeta-stremio.strem.fun";

        public DiscoverViewModel(ICatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        }

        /// <summary>
        /// Fetches media items from a specified Stremio catalog endpoint[cite: 1, 2].
        /// </summary>
        [RelayCommand]
        public async Task LoadCatalogAsync(string addonUrl = null)
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(addonUrl) ? DefaultAddonUrl : addonUrl;

                // Query popular movies from Cinemeta catalog endpoint[cite: 1, 2]
                var items = await _catalogService.GetCatalogAsync(targetUrl, "movie", "top");

                Movies.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        Movies.Add(item);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}