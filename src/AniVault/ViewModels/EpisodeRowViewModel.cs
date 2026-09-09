using System;
using System.Threading.Tasks;
using AniVault.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AniVault.ViewModels;

/// <summary>One episode checkbox in the detail page. Toggling persists immediately.</summary>
public sealed partial class EpisodeRowViewModel : ObservableObject
{
    private readonly Func<int, bool, Task> _persistWatched;
    private bool _suppressPersist;

    [ObservableProperty]
    private bool _isWatched;

    public EpisodeRowViewModel(Episode episode, Func<int, bool, Task> persistWatched)
    {
        _persistWatched = persistWatched;
        Id = episode.Id;
        Number = episode.EpisodeNumber;
        Title = episode.Title;

        _suppressPersist = true;
        IsWatched = episode.IsWatched;
        _suppressPersist = false;
    }

    public int Id { get; }

    public int Number { get; }

    public string? Title { get; }

    public string Label => $"EP {Number:00}";

    partial void OnIsWatchedChanged(bool value)
    {
        if (!_suppressPersist)
        {
            _ = _persistWatched(Id, value);
        }
    }
}
