using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.Models;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Services;

/// <summary>
/// Creates and shows the modal <see cref="MediaEditorWindow"/>. Lives in the composition
/// root so ViewModels can request the editor without referencing any View type.
/// </summary>
public sealed class MediaEditorService : IMediaEditorService
{
    private readonly IServiceProvider _services;

    public MediaEditorService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> AddNewAsync(MediaType mediaType)
    {
        var viewModel = _services.GetRequiredService<MediaEditorViewModel>();
        await viewModel.InitializeForNewAsync(mediaType);
        return await ShowAsync(viewModel);
    }

    public async Task<bool> EditAsync(int mediaId)
    {
        var viewModel = _services.GetRequiredService<MediaEditorViewModel>();
        if (!await viewModel.InitializeForEditAsync(mediaId))
        {
            return false;
        }

        return await ShowAsync(viewModel);
    }

    private Task<bool> ShowAsync(MediaEditorViewModel viewModel)
    {
        var window = new MediaEditorWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        var result = false;
        viewModel.RequestClose += saved =>
        {
            result = saved;
            window.DialogResult = saved;
        };

        window.ShowDialog();
        return Task.FromResult(result);
    }
}
