// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Simple theme switcher that swaps application ResourceDictionaries at runtime.
/// Can also operate in event-only mode for consumers to apply resources themselves.
/// Exposes a dedicated override dictionary that stays merged and is cleared on theme changes.
/// </summary>
internal sealed partial class ResourceSwapper
{
    private readonly Lock _resourceSwapGate = new();
    private readonly Dictionary<string, Uri> _themeUris = new(StringComparer.OrdinalIgnoreCase);
    private ResourceDictionary? _activeDictionary;
    private string? _currentThemeName;
    private Uri? _currentThemeUri;

    private ResourceDictionary? _overrideDictionary;

    /// <summary>
    /// Raised after a theme has been activated.
    /// </summary>
    public event EventHandler<ResourcesSwappedEventArgs>? ResourcesSwapped;

    /// <summary>
    /// Gets or sets a value indicating whether when true (default) ResourceSwapper updates Application.Current.Resources. When false, it only raises ResourcesSwapped.
    /// </summary>
    public bool ApplyToAppResources { get; set; } = true;

    /// <summary>
    /// Gets name of the currently selected theme (if any).
    /// </summary>
    public string? CurrentThemeName
    {
        get
        {
            lock (_resourceSwapGate)
            {
                return _currentThemeName;
            }
        }
    }

    /// <summary>
    /// Initializes ResourceSwapper by checking Application resources for an already merged theme dictionary.
    /// </summary>
    public void Initialize()
    {
        // Find merged dictionary in Application resources that matches a registered theme by URI
        // This allows ResourceSwapper to pick up an initial theme set in XAML
        var app = Application.Current;
        var resourcesMergedDictionaries = app?.Resources?.MergedDictionaries;
        if (resourcesMergedDictionaries == null)
        {
            return;
        }

        foreach (var dict in resourcesMergedDictionaries)
        {
            var uri = dict.Source;
            if (uri is null)
            {
                continue;
            }

            var name = GetNameForUri(uri);
            if (name is null)
            {
                continue;
            }

            lock (_resourceSwapGate)
            {
                _currentThemeName = name;
                _currentThemeUri = uri;
                _activeDictionary = dict;
            }

            break;
        }
    }

    /// <summary>
    /// Gets uri of the currently selected theme dictionary (if any).
    /// </summary>
    public Uri? CurrentThemeUri
    {
        get
        {
            lock (_resourceSwapGate)
            {
                return _currentThemeUri;
            }
        }
    }

    public static ResourceDictionary GetOverrideDictionary(bool clear = false)
    {
        var app = Application.Current ?? throw new InvalidOperationException("App is null");

        if (app.Resources == null)
        {
            throw new InvalidOperationException("Application.Resources is null");
        }

        // (Re)locate the slot – Hot Reload may rebuild Application.Resources.
        var slot = app.Resources!.MergedDictionaries!
            .OfType<MutableOverridesDictionary>()
            .FirstOrDefault();

        if (slot is null)
        {
            // If the slot vanished (Hot Reload), create it again at the end so it wins precedence.
            slot = new MutableOverridesDictionary();
            app.Resources.MergedDictionaries!.Add(slot);
        }

        // Ensure the slot has exactly one child RD we can swap safely.
        if (slot.MergedDictionaries!.Count == 0)
        {
            slot.MergedDictionaries.Add(new ResourceDictionary());
        }
        else if (slot.MergedDictionaries.Count > 1)
        {
            // Normalize to a single child to keep semantics predictable.
            var keep = slot.MergedDictionaries[^1];
            slot.MergedDictionaries.Clear();
            slot.MergedDictionaries.Add(keep);
        }

        if (clear)
        {
            // Swap the child dictionary instead of Clear() to avoid reentrancy issues.
            var fresh = new ResourceDictionary();
            slot.MergedDictionaries[0] = fresh;
            return fresh;
        }

        return slot.MergedDictionaries[0]!;
    }

