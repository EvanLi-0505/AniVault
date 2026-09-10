using System;
using System.Windows.Data;
using System.Windows.Markup;
using AniVault.Services;

namespace AniVault.Utilities;

/// <summary>
/// XAML markup extension for localized strings: <c>Text="{loc:Loc Nav.Home}"</c>.
/// Binds to <see cref="LocalizationService"/>'s indexer so the text updates live when the
/// language changes. Falls back to the key if the string is missing or the service isn't ready.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = Key,
            TargetNullValue = Key,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
