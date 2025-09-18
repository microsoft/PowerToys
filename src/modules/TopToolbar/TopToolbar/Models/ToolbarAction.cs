// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TopToolbar.Models;

public class ToolbarAction : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private ToolbarActionType _type = ToolbarActionType.CommandLine;

    public ToolbarActionType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
            }
        }
    }

    private string _command;

    public string Command
    {
        get => _command;
        set
        {
            if (_command != value)
            {
                _command = value;
                OnPropertyChanged();
            }
        }
    }

    private string _arguments;

    public string Arguments
    {
        get => _arguments;
        set
        {
            if (_arguments != value)
            {
                _arguments = value;
                OnPropertyChanged();
            }
        }
    }

    private string _workingDirectory;

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (_workingDirectory != value)
            {
                _workingDirectory = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _runAsAdmin;

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set
        {
            if (_runAsAdmin != value)
            {
                _runAsAdmin = value;
                OnPropertyChanged();
            }
        }
    }

    private string _providerId = string.Empty;

    public string ProviderId
    {
        get => _providerId;
        set
        {
            if (_providerId != value)
            {
                _providerId = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private string _providerActionId = string.Empty;

    public string ProviderActionId
    {
        get => _providerActionId;
        set
        {
            if (_providerActionId != value)
            {
                _providerActionId = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private string _providerArgumentsJson;

    public string ProviderArgumentsJson
    {
        get => _providerArgumentsJson;
        set
        {
            if (_providerArgumentsJson != value)
            {
                _providerArgumentsJson = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public bool IsProviderAction => Type == ToolbarActionType.Provider;

    public ToolbarAction Clone()
    {
        return new ToolbarAction
        {
            Type = Type,
            Command = Command,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            RunAsAdmin = RunAsAdmin,
            ProviderId = ProviderId,
            ProviderActionId = ProviderActionId,
            ProviderArgumentsJson = ProviderArgumentsJson,
        };
    }
}
