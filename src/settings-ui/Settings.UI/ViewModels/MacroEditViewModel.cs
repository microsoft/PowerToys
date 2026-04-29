// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

// Stub — full implementation in Task 6.
public sealed class MacroEditViewModel
{
    public MacroEditViewModel()
    {
    }

    public MacroEditViewModel(MacroDefinition definition)
    {
        OriginalId = definition.Id;
        _name = definition.Name;
    }

    private readonly string _name = string.Empty;

    public string OriginalId { get; } = Guid.NewGuid().ToString();

    public MacroDefinition ToDefinition() => new() { Id = OriginalId, Name = _name };
}
