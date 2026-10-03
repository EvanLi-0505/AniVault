using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AniVault;

/// <summary>
/// Makes the taskbar button show AniVault's own icon no matter how the exe was launched.
///
/// Without an explicit AppUserModelID the Windows 11 taskbar ignores the window's icon and
/// looks the exe path up in the shell icon cache instead. That lookup is right when the app
/// is started from the installer's shortcut, but for a freshly unzipped portable exe the
/// cache can hold a generic icon (typically cached while the file was still being
/// extracted). With an explicit id the taskbar uses the icon the window itself supplies —
/// and that one is loaded from a resource inside the assembly, not from the file system.
/// </summary>
public partial class App
{
    /// <summary>Must match <c>AppUserModelID</c> on the shortcuts in build/installer/AniVault.iss.</summary>
    private const string AppUserModelId = "AniVault.AniVault";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    /// <summary>Call before the first window is created.</summary>
    private static void SetupTaskbarIdentity()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception)
        {
            // Cosmetic only: without the id the taskbar falls back to the exe's cached icon.
        }

        ImageSource icon;
        try
        {
            icon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/Icons/AniVault.ico"));
        }
        catch (Exception)
        {
            return; // WPF then uses the icon embedded in the exe, as before.
        }

        // Every window (main, first-run, editor, dialogs…) gets the icon as soon as it loads.
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window { Icon: null } window)
                {
                    window.Icon = icon;
                }
            }));
    }
}
