using System.Windows;
using AniVault.Views;

namespace AniVault.Services;

/// <summary>
/// Opens the read-only "rating rubric" window. Lives in the composition root so ViewModels
/// can show it without referencing a View type. Everything is offline / static.
/// </summary>
public interface IRatingGuideService
{
    void Show();
}

public sealed class RatingGuideService : IRatingGuideService
{
    public void Show()
    {
        Window? owner = null;
        foreach (Window window in Application.Current.Windows)
        {
            if (window.IsActive)
            {
                owner = window;
            }
        }

        var guide = new RatingGuideWindow { Owner = owner ?? Application.Current.MainWindow };
        guide.ShowDialog();
    }
}
