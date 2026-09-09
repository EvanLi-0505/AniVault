using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.Models;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Services;

/// <summary>
/// Opens the modal "Search online" window. Lives in the composition root so ViewModels
/// can start an online import without referencing any View type.
/// </summary>
public interface IOnlineSearchService
{
    /// <summary>Returns the id of a newly imported media item, or null if the user imported nothing.</summary>
    Task<int?> SearchAndImportAsync(MediaType? preferredMediaType);
}

public sealed class OnlineSearchService : IOnlineSearchService
{
    private readonly IServiceProvider _services;

    public OnlineSearchService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<int?> SearchAndImportAsync(MediaType? preferredMediaType)
    {
        var viewModel = _services.GetRequiredService<OnlineSearchViewModel>();
        await viewModel.InitializeAsync(preferredMediaType);

        var window = new OnlineSearchWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        viewModel.RequestClose += () => window.DialogResult = viewModel.ImportedMediaId is not null;
        window.ShowDialog();

        return viewModel.ImportedMediaId;
    }
}
