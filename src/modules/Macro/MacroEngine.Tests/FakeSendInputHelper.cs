// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

internal sealed class FakeSendInputHelper : ISendInputHelper
{
    public List<string> KeyCombos { get; } = [];

    public List<string> Texts { get; } = [];

    public void PressKeyCombo(string combo) => KeyCombos.Add(combo);

    public void TypeText(string text) => Texts.Add(text);
}
