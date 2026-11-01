
using CommunityToolkit.Mvvm.ComponentModel;
using StreamioX.App.Models;
using StreamioX.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WinRT;

namespace StreamioX.App.ViewModels
{
    [GeneratedBindableCustomProperty]
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;

        [ObservableProperty]
        public partial ObservableCollection<Movie> Movies { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string? ErrorMessage { get; set; }

        public string DefaultAddonUrl { get; set; } =
            "https://v3-cinemeta.strem.io";

        public DiscoverViewModel(ICatalogService catalogService)
        {
            _catalogService = catalogService
                ?? throw new ArgumentNullException(nameof(catalogService));

            Movies = new ObservableCollection<Movie>();
        }

        public async Task LoadCatalogAsync(string? addonUrl = null)
        {
            if (IsLoading)
                return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(addonUrl)
                    ? DefaultAddonUrl
                    : addonUrl.Trim();

                Movies.Clear();

                try
                {
                    var movieItems = await _catalogService.GetCatalogAsync(
                        targetUrl, "movie", "top");

                    foreach (var item in movieItems)
                    {
                        if (item == null ||
                            string.IsNullOrWhiteSpace(item.Id))
                            continue;

                        item.Type = "movie";
                        Movies.Add(item);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[Discover] Movies received: {Movies.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Discover] Movie request failed: {ex}");
                }

                int movieCount = Movies.Count;

                try
                {
                    var seriesItems = await _catalogService.GetCatalogAsync(
                        targetUrl, "series", "top");

                    foreach (var item in seriesItems)
                    {
                        if (item == null ||
                            string.IsNullOrWhiteSpace(item.Id))
                            continue;

                        item.Type = "series";
                        Movies.Add(item);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[Discover] Series received: {Movies.Count - movieCount}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Discover] Series request failed: {ex}");
                }

                if (Movies.Count == 0)
                {
                    ErrorMessage = "No catalogue items were returned.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load the catalogue.";

                System.Diagnostics.Debug.WriteLine(
                    $"[Discover] Catalog error: {ex}");
            }
            finally
            {
                IsLoading = false;

                System.Diagnostics.Debug.WriteLine(
                    $"[VM] Collection type: {Movies.GetType().FullName}");

                System.Diagnostics.Debug.WriteLine(
                    $"[VM] Collection count: {Movies.Count}");

                System.Diagnostics.Debug.WriteLine(
                    $"[VM] UI thread access: {Windows.UI.Xaml.Window.Current.Dispatcher.HasThreadAccess}");
            }
        }

        public async Task RefreshCatalogAsync()
        {
            await LoadCatalogAsync();
        }
    }
}
