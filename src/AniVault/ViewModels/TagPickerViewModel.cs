using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AniVault.ViewModels;

/// <summary>
/// Reusable tag editor: a set of chosen tag names plus an input box with suggestions from
/// the tags that already exist. Used by the media editor (and available for the detail page).
/// </summary>
public sealed partial class TagPickerViewModel : ObservableObject
{
    private readonly ITagService _tagService;

    [ObservableProperty]
    private string _newTagText = string.Empty;

    public TagPickerViewModel(ITagService tagService)
    {
        _tagService = tagService;
        SelectedTags.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised whenever the chosen tag set changes.</summary>
    public event EventHandler? Changed;

    public ObservableCollection<string> SelectedTags { get; } = new();

    /// <summary>All existing tag names, for the suggestion list.</summary>
    public ObservableCollection<string> Suggestions { get; } = new();

    public async Task LoadAsync(IEnumerable<string>? initialTags = null)
    {
        SelectedTags.Clear();
        foreach (var tag in (initialTags ?? Enumerable.Empty<string>())
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .Select(t => t.Trim()))
        {
            if (!Contains(tag))
            {
                SelectedTags.Add(tag);
            }
        }

        Suggestions.Clear();
        foreach (var usage in await _tagService.GetAllWithUsageAsync())
        {
            Suggestions.Add(usage.Name);
        }
    }

    public IReadOnlyList<string> GetTags() => SelectedTags.ToList();

    [RelayCommand]
    private void AddTag(string? name)
    {
        var value = (name ?? NewTagText).Trim();
        if (value.Length == 0 || Contains(value))
        {
            NewTagText = string.Empty;
            return;
        }

        SelectedTags.Add(value);
        NewTagText = string.Empty;
    }

    [RelayCommand]
    private void RemoveTag(string? name)
    {
        if (name is null)
        {
            return;
        }

        var match = SelectedTags.FirstOrDefault(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            SelectedTags.Remove(match);
        }
    }

    private bool Contains(string tag)
        => SelectedTags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
}
