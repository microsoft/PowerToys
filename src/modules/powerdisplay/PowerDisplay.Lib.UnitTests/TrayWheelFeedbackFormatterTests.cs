// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackFormatterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly int[] PrimarySingleValues = [55];
    private static readonly int[] PrimaryMirrorsValues = [70, 35];
    private static readonly int[] AllDisplayValues = [10, 30, 50, 70];
    private static readonly int[] PrimaryRangeValues = [90, 20, 50, 60, 30];
    private static readonly int[] AllRangeValues = [35, 70, 40, 90, 55, 60];
    private static readonly int[] BoundaryValues = [0, 100];
    private static readonly int[] DisabledValues = [55];
    private static readonly int[] BrokenTemplateValues = [55];
    private static readonly int[] BrokenPercentageValues = [25, 75];
    private static readonly int[] LongLengthValues = [55];

    private static TrayWheelFeedbackTemplates Templates(
        string? primary = "Primary display · {0}",
        string? primaryPlural = "Primary displays · {0}",
        string? all = "All displays · {0}",
        string? percentage = "{0}%",
        string? range = "{0}–{1} ({2} displays)",
        string? separator = ", ")
        => new(primary, primaryPlural, all, percentage, range, separator);

    [TestMethod]
    public void Format_PrimarySingle_UsesSingularLabel()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            PrimarySingleValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary display · 55%", result);
    }

    [TestMethod]
    public void Format_PrimaryMirrors_PreservesValueOrderAndUsesPluralLabel()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            PrimaryMirrorsValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary displays · 70%, 35%", result);
    }

    [TestMethod]
    public void Format_AllDisplays_ListsUpToFourValues()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            AllDisplayValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays · 10%, 30%, 50%, 70%", result);
    }

    [TestMethod]
    public void Format_PrimaryMoreThanFour_UsesRangeAndCount()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            PrimaryRangeValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary displays · 20%–90% (5 displays)", result);
    }

    [TestMethod]
    public void Format_AllMoreThanFour_UsesRangeAndCount()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            AllRangeValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays · 35%–90% (6 displays)", result);
    }

    [TestMethod]
    public void Format_Boundaries_ShowZeroAndOneHundred()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            BoundaryValues);

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays · 0%, 100%", result);
    }

    [TestMethod]
    public void Format_EmptyValues_ReturnsNull()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            Array.Empty<int>());

        Assert.IsNull(TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture));
    }

    [TestMethod]
    public void Format_DisabledMode_ReturnsNull()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.Disabled,
            DisabledValues);

        Assert.IsNull(TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture));
    }

    [TestMethod]
    public void Format_BrokenLocalizedTemplate_UsesNeutralEnglishTemplate()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            BrokenTemplateValues);

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(primary: "Broken {1"),
            Culture);

        Assert.AreEqual("Primary display · 55%", result);
    }

    [TestMethod]
    public void Format_BrokenPercentageTemplate_UsesNeutralPercentage()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            BrokenPercentageValues);

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(percentage: "{1}%"),
            Culture);

        Assert.AreEqual("All displays · 25%, 75%", result);
    }

    [TestMethod]
    public void Format_LimitsUtf16LengthWithoutDanglingHighSurrogate()
    {
        var longPrimary = new string('A', 126) + "\U0001F600{0}";
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            LongLengthValues);

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(primary: longPrimary),
            Culture,
            maxLength: 127);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length <= 127);
        Assert.IsFalse(char.IsHighSurrogate(result[^1]));
    }
}
