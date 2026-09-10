using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AniVault.ViewModels;

/// <summary>
/// Shown for navigation entries whose feature is scheduled for a later development phase.
/// This is an intentional, honest empty state — not stubbed production logic.
/// </summary>
public sealed partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = LocalizationService.Instance?.Text("Placeholder.ComingSoon") ?? "Coming soon";

    [ObservableProperty]
    private string _message = string.Empty;

    public void Describe(string title, string feature)
    {
        Title = title;
        Message = LocalizationService.Instance?.Format("Placeholder.Format", feature)
            ?? $"\"{feature}\" is planned for a later development phase.";
    }
}
