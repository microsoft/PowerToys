// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class PowerScriptTemplatesPage
{
    private readonly Dictionary<string, IReadOnlyList<PowerScriptCliArgument>> _cliDrafts = new(StringComparer.Ordinal);
    private string _cliDraftId;

    public ObservableCollection<PowerScriptCliArgument> CliArguments { get; } = new();

    private string CliInput => CliArguments.Where(argument => argument.SourceIndex == 1)
        .Select(argument => argument.InputType).FirstOrDefault() ?? "none";

    private static IEnumerable<string> PossibleCliInputs(PowerScriptOperationTemplate template)
    {
        yield return "none";
        if (template.Input == "text" || template.Parameters.Count > 0)
        {
            yield return "text";
        }

        if (template.Input == "files")
        {
            yield return "files";
        }
    }

    private void UpdateCliArguments()
    {
        var cli = SelectedCliContent;
        if (_cliDraftId == cli?.Id)
        {
            return;
        }

        foreach (var argument in CliArguments)
        {
            argument.PropertyChanged -= CliArgument_PropertyChanged;
            argument.Value.PropertyChanged -= CliValue_PropertyChanged;
        }

        CliArguments.Clear();
        _cliDraftId = cli?.Id;
        if (cli is null)
        {
            return;
        }

        if (cli.Input == "files")
        {
            MinimumFilesNumberBox.Value = 1;
            MaximumFilesNumberBox.Value = cli.Id == "image-resizer" ? 0 : 1;
        }

        if (!_cliDrafts.TryGetValue(cli.Id, out var draft))
        {
            var arguments = cli.Parameters.Select(parameter => new PowerScriptCliArgument(parameter, useInput: false)).ToList();
            if (cli.InputArgument is { } input)
            {
                arguments.Insert(0, new PowerScriptCliArgument(input, useInput: true));
            }

            draft = arguments;
            _cliDrafts.Add(cli.Id, draft);
        }

        foreach (var argument in draft)
        {
            CliArguments.Add(argument);
            argument.PropertyChanged += CliArgument_PropertyChanged;
            argument.Value.PropertyChanged += CliValue_PropertyChanged;
        }
    }

    private void CliValue_PropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateCreateButton();

    private void CliArgument_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_syncingCliSources || e.PropertyName != nameof(PowerScriptCliArgument.SourceIndex) || sender is not PowerScriptCliArgument changed)
        {
            return;
        }

        // One script can only take one kind of input, so picking a new one releases the others.
        if (changed.IsInput)
        {
            _syncingCliSources = true;
            try
            {
                foreach (var other in CliArguments.Where(argument => argument != changed && argument.IsInput && argument.InputType != changed.InputType).ToList())
                {
                    other.SourceIndex = 0;
                }
            }
            finally
            {
                _syncingCliSources = false;
            }
        }

        UpdateOptions();
    }

    private bool ValidateCliArguments(out string error)
    {
        error = null;
        if (!IsCliContent)
        {
            return true;
        }

        if (SelectedCliContent is null)
        {
            return false;
        }

        if (CliArguments.Where(argument => argument.SourceIndex == 1).Select(argument => argument.InputType).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            error = PowerScriptsViewModel.GetResourceString("PowerScripts_CliArgumentMixedInput");
            return false;
        }

        foreach (var argument in CliArguments)
        {
            if (argument.IsFixed && !argument.Value.TryValidateForCreate(out error))
            {
                return false;
            }
        }

        return true;
    }

    private JsonObject CreateCliBindings()
    {
        if (!IsCliContent)
        {
            return null;
        }

        if (!ValidateCliArguments(out var error))
        {
            throw new ArgumentException(error);
        }

        var bindings = new JsonObject();
        foreach (var argument in CliArguments)
        {
            bindings.Add(argument.Name, argument.ToBinding());
        }

        return bindings;
    }

    private async void CliBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating || sender is not Button { Tag: PowerScriptCliArgument argument } button)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.MainWindow.AppWindow.Id);
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file is not null && CliArguments.Contains(argument))
            {
                argument.Value.TextValue = file.Path;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to select a module-call input file.", ex);
            TemplateStatus.Severity = InfoBarSeverity.Error;
            TemplateStatus.Message = ex.Message;
            TemplateStatus.IsOpen = true;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
