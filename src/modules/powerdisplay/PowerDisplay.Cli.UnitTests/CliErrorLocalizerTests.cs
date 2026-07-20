// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Output;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests for <see cref="CliErrorLocalizer"/> (the app-Code/MessageId -> localized text mapping) and
/// the <see cref="TextCliOutput.WriteError"/> rendering that consumes it. The app sends only ids +
/// structured data; these pin that the CLI composes the human text from them, and falls back to the
/// app's English message for an unrecognized id.
/// </summary>
[TestClass]
public class CliErrorLocalizerTests
{
    [TestMethod]
    public void Localize_OutOfRange_SubstitutesValueAndSetting()
    {
        var (message, hint) = CliErrorLocalizer.Localize(new CliError
        {
            Code = CliErrorCodes.OutOfRange,
            MessageId = CliMessageIds.OutOfRange,
            Value = "150",
            Setting = "brightness",
        });

        Assert.AreEqual("150 is out of range for brightness", message);
        Assert.IsNull(hint);
    }

    [TestMethod]
    public void Localize_Unsupported_UsesSettingName()
    {
        var (message, _) = CliErrorLocalizer.Localize(new CliError
        {
            MessageId = CliMessageIds.Unsupported,
            Setting = "volume",
        });

        Assert.AreEqual("volume is not supported", message);
    }

    [TestMethod]
    public void Localize_UnknownSetting_ProducesCliGeneratedHint()
    {
        // The hint's valid-settings list is CLI-known data, generated here (not sent by the app).
        var (message, hint) = CliErrorLocalizer.Localize(new CliError
        {
            MessageId = CliMessageIds.UnknownSetting,
            Value = "foo",
        });

        Assert.AreEqual("unknown setting foo", message);
        Assert.IsNotNull(hint);
        StringAssert.Contains(hint, "brightness");
    }

    [TestMethod]
    public void Localize_HardwareFailure_MessageIsFixed_DetailRenderedSeparately()
    {
        // The driver string travels in Detail (rendered on its own line), not folded into the message.
        var (message, hint) = CliErrorLocalizer.Localize(new CliError
        {
            MessageId = CliMessageIds.HardwareFailure,
            Detail = "DDC write timed out",
        });

        Assert.AreEqual("hardware write failed", message);
        Assert.IsNull(hint);
    }

    [TestMethod]
    public void Localize_UnknownMessageId_FallsBackToAppMessageAndHint()
    {
        // Version-skew safety: an id the CLI does not recognize degrades to the app's English prose.
        var (message, hint) = CliErrorLocalizer.Localize(new CliError
        {
            MessageId = "an-id-a-future-app-added",
            Message = "english fallback",
            Hint = "english hint",
        });

        Assert.AreEqual("english fallback", message);
        Assert.AreEqual("english hint", hint);
    }

    [TestMethod]
    public void Localize_EmptyMessageId_FallsBackToAppMessage()
    {
        // CLI-side errors (parse/validation) already carry a localized Message and no MessageId.
        var (message, _) = CliErrorLocalizer.Localize(new CliError
        {
            Message = "already-localized cli-side message",
        });

        Assert.AreEqual("already-localized cli-side message", message);
    }

    [TestMethod]
    public void WriteError_OutOfRange_RendersMessageExpectedAndLabels()
    {
        var stderr = new StringWriter();
        var output = new TextCliOutput(new StringWriter(), stderr, quiet: false);

        output.WriteError(new CliErrorResult
        {
            Command = "set",
            Error = new CliError
            {
                Code = CliErrorCodes.OutOfRange,
                MessageId = CliMessageIds.OutOfRange,
                Value = "150",
                Setting = "brightness",
                ExpectedRange = "[0, 100]",
            },
        });

        var text = stderr.ToString();
        StringAssert.Contains(text, "150 is out of range for brightness");
        StringAssert.Contains(text, "[0, 100]");
    }

    [TestMethod]
    public void WriteError_HardwareFailure_RendersDetailLine()
    {
        var stderr = new StringWriter();
        var output = new TextCliOutput(new StringWriter(), stderr, quiet: false);

        output.WriteError(new CliErrorResult
        {
            Command = "set",
            Error = new CliError
            {
                Code = CliErrorCodes.HardwareFailure,
                MessageId = CliMessageIds.HardwareFailure,
                Detail = "DDC write timed out",
            },
        });

        var text = stderr.ToString();
        StringAssert.Contains(text, "hardware write failed");
        StringAssert.Contains(text, "DDC write timed out");
    }
}
