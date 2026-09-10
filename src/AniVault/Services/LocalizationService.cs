using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Services;

public enum AppLanguage
{
    English,
    Chinese,
}

/// <summary>
/// Runtime UI-string localization. XAML uses the <c>{loc:Loc Key}</c> markup extension (which
/// binds to the <see cref="this[string]"/> indexer); C# code uses <see cref="Text"/> /
/// <see cref="Format"/>. Switching language raises <see cref="LanguageChanged"/> and refreshes
/// all indexer bindings, and the shell re-navigates so every page rebuilds its strings.
///
/// Strings live in embedded <c>Resources/Strings/{en,zh}.json</c> (flat "Key": "value" maps).
/// A missing key falls back to English, then to the key itself — so partial translations are safe.
/// </summary>
public interface ILocalizationService
{
    AppLanguage Current { get; }

    string Text(string key);

    string Format(string key, params object?[] args);

    Task InitializeAsync();

    Task SetLanguageAsync(AppLanguage language);
}

public sealed class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

    private readonly ISettingsService _settings;
    private Dictionary<string, string> _strings = new();
    private Dictionary<string, string> _fallback = new();

    public LocalizationService(ISettingsService settings)
    {
        _settings = settings;
        _fallback = Load(AppLanguage.English);
        _strings = _fallback;
        Current = AppLanguage.English;
        Instance = this;
    }

    /// <summary>Set on construction so the XAML markup extension can reach the live instance.</summary>
    public static LocalizationService? Instance { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changes so ViewModels can rebuild cached strings.</summary>
    public event Action? LanguageChanged;

    public AppLanguage Current { get; private set; }

    /// <summary>Indexer used by the <c>{loc:Loc}</c> binding.</summary>
    public string this[string key] => Text(key);

    public string Text(string key)
    {
        if (_strings.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value))
        {
            return value;
        }

        return key;
    }

    public string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, Text(key), args);
        }
        catch (FormatException)
        {
            return Text(key);
        }
    }

    public async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync(SettingKeys.Language, AppLanguage.English.ToString());
        Apply(Enum.TryParse<AppLanguage>(saved, ignoreCase: true, out var lang) ? lang : AppLanguage.English);
    }

    public async Task SetLanguageAsync(AppLanguage language)
    {
        if (language == Current)
        {
            return;
        }

        Apply(language);
        await _settings.SetAsync(SettingKeys.Language, language.ToString());
    }

    private void Apply(AppLanguage language)
    {
        Current = language;
        _strings = language == AppLanguage.English ? _fallback : Load(language);

        var culture = language == AppLanguage.Chinese ? new CultureInfo("zh-Hans") : CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    private static Dictionary<string, string> Load(AppLanguage language)
    {
        var file = language == AppLanguage.Chinese ? "zh.json" : "en.json";
        var resourceName = $"AniVault.Resources.Strings.{file}";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new Dictionary<string, string>();
        }

        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd(), JsonOptions)
               ?? new Dictionary<string, string>();
    }
}
