// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class OverlayManagerTelemetryTests
{
    [TestMethod]
    public void ShowAsync_EmitsPowerOCRInvokedTelemetryAfterSuccessfulActivation()
    {
        string overlayManagerPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                @"..\..\..\..\src\modules\PowerOCR\PowerOCR\Services\OverlayManager.cs"));

        string source = File.ReadAllText(overlayManagerPath);

        int activationIndex = source.IndexOf("window.Activate();", StringComparison.Ordinal);
        int telemetryIndex = source.IndexOf(
            "PowerToysTelemetry.Log.WriteEvent(new PowerOCRInvokedEvent());",
            StringComparison.Ordinal);
        int failureLogIndex = source.IndexOf(
            "Logger.LogError(\"Failed to create or activate overlay windows\", ex);",
            StringComparison.Ordinal);

        Assert.IsTrue(activationIndex >= 0, "Expected overlay activation loop in OverlayManager.cs.");
        Assert.IsTrue(telemetryIndex >= 0, "Expected PowerOCR invocation telemetry in OverlayManager.cs.");
        Assert.IsTrue(telemetryIndex > activationIndex, "Telemetry should be emitted after overlay activation.");
        Assert.IsTrue(failureLogIndex > telemetryIndex, "Telemetry should be emitted before overlay activation failure handling.");
    }
}
