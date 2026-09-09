using System;
using AniVault.Services;

namespace AniVault.ViewModels;

/// <summary>
/// One entry in the left navigation sidebar. <see cref="IsHeader"/> rows are non-clickable
/// section captions; the rest carry a <see cref="Navigate"/> action.
/// </summary>
public sealed class NavItem
{
    private NavItem(string label, string? icon, bool isHeader, Action<INavigationService>? navigate)
    {
        Label = label;
        Icon = icon;
        IsHeader = isHeader;
        Navigate = navigate;
    }

    public string Label { get; }

    public string? Icon { get; }

    public bool IsHeader { get; }

    public Action<INavigationService>? Navigate { get; }

    public bool IsSelectable => !IsHeader;

    public static NavItem Header(string label) => new(label, null, isHeader: true, navigate: null);

    public static NavItem Page(string label, string icon, Action<INavigationService> navigate)
        => new(label, icon, isHeader: false, navigate);
}
