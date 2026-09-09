using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AniVault.ViewModels;

/// <summary>
/// Base class for all ViewModels. Adds an optional <see cref="LoadAsync"/> hook that the
/// navigation service calls after a page becomes active, so pages can pull data from the
/// local database off the UI thread.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Called once when the page is navigated to. Override to load local data.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;
}
