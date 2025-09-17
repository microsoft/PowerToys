// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopToolbar.Models;

public class ToolbarButton : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = System.Guid.NewGuid().ToString();

    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    private string _description;

    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    private ToolbarIconType _iconType = ToolbarIconType.Glyph;

    public ToolbarIconType IconType
    {
        get => _iconType;
        set
        {
            if (_iconType != value)
            {
                _iconType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IconTypeIndex));
            }
        }
    }

    public int IconTypeIndex
    {
        get => (int)IconType;
        set
        {
            if ((int)_iconType != value)
            {
                IconType = (ToolbarIconType)value;
                OnPropertyChanged();
            }
        }
    }

    // For glyph icons (Segoe MDL2 Assets)
    private string _iconGlyph = "\uE10F";

    public string IconGlyph
    {
        get => _iconGlyph;
        set
        {
            var normalized = NormalizeGlyph(value);
            if (_iconGlyph != normalized)
            {
                _iconGlyph = normalized;
                OnPropertyChanged();
            }
        }
    }

    private static string NormalizeGlyph(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var s = input.Trim();

        try
        {
            // Accept patterns: \uE713, U+E713, 0xE713, E713, &#xE713; or the literal glyph
            if (s.StartsWith("\\u", System.StringComparison.OrdinalIgnoreCase) && s.Length >= 6)
            {
                s = s.Substring(2);
            }
            else if (s.StartsWith("U+", System.StringComparison.OrdinalIgnoreCase) && s.Length >= 5)
            {
                s = s.Substring(2);
            }
            else if (s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) && s.Length >= 4)
            {
                s = s.Substring(2);
            }
            else if (s.StartsWith("&#x", System.StringComparison.OrdinalIgnoreCase) && s.EndsWith(';'))
            {
                s = s.Substring(3, s.Length - 4);
            }

            // If now looks like hex, parse to a single char string
            bool isHex = true;
            foreach (var c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    isHex = false;
                    break;
                }
            }

            if (isHex && s.Length >= 4 && s.Length <= 6)
            {
                var codePoint = Convert.ToInt32(s, 16);
                return char.ConvertFromUtf32(codePoint);
            }
        }
        catch
        {
            // Fall back to original input on any parse error
        }

        return input;
    }

    // For custom images (png/jpg/ico) absolute path
    private string _iconPath;

    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath != value)
            {
                _iconPath = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    private ToolbarAction _action = new();

    public ToolbarAction Action
    {
        get => _action;
        set
        {
            if (!Equals(_action, value))
            {
                _action = value;
                OnPropertyChanged();
            }
        }
    }
}
