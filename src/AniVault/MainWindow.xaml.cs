using System.Windows;
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
}
