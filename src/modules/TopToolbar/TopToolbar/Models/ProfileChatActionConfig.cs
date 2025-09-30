// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TopToolbar.Models;

/// <summary>
/// Configuration for chat-based actions (future feature)
/// </summary>
public class ProfileChatActionConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _prompt = string.Empty;

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (_prompt != value)
            {
                _prompt = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private string _model = string.Empty;

    public string Model
    {
        get => _model;
        set
        {
            if (_model != value)
            {
                _model = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private bool _useContext = true;

    public bool UseContext
    {
        get => _useContext;
        set
        {
            if (_useContext != value)
            {
                _useContext = value;
                OnPropertyChanged();
            }
        }
    }

    public ProfileChatActionConfig Clone()
    {
        return new ProfileChatActionConfig
        {
            Prompt = Prompt,
            Model = Model,
            UseContext = UseContext,
        };
    }
}
