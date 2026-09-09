using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AniVault.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AniVault.Services;

/// <summary>
/// Minimal ViewModel-first navigation for the single-window shell.
/// The shell binds a <c>ContentControl</c> to <see cref="CurrentViewModel"/>; DataTemplates
/// in XAML map each ViewModel type to its View. A single-level-plus back stack supports
/// drilling into detail pages and returning.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    bool CanGoBack { get; }

    event Action? CurrentViewModelChanged;

    void NavigateTo<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase;

    void NavigateTo(Type viewModelType);

    /// <summary>Navigates to a page and remembers the current one so <see cref="GoBack"/> can return.</summary>
    void NavigateToDetail<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase;

    void GoBack();
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NavigationService> _logger;
    private readonly Stack<ViewModelBase> _backStack = new();

    public NavigationService(IServiceProvider services, ILogger<NavigationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public ViewModelBase? CurrentViewModel { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase
    {
        _backStack.Clear();
        SetCurrent(Resolve(configure));
    }

    public void NavigateTo(Type viewModelType)
    {
        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
        {
            throw new ArgumentException($"{viewModelType} is not a ViewModelBase.", nameof(viewModelType));
        }

        _backStack.Clear();
        SetCurrent((ViewModelBase)_services.GetRequiredService(viewModelType));
    }

    public void NavigateToDetail<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase
    {
        if (CurrentViewModel is not null)
        {
            _backStack.Push(CurrentViewModel);
        }

        SetCurrent(Resolve(configure));
    }

    public void GoBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        SetCurrent(_backStack.Pop(), reload: true);
    }

    private TViewModel Resolve<TViewModel>(Action<TViewModel>? configure)
        where TViewModel : ViewModelBase
    {
        var viewModel = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(viewModel);
        return viewModel;
    }

    private void SetCurrent(ViewModelBase viewModel, bool reload = true)
    {
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();

        if (reload)
        {
            _ = LoadPageAsync(viewModel);
        }
    }

    private async Task LoadPageAsync(ViewModelBase viewModel)
    {
        try
        {
            await viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load page {Page}.", viewModel.GetType().Name);
        }
    }
}
