// Purpose: Owns LibraryViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly ILibraryService _library;
        private readonly INavigationService _navigation;

        [ObservableProperty] private ObservableCollection<LibraryEntry> entries = new();
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string status = "Your saved titles will appear here.";
        public bool HasEntries => Entries.Count > 0;

        public LibraryViewModel(ILibraryService library, INavigationService navigation)
        {
            _library = library;
            _navigation = navigation;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                Entries.Clear();
                foreach (var entry in await _library.GetEntriesAsync())
                {
                    if (entry.IsSaved)
                        Entries.Add(entry);
                }
                Status = Entries.Count == 0 ? "Your library is empty. Save movies and series from their details pages." : $"{Entries.Count} saved title(s)";
                OnPropertyChanged(nameof(HasEntries));
            }
            catch (Exception ex) { Status = $"Unable to load your library: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void OpenEntry(LibraryEntry? entry)
        {
            if (entry?.Movie != null) _navigation.NavigateTo(NavigationRoute.Details, entry.Movie);
        }

        [RelayCommand]
        private async Task RemoveEntry(LibraryEntry? entry)
        {
            if (entry?.Movie?.Id == null) return;
            await _library.RemoveAsync(entry.Movie.Type ?? "movie", entry.Movie.Id);
            Entries.Remove(entry);
            Status = Entries.Count == 0 ? "Your library is empty. Save movies and series from their details pages." : $"{Entries.Count} saved title(s)";
            OnPropertyChanged(nameof(HasEntries));
        }
    }
}
