using System.Windows.Controls;
using System.Windows.Input;

namespace AniVault.Views;

public partial class TagsView : UserControl
{
    public TagsView()
    {
        InitializeComponent();
    }

    /// <summary>Clicking anywhere in the icon/placeholder area focuses the actual text box.</summary>
    private void OnNewTagBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        NewTagBox.Focus();
    }
}
