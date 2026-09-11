using System;
using AniVault.Data;
using AniVault.Metadata;
using AniVault.Metadata.Providers;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.Services.Backup;
using AniVault.Services.Export;
using AniVault.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Composition;

/// <summary>
/// The DI composition root, split into one method per subsystem so each stays small and a new
/// subsystem is a one-line addition in <see cref="App.BuildServiceProvider"/> rather than more
/// lines dropped into one long method.
///
/// This is also the template for adding a future pluggable subsystem (see
/// <c>Metadata/</c> + <see cref="AddMetadataServices"/> for the reference shape — provider
/// interface, isolated HTTP client, a service that gates network calls behind an explicit
/// setting, and its own launcher service for the UI). A hypothetical online-watch feature
/// would follow the same pattern as its own top-level folder (e.g. <c>Playback/</c>) with its
/// own <c>network.onlineWatchEnabled</c> setting and its own explicit "▶ Watch online" button —
/// never automatic, per the offline-first rule in CLAUDE.md. See docs/ARCHITECTURE.md →
/// "Adding a pluggable subsystem".
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>Services with no feature attached to them: paths, navigation, dialogs, settings, theme, localization.</summary>
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IBootstrapConfigService, BootstrapConfigService>();
        services.AddSingleton<IAppPathService, AppPathService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMediaEditorService, MediaEditorService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecureSettingsService, SecureSettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
        services.AddSingleton<MainWindow>();

        return services;
    }

    /// <summary>
    /// The connection string is resolved lazily (on first <c>CreateDbContext</c>), so the app
    /// can start and show first-run setup before a data directory has been chosen.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddSingleton<IDbContextFactory<AppDbContext>, RuntimeDbContextFactory>();
        services.AddSingleton<DatabaseInitializer>();

        return services;
    }

    /// <summary>The local-library business logic — everything that works with zero network access.</summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IMediaQueryService, MediaQueryService>();
        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<IArtworkService, ArtworkService>();
        services.AddSingleton<IImageDownloadService, ImageDownloadService>();
        services.AddSingleton<IMediaCardFactory, MediaCardFactory>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ILibraryExportService, LibraryExportService>();

        return services;
    }

    /// <summary>
    /// The only subsystem allowed to touch the network — and only from an explicit user action
    /// while <c>network.onlineSearchEnabled</c> is on (enforced in <see cref="MetadataService"/>,
    /// never here). One shared, capped, identified <see cref="System.Net.Http.HttpClient"/> for
    /// every provider.
    /// </summary>
    public static IServiceCollection AddMetadataServices(this IServiceCollection services, string appVersion)
    {
        services.AddHttpClient(ProviderHttp.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"AniVault/{appVersion} (personal media library)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddSingleton<ICustomProviderStore, CustomProviderStore>();
        services.AddSingleton<IMetadataProvider, BangumiProvider>();
        services.AddSingleton<IMetadataProvider, AniListProvider>();
        services.AddSingleton<IMetadataProvider, JikanProvider>();
        services.AddSingleton<IMetadataProvider, KitsuProvider>();
        services.AddSingleton<IMetadataProvider, TmdbProvider>();
        services.AddSingleton<IMetadataProvider, CustomMetadataProvider>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IMetadataImporter, MetadataImporter>();
        services.AddSingleton<IOnlineSearchService, OnlineSearchService>();
        services.AddSingleton<ICustomProviderService, CustomProviderService>();

        return services;
    }

    /// <summary>
    /// Small, self-contained features that are neither core infrastructure nor part of the
    /// metadata subsystem. Each is a launcher service + its own window/ViewModel pair — the
    /// pattern to copy for the next standalone feature.
    /// </summary>
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IRatingCalculatorService, RatingCalculatorService>();
        services.AddSingleton<IRatingGuideService, RatingGuideService>();

        return services;
    }

    /// <summary>Page/window ViewModels. The shell is a singleton (one nav back-stack); everything else is transient.</summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<MediaDetailViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<SeasonsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BackupExportViewModel>();
        services.AddTransient<MetadataSettingsViewModel>();
        services.AddTransient<CustomProviderViewModel>();
        services.AddTransient<RatingCalculatorViewModel>();
        services.AddTransient<OnlineSearchViewModel>();
        services.AddTransient<PlaceholderViewModel>();
        services.AddTransient<MediaEditorViewModel>();
        services.AddTransient<TagPickerViewModel>();
        services.AddTransient<FilterPanelViewModel>();

        return services;
    }
}
