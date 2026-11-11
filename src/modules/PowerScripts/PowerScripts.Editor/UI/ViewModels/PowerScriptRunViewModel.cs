// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

/// <summary>Transient inputs and output for the editor. Nothing here is written to Activity.</summary>
public sealed class PowerScriptRunViewModel : Observable
{
    private static readonly char[] FileSeparators = { '\r', '\n' };

    private PowerScriptInfo _script;
    private bool _isLoading;
    private bool _isRunning;
    private string _primaryText = string.Empty;
    private string _primaryFilesText = string.Empty;
    private string _validationError = string.Empty;
    private string _failureMessage = string.Empty;
    private string _output = string.Empty;
    private string _diagnostics = string.Empty;
    private string _status = Resource("PowerScripts_RunReady");
    private string _exitCode = string.Empty;

    public ObservableCollection<PowerScriptRunParameter> Parameters { get; } = new();

    public PowerScriptInfo Script => _script;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value))
            {
                NotifyAvailability();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                NotifyAvailability();
            }
        }
    }

    public bool CanEdit => !IsLoading && !IsRunning;

    public bool CanRun => _script is not null && IsSupported && CanEdit && string.IsNullOrEmpty(ValidationError);

    public bool HasNoParameters => _script is not null && Parameters.Count == 0;

    public bool HasTextInput => _script?.Input == PowerScriptIO.Text;

    public bool HasFileInput => _script?.Input == PowerScriptIO.Files;

    public bool IsSupported => _script is not null &&
        _script.Input is PowerScriptIO.None or PowerScriptIO.Text or PowerScriptIO.Files &&
        _script.Output is PowerScriptIO.None or PowerScriptIO.Text or PowerScriptIO.Files;

    public string UnsupportedReason => _script is not null && !IsSupported ? Resource("PowerScripts_RunUnsupported") : string.Empty;

    public bool HasUnsupportedReason => !string.IsNullOrEmpty(UnsupportedReason);

    public string PrimaryText
    {
        get => _primaryText;
        set
        {
            if (Set(ref _primaryText, value ?? string.Empty))
            {
                InputChanged();
            }
        }
    }

    public string PrimaryFilesText
    {
        get => _primaryFilesText;
        set
        {
            if (Set(ref _primaryFilesText, value ?? string.Empty))
            {
                InputChanged();
            }
        }
    }

    public string ValidationError
    {
        get => _validationError;
        private set
        {
            if (Set(ref _validationError, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    public string FailureMessage
    {
        get => _failureMessage;
        private set
        {
            if (Set(ref _failureMessage, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasFailure));
            }
        }
    }

    public bool HasFailure => !string.IsNullOrEmpty(FailureMessage);

    public string Output
    {
        get => _output;
        private set
        {
            if (Set(ref _output, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasOutput));
            }
        }
    }

    public bool HasOutput => !string.IsNullOrEmpty(Output);

    public string Diagnostics
    {
        get => _diagnostics;
        private set
        {
            if (Set(ref _diagnostics, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasDiagnostics));
            }
        }
    }

    public bool HasDiagnostics => !string.IsNullOrEmpty(Diagnostics);

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string ExitCode
    {
        get => _exitCode;
        private set => Set(ref _exitCode, value);
    }

    public void StartLoading()
    {
        IsLoading = true;
        FailureMessage = string.Empty;
        ClearResult();
        Status = Resource("PowerScripts_RunLoading");
    }

    public void FinishLoading(PowerScriptInfo script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var drafts = Parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in Parameters)
        {
            parameter.PropertyChanged -= Parameter_PropertyChanged;
        }

        _script = script;
        Parameters.Clear();
        foreach (var definition in script.Parameters)
        {
            var parameter = new PowerScriptRunParameter(definition);
            if (drafts.TryGetValue(definition.Name, out var draft) &&
                string.Equals(definition.Type, draft.Definition.Type, StringComparison.OrdinalIgnoreCase))
            {
                parameter.IsProvided = parameter.CanOmit ? draft.IsProvided : true;
                parameter.TextValue = draft.TextValue;
                parameter.BooleanValue = draft.BooleanValue;
            }

            parameter.PropertyChanged += Parameter_PropertyChanged;
            Parameters.Add(parameter);
        }

        IsLoading = false;
        Status = Resource("PowerScripts_RunReady");
        OnPropertyChanged(nameof(Script));
        OnPropertyChanged(nameof(HasNoParameters));
        OnPropertyChanged(nameof(HasTextInput));
        OnPropertyChanged(nameof(HasFileInput));
        OnPropertyChanged(nameof(IsSupported));
        OnPropertyChanged(nameof(UnsupportedReason));
        OnPropertyChanged(nameof(HasUnsupportedReason));
        Validate();
    }

    public void LoadingFailed(string message)
    {
        _script = null;
        IsLoading = false;
        FailureMessage = message;
        Status = Resource("PowerScripts_RunFailed");
        NotifyAvailability();
    }

    public bool TryCreateInput(out PowerScriptTransformInput input, out string error)
    {
        input = null;
        error = string.Empty;
        if (_script is null || !IsSupported)
        {
            error = Resource("PowerScripts_RunUnsupported");
            return false;
        }

        var provided = Parameters.Where(parameter => parameter.IsProvided)
            .ToDictionary(parameter => parameter.Name, parameter => parameter.ProvidedValue, StringComparer.OrdinalIgnoreCase);
        if (!PowerScriptParameterValues.TryCreate(_script, provided, out var values, out var parameterError))
        {
            error = parameterError;
            return false;
        }

        IReadOnlyList<string> files = null;
        if (HasFileInput)
        {
            files = PrimaryFilesText.Split(FileSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (files.Count < _script.InputMinFiles ||
                (_script.InputMaxFiles is int maximum && files.Count > maximum) ||
                files.Any(file => !_script.AcceptsFileExtension(System.IO.Path.GetExtension(file))))
            {
                error = Resource("PowerScripts_RunInvalidFiles");
                return false;
            }
        }

        input = new PowerScriptTransformInput
        {
            Text = HasTextInput ? PrimaryText : null,
            FilePaths = files,
            Parameters = values.Count == 0 ? null : values,
        };
        return true;
    }

    public void BeginRun()
    {
        FailureMessage = string.Empty;
        ClearResult();
        Status = Resource("PowerScripts_RunRunning");
        IsRunning = true;
    }

    public void RequestCancellation() => Status = Resource("PowerScripts_RunCancelRequested");

    public void Complete(PowerScriptTransformResult result)
    {
        Output = result.Text ?? (result.FilePaths is { } files ? string.Join(Environment.NewLine, files) : string.Empty);
        Diagnostics = result.StdErr;
        ExitCode = Resource("PowerScripts_RunExitCode").Replace("{0}", result.ExitCode.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);
        Status = Resource(result.Succeeded ? "PowerScripts_RunSucceeded" : "PowerScripts_RunFailed");
        IsRunning = false;
    }

    public void Fail(string message)
    {
        FailureMessage = message;
        Status = Resource("PowerScripts_RunFailed");
        IsLoading = false;
        IsRunning = false;
    }

    public void Cancelled(bool timedOut)
    {
        Status = Resource(timedOut ? "PowerScripts_RunTimedOut" : "PowerScripts_RunCancelled");
        FailureMessage = Resource("PowerScripts_RunExecutionUnknown");
        IsLoading = false;
        IsRunning = false;
    }

    private static string Resource(string name) => PowerScriptsViewModel.GetResourceString(name);

    private void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e) => InputChanged();

    private void InputChanged()
    {
        if (CanEdit)
        {
            ClearResult();
            FailureMessage = string.Empty;
            Status = Resource("PowerScripts_RunReady");
        }

        Validate();
    }

    private void ClearResult()
    {
        Output = string.Empty;
        Diagnostics = string.Empty;
        ExitCode = string.Empty;
    }

    private void Validate()
    {
        ValidationError = _script is not null && IsSupported && !TryCreateInput(out _, out var error) ? error : string.Empty;
        OnPropertyChanged(nameof(CanRun));
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanRun));
    }
}
