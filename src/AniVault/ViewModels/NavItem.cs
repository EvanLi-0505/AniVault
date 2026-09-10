using System;
using AniVault.Services;

namespace AniVault.ViewModels;

/// <summary>
/// One entry in the left navigation sidebar. <see cref="IsHeader"/> rows are non-clickable
/// section captions; the rest carry a <see cref="Navigate"/> action.
/// </summary>
public sealed class NavItem
{
    private NavItem(string key, string label, string? icon, bool isHeader, Action<INavigationService>? navigate)
    {
        Key = key;
        Label = label;
        Icon = icon;
        IsHeader = isHeader;
        Navigate = navigate;
    }

    /// <summary>Stable identifier, language-independent (used to re-select after a language switch).</summary>
    public string Key { get; }

    public string Label { get; }

    public string? Icon { get; }

    public bool IsHeader { get; }

    public Action<INavigationService>? Navigate { get; }

    public bool IsSelectable => !IsHeader;

    public static NavItem Header(string label) => new(string.Empty, label, null, isHeader: true, navigate: null);

    public static NavItem Page(string key, string label, string icon, Action<INavigationService> navigate)
        => new(key, label, icon, isHeader: false, navigate);
}
