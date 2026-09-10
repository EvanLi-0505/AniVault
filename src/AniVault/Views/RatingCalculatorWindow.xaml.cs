using System.Windows;
using AniVault.Utilities;
using AniVault.ViewModels;

namespace AniVault.Views;

/// <summary>Modal rating questionnaire. DataContext is a RatingCalculatorViewModel.</summary>
public partial class RatingCalculatorWindow : Window
{
    public RatingCalculatorWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is RatingCalculatorViewModel vm)
            {
                GuideViewer.Document = MarkdownFlow.Build(vm.GuideText);
            }
        };
    }
}
