// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using EnvironmentVariablesUILib.Converters;
using EnvironmentVariablesUILib.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EnvironmentVariablesUILib.UnitTests.Converters;

[TestClass]
public class EnvironmentStateConverterTests
{
    [TestMethod]
    [DataRow(EnvironmentState.Unchanged, null)]
    [DataRow(EnvironmentState.ChangedOnStartup, "StateNotUpToDateOnStartupMsg")]
    [DataRow(EnvironmentState.EnvironmentMessageReceived, "StateNotUpToDateEnvironmentMessageReceivedMsg")]
    [DataRow(EnvironmentState.ProfileNotApplicable, "StateProfileNotApplicableMsg")]
    [DataRow(EnvironmentState.ProfileNameInvalid, "StateProfileNameInvalidMsg")]
    public void MessageMapping_ResolvesOnlyExpectedKeyOrReturnsEmpty(EnvironmentState state, string key)
    {
        var requested = new List<string>();
        var result = EnvironmentStateToMessageConverter.ConvertState(state, resourceKey =>
        {
            requested.Add(resourceKey);
            return "sentinel:" + resourceKey;
        });

        Assert.AreEqual(key == null ? string.Empty : "sentinel:" + key, result);
        CollectionAssert.AreEqual(key == null ? Array.Empty<string>() : new[] { key }, requested);
    }

    [TestMethod]
    [DataRow(EnvironmentState.Unchanged, "StateNotUpToDateTitle")]
    [DataRow(EnvironmentState.ChangedOnStartup, "StateNotUpToDateTitle")]
    [DataRow(EnvironmentState.EnvironmentMessageReceived, "StateNotUpToDateTitle")]
    [DataRow(EnvironmentState.ProfileNotApplicable, "ProfileNotApplicableTitle")]
    [DataRow(EnvironmentState.ProfileNameInvalid, "ProfileNameInvalidTitle")]
    [DataRow((EnvironmentState)999, "StateNotUpToDateTitle")]
    public void TitleMapping_ResolvesExpectedKeyIncludingUnknownFallback(EnvironmentState state, string key)
    {
        var requested = new List<string>();
        var result = EnvironmentStateToTitleConverter.ConvertState(state, resourceKey =>
        {
            requested.Add(resourceKey);
            return "sentinel:" + resourceKey;
        });

        Assert.AreEqual("sentinel:" + key, result);
        CollectionAssert.AreEqual(new[] { key }, requested);
    }

    [TestMethod]
    public void MessageMapping_UnknownStateThrowsWithoutResolvingAResource()
    {
        var requested = new List<string>();
        Assert.ThrowsExactly<NotImplementedException>(() =>
            EnvironmentStateToMessageConverter.ConvertState((EnvironmentState)999, key =>
            {
                requested.Add(key);
                return "unexpected";
            }));
        Assert.AreEqual(0, requested.Count);
    }

    [TestMethod]
    [DataRow(EnvironmentState.Unchanged, false, Visibility.Collapsed)]
    [DataRow(EnvironmentState.ChangedOnStartup, true, Visibility.Visible)]
    [DataRow(EnvironmentState.EnvironmentMessageReceived, true, Visibility.Visible)]
    [DataRow(EnvironmentState.ProfileNotApplicable, true, Visibility.Visible)]
    [DataRow(EnvironmentState.ProfileNameInvalid, true, Visibility.Visible)]
    [DataRow((EnvironmentState)999, true, Visibility.Visible)]
    public void WarningState_OnlyUnchangedIsFalseAndCollapsed(EnvironmentState state, bool expectedBool, Visibility expectedVisibility)
    {
        Assert.AreEqual(expectedBool, new EnvironmentStateToBoolConverter().Convert(state, typeof(bool), null, string.Empty));
        Assert.AreEqual(expectedVisibility, new EnvironmentStateToVisibilityConverter().Convert(state, typeof(Visibility), null, string.Empty));
    }

    [TestMethod]
    [DataRow(VariablesSetType.User, "\uE77B")]
    [DataRow(VariablesSetType.System, "\uE977")]
    [DataRow(VariablesSetType.Profile, "\uEDE3")]
    [DataRow(VariablesSetType.Path, "\uE8AC")]
    [DataRow(VariablesSetType.Duplicate, "\uE8C8")]
    public void VariableScope_UsesExistingGlyphMapping(VariablesSetType scope, string expected)
    {
        Assert.AreEqual(expected, new VariableTypeToGlyphConverter().Convert(scope, typeof(string), null, string.Empty));
    }

    [TestMethod]
    public void VariableScope_UnknownValueThrows()
    {
        Assert.ThrowsExactly<NotImplementedException>(() =>
            new VariableTypeToGlyphConverter().Convert((VariablesSetType)999, typeof(string), null, string.Empty));
    }

    [TestMethod]
    [DataRow("bool")]
    [DataRow("visibility")]
    [DataRow("message")]
    [DataRow("title")]
    [DataRow("glyph")]
    public void ReverseConversion_IsUnsupportedWithoutResourceResolution(string converterName)
    {
        IValueConverter converter = converterName switch
        {
            "bool" => new EnvironmentStateToBoolConverter(),
            "visibility" => new EnvironmentStateToVisibilityConverter(),
            "message" => new EnvironmentStateToMessageConverter(),
            "title" => new EnvironmentStateToTitleConverter(),
            "glyph" => new VariableTypeToGlyphConverter(),
            _ => throw new ArgumentException("Unknown converter.", nameof(converterName)),
        };

        Assert.ThrowsExactly<NotImplementedException>(() => converter.ConvertBack(null, typeof(EnvironmentState), null, string.Empty));
    }
}
