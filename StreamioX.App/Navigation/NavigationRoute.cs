namespace StreamioX.App.Navigation
{
    /// <summary>
    /// Strongly typed navigation destinations corresponding to the core StremioX views.
    /// Prevents magic strings when routing between the browse and playback flows.
    /// </summary>
    public enum NavigationRoute
    {
        Home,
        Discover,
        Library,
        Search,
        Addons,
        Details,
        Player,
        Diagnostics,
        Settings
    }
}
