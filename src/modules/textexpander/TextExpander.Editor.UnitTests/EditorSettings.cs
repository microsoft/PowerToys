// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander.Storage;
using Moq;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

/// <summary>The editor's settings for view model tests, kept in memory.</summary>
internal static class EditorSettings
{
    public static TextExpanderSettings For(string snippetsPath)
    {
        var settings = new TextExpanderSettings();
        settings.Properties.SnippetsPath.Value = snippetsPath;
        return settings;
    }

    /// <summary>
    /// A store that opens with <paramref name="settings"/>, applies option updates to them, and
    /// afterwards reads back <paramref name="latest"/>, which by default is the same settings.
    /// </summary>
    public static Mock<TextExpanderSettingsUtils> Store(TextExpanderSettings settings, Func<TextExpanderSettings> latest = null)
    {
        var store = new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        store.Setup(s => s.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
        store.Setup(s => s.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(latest ?? (() => settings));
        store.Setup(s => s.UpdateOptions(It.IsAny<Action<TextExpanderProperties>>())).Returns((Action<TextExpanderProperties> update) =>
        {
            update(settings.Properties);
            return settings;
        });
        return store;
    }
}
