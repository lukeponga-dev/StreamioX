using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StreamioX.App;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using Xunit;

namespace StreamioX.App.Tests;

public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddStreamioXServices(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void ServiceProvider_BuildsWithValidationEnabled()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void EveryRegisteredApplicationService_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddStreamioXServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        foreach (var serviceType in services
            .Select(descriptor => descriptor.ServiceType)
            .Where(serviceType => !serviceType.ContainsGenericParameters)
            .Distinct())
        {
            Assert.NotNull(provider.GetRequiredService(serviceType));
        }

        Assert.NotNull(provider.GetRequiredService<HttpClient>());
        Assert.NotNull(provider.GetRequiredService<StremioClient>());
    }

    [Fact]
    public void AllViewModels_AreRegisteredAsTransient()
    {
        var services = new ServiceCollection();
        services.AddStreamioXServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var viewModels = services
            .Where(descriptor => descriptor.ServiceType.Namespace == "StreamioX.App.ViewModels")
            .Select(descriptor => descriptor.ServiceType)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(viewModels);
        foreach (var viewModel in viewModels)
        {
            var registration = services.Last(descriptor => descriptor.ServiceType == viewModel);
            Assert.Equal(ServiceLifetime.Transient, registration.Lifetime);
            Assert.NotNull(provider.GetRequiredService(viewModel));
            Assert.NotSame(provider.GetRequiredService(viewModel), provider.GetRequiredService(viewModel));
        }
    }

    [Fact]
    public void CoreServices_AreSingletons_AndNavigationResolves()
    {
        var services = new ServiceCollection();
        services.AddStreamioXServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var singletonServices = new[]
        {
            typeof(INavigationService), typeof(IAuthService), typeof(IAddonService),
            typeof(ILibraryService), typeof(ICatalogService), typeof(IMetadataService),
            typeof(IStreamService), typeof(ITorrentBridgeService),
            typeof(FFmpegPlaybackService), typeof(PlaybackSessionService), typeof(HttpClient)
        };

        foreach (var serviceType in singletonServices)
        {
            var registration = services.Last(descriptor => descriptor.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
            var first = provider.GetRequiredService(serviceType);
            var second = provider.GetRequiredService(serviceType);
            Assert.Same(first, second);
        }

        Assert.IsType<NavigationService>(provider.GetRequiredService<INavigationService>());
    }

    [Fact]
    public void ScopedAndTransientLifetimes_AreIsolatedPerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SingletonProbe>();
        services.AddScoped<ScopedProbe>();
        services.AddTransient<TransientProbe>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.Same(provider.GetRequiredService<SingletonProbe>(),
            firstScope.ServiceProvider.GetRequiredService<SingletonProbe>());
        Assert.Same(firstScope.ServiceProvider.GetRequiredService<ScopedProbe>(),
            firstScope.ServiceProvider.GetRequiredService<ScopedProbe>());
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<ScopedProbe>(),
            secondScope.ServiceProvider.GetRequiredService<ScopedProbe>());
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<TransientProbe>(),
            firstScope.ServiceProvider.GetRequiredService<TransientProbe>());
    }

    [Fact]
    public void HttpClientRegistrations_AreConfiguredAndResolvable()
    {
        using var provider = BuildProvider();
        var sharedClient = provider.GetRequiredService<HttpClient>();
        var typedClient = provider.GetRequiredService<StremioClient>();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.NotNull(factory.CreateClient("StremioAuth"));
        Assert.Equal(TimeSpan.FromSeconds(30), sharedClient.Timeout);
        Assert.Contains(sharedClient.DefaultRequestHeaders.UserAgent,
            value => value.ToString().Contains("StreamioX", StringComparison.Ordinal));
        Assert.NotNull(typedClient);
    }

    [Fact]
    public void SourceGeneratedJsonContext_ContainsTransportDtosAndSynopsisAliases()
    {
        var registeredTypes = new[]
        {
            typeof(CatalogResponse), typeof(MetaResponse), typeof(Movie),
            typeof(Video), typeof(StreamResponse), typeof(StreamSource),
            typeof(StreamBehaviorHints), typeof(StreamProxyHeaders)
        };

        foreach (var type in registeredTypes)
            Assert.NotNull(StreamioX.App.Models.StremioJsonContext.Default.GetTypeInfo(type));

        const string json = """{"id":"tt123","type":"movie","overview":"TMDB overview","plot":"Alternate plot","trailer":{"url":"https://example.com/trailer.mp4"}}""";
        var movie = System.Text.Json.JsonSerializer.Deserialize(
            json,
            StreamioX.App.Models.StremioJsonContext.Default.Movie);

        Assert.NotNull(movie);
        Assert.Equal("TMDB overview", movie.Overview);
        Assert.Equal("Alternate plot", movie.Plot);
        Assert.Equal("TMDB overview", movie.Synopsis);
        Assert.Equal("https://example.com/trailer.mp4", movie.TrailerLink);
    }

    [Fact]
    public void Options_AreBoundFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StreamioX:RequestTimeoutSeconds"] = "45",
                ["StreamioX:UserAgent"] = "StreamioX.Tests/1.0"
            })
            .Build();

        using var provider = BuildProvider(configuration);
        var options = provider.GetRequiredService<IOptions<StreamioXOptions>>().Value;

        Assert.Equal(45, options.RequestTimeoutSeconds);
        Assert.Equal("StreamioX.Tests/1.0", options.UserAgent);
        Assert.Equal(TimeSpan.FromSeconds(45), provider.GetRequiredService<HttpClient>().Timeout);
    }

    private sealed class SingletonProbe { }
    private sealed class ScopedProbe { }
    private sealed class TransientProbe { }
}
