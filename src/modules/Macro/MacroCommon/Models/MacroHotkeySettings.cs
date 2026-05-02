// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

/// <summary>
/// Structured hotkey stored in <see cref="MacroDefinition"/>.
/// Mirrors HotkeySettings (Settings.UI.Library) but lives in MacroCommon
/// so MacroEngine can read it without any UI dependency.
/// </summary>
public sealed record MacroHotkeySettings(bool Win, bool Ctrl, bool Alt, bool Shift, int Code);
