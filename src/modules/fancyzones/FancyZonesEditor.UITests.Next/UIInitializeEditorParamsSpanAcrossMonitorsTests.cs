// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeEditorParamsSpanAcrossMonitorsTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsSpanAcrossMonitorsTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsSpanAcrossMonitors)
    {
    }

    [TestMethod]
    public void EditorParams_SpanAcrossMonitors()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);

        var parameters = new EditorParameters().Read(new EditorParameters().File);
        Assert.IsTrue(parameters.SpanZonesAcrossMonitors, "Expected SpanZonesAcrossMonitors to remain true.");

        var editorWindow = new IntPtr(Session.WindowHandle);
        WindowHelper.RestoreWindow(editorWindow);

        var (displayWidth, _) = WindowHelper.GetDisplaySize();
        var expectedCenter = displayWidth / 2.0;
        var centered = WaitHelper.WaitForStable(
            observe: () =>
            {
                var (left, _, right, _) = WindowHelper.GetWindowBounds(editorWindow);
                return (left + right) / 2.0;
            },
            isMatch: actualCenter => Math.Abs(actualCenter - expectedCenter) <= 2.0,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100);
        Assert.IsTrue(
            centered.Succeeded,
            $"Span-across-monitors mode should center the editor on the combined desktop. Expected center {expectedCenter}, actual {centered.LastObservation}.");
    }
}
