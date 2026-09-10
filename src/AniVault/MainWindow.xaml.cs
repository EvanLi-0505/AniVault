using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AniVault.ViewModels;

namespace AniVault;

/// <summary>
/// The application shell window. All page content is hosted inside it via ViewModel-first
/// navigation; there are no other top-level windows except the modal setup / editor dialogs.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += (_, _) => viewModel.NavigateToStart();
    }

    /// <summary>
    /// Fades and lifts the content area each time the navigated page changes. Driven from
    /// code-behind (not a XAML <c>Binding.TargetUpdated</c> template trigger) because
    /// resolving <c>Storyboard.TargetName</c> in that trigger throws during first layout.
    /// </summary>
    private void OnPageChanged(object? sender, DataTransferEventArgs e)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(180));

        PageHost.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0d, 1d, duration));

        if (PageHost.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10d, 0d, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }
}