    /// <summary>
    /// Registers a theme name mapped to a XAML ResourceDictionary URI (e.g. ms-appx:///Themes/Red.xaml)
    /// </summary>
    public void RegisterTheme(string name, Uri dictionaryUri)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Theme name is required", nameof(name));
        }

        lock (_resourceSwapGate)
        {
            _themeUris[name] = dictionaryUri ?? throw new ArgumentNullException(nameof(dictionaryUri));
        }
    }

    /// <summary>
    /// Registers a theme with a string URI.
    /// </summary>
    public void RegisterTheme(string name, string dictionaryUri)
    {
        ArgumentNullException.ThrowIfNull(dictionaryUri);
        RegisterTheme(name, new Uri(dictionaryUri));
    }

    /// <summary>
    /// Removes a previously registered theme.
    /// </summary>
    public bool UnregisterTheme(string name)
    {
        lock (_resourceSwapGate)
        {
            return _themeUris.Remove(name);
        }
    }

    /// <summary>
    /// Gets the names of all registered themes.
    /// </summary>
    public IEnumerable<string> GetRegisteredThemes()
    {
        lock (_resourceSwapGate)
        {
            // return a copy to avoid external mutation
            return new List<string>(_themeUris.Keys);
        }
    }

    /// <summary>
    /// Activates a theme by name. The dictionary for the given name must be registered first.
    /// </summary>
    public void ActivateTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            throw new ArgumentException("Theme name is required", nameof(theme));
        }

        Uri uri;
        lock (_resourceSwapGate)
        {
            if (!_themeUris.TryGetValue(theme, out uri!))
            {
                throw new KeyNotFoundException($"Theme '{theme}' is not registered.");
            }
        }

        ActivateThemeInternal(theme, uri);
    }

    /// <summary>
    /// Tries to activate a theme by name without throwing.
    /// </summary>
    public bool TryActivateTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return false;
        }

        Uri uri;
        lock (_resourceSwapGate)
        {
            if (!_themeUris.TryGetValue(theme, out uri!))
            {
                return false;
            }
        }

        ActivateThemeInternal(theme, uri);
        return true;
    }

    /// <summary>
    /// Activates a theme by URI to a ResourceDictionary.
    /// </summary>
    public void ActivateTheme(Uri dictionaryUri)
    {
        ArgumentNullException.ThrowIfNull(dictionaryUri);

        ActivateThemeInternal(GetNameForUri(dictionaryUri), dictionaryUri);
    }

    /// <summary>
    /// Clears the currently active theme ResourceDictionary. Also clears the override dictionary.
    /// </summary>
    public void ClearActiveTheme()
    {
        lock (_resourceSwapGate)
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            if (_activeDictionary is not null && ApplyToAppResources)
            {
                _ = app.Resources.MergedDictionaries.Remove(_activeDictionary);
                _activeDictionary = null;
            }

            // Clear overrides but keep the override dictionary merged for future updates
            _overrideDictionary?.Clear();

            _currentThemeName = null;
            _currentThemeUri = null;
        }
    }

    private void ActivateThemeInternal(string? name, Uri dictionaryUri)
    {
        lock (_resourceSwapGate)
        {
            _currentThemeName = name;
            _currentThemeUri = dictionaryUri;
        }

        if (ApplyToAppResources)
        {
            ActivateThemeCore(dictionaryUri);
        }

        OnResourcesSwapped(new(name, dictionaryUri));
    }

    private void ActivateThemeCore(Uri dictionaryUri)
    {
        var app = Application.Current ?? throw new InvalidOperationException("Application.Current is null");

        // Remove previously applied base theme dictionary
        if (_activeDictionary is not null)
        {
            _ = app.Resources.MergedDictionaries.Remove(_activeDictionary);
            _activeDictionary = null;
        }

        // Load and merge the new base theme dictionary
        var newDict = new ResourceDictionary { Source = dictionaryUri };
        app.Resources.MergedDictionaries.Add(newDict);
        _activeDictionary = newDict;

        // Ensure override dictionary exists and is merged last, then clear it to avoid leaking stale overrides
        _overrideDictionary = GetOverrideDictionary(clear: true);
    }

    private string? GetNameForUri(Uri dictionaryUri)
    {
        lock (_resourceSwapGate)
        {
            foreach (var (key, value) in _themeUris)
            {
                if (Uri.Compare(value, dictionaryUri, UriComponents.AbsoluteUri, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return key;
                }
            }

            return null;
        }
    }

    private void OnResourcesSwapped(ResourcesSwappedEventArgs e)
    {
        ResourcesSwapped?.Invoke(this, e);
    }
}
