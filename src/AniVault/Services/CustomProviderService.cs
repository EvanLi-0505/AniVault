using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Services;

/// <summary>
/// Opens the modal "Edit custom provider" window. Lives in the composition root so the
/// Settings ViewModel can launch it without referencing any View type.
/// </summary>
public interface ICustomProviderService
{
    /// <summary>Returns true if the user saved (or cleared) the custom-provider configuration.</summary>
    Task<bool> EditAsync();
}

public sealed class CustomProviderService : ICustomProviderService
{
    private readonly IServiceProvider _services;

    public CustomProviderService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> EditAsync()
    {
        var viewModel = _services.GetRequiredService<CustomProviderViewModel>();
        await viewModel.LoadAsync();

        var window = new CustomProviderWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        var saved = false;
        viewModel.RequestClose += ok =>
        {
            saved = ok;
            window.DialogResult = ok;
        };

        window.ShowDialog();
        return saved;
    }
}
