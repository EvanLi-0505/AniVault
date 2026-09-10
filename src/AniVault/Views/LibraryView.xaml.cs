using System.ComponentModel;
using System.Windows.Controls;
using AniVault.Utilities;
using AniVault.ViewModels;

namespace AniVault.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LibraryViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is LibraryViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            RenderRatingGuide(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.RatingGuideText) or nameof(LibraryViewModel.RatingGuideVisible)
            && sender is LibraryViewModel vm)
        {
            RenderRatingGuide(vm);
        }
    }

    private void RenderRatingGuide(LibraryViewModel vm)
    {
        RatingGuideViewer.Document = vm.RatingGuideVisible && !string.IsNullOrEmpty(vm.RatingGuideText)
            ? MarkdownFlow.Build(vm.RatingGuideText)
            : null;
    }
}
