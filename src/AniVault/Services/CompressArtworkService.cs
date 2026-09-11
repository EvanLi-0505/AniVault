using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Services;

/// <summary>
/// Opens the modal "Compress oversized artwork" window. Lives in the composition root so
/// ViewModels can launch it without referencing a View type.
/// </summary>
public interface ICompressArtworkService
{
    Task RunAsync();
}

public sealed class CompressArtworkService : ICompressArtworkService
{
    private readonly IServiceProvider _services;

    public CompressArtworkService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task RunAsync()
    {
        var viewModel = _services.GetRequiredService<CompressArtworkViewModel>();
        await viewModel.LoadAsync();

        var window = new CompressArtworkWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        viewModel.RequestClose += () => window.Close();
        window.ShowDialog();
    }
}
