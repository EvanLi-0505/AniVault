using CommunityToolkit.Mvvm.ComponentModel;

namespace AniVault.ViewModels;

/// <summary>
/// Shown for navigation entries whose feature is scheduled for a later development phase.
/// This is an intentional, honest empty state — not stubbed production logic.
/// </summary>
public sealed partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Coming soon";

    [ObservableProperty]
    private string _message = "This section will be implemented in a later phase.";

    public void Describe(string title, string feature)
    {
        Title = title;
        Message = $"\"{feature}\" is planned for a later development phase. "
            + "The offline library foundation (Home, Anime, Movies, TV Series, Settings) is available now.";
    }
}
