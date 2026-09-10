using System.Windows;
using AniVault.Utilities;

namespace AniVault.Views;

/// <summary>A read-only window showing the full 10-point rating rubric.</summary>
public partial class RatingGuideWindow : Window
{
    public RatingGuideWindow()
    {
        InitializeComponent();
        GuideViewer.Document = MarkdownFlow.Build(RatingGuide.Text);
    }
}
