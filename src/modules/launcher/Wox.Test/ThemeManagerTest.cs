// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.Helper;

namespace Wox.Test;

[TestClass]
public class ThemeManagerTest
{
    [DataTestMethod]
    [DataRow(Theme.Light, ThemeMode.Light)]
    [DataRow(Theme.Dark, ThemeMode.Dark)]
    [DataRow(Theme.HighContrastBlack, ThemeMode.None)]
    [DataRow(Theme.HighContrastWhite, ThemeMode.None)]
    [DataRow(Theme.HighContrastOne, ThemeMode.None)]
    [DataRow(Theme.HighContrastTwo, ThemeMode.None)]
    public void GetThemeMode_ReturnsExpectedThemeMode(Theme theme, ThemeMode expectedThemeMode)
    {
        Assert.AreEqual(expectedThemeMode, ThemeManager.GetThemeMode(theme));
    }
}
