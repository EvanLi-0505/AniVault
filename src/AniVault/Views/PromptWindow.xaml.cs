using System.Windows;

namespace AniVault.Views;

/// <summary>A minimal single-line text-input dialog (WPF has no built-in InputBox).</summary>
public partial class PromptWindow : Window
{
    public PromptWindow(string title, string message, string? initialValue)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initialValue ?? string.Empty;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    public string? Value { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Value = InputBox.Text;
        DialogResult = true;
    }
}
