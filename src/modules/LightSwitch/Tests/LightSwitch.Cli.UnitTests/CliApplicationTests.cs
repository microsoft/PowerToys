// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.Cli.UnitTests;

[TestClass]
public sealed class CliApplicationTests
{
    internal const string SuccessfulResponse = """
        {"version":1,"success":true,"state":{"systemTheme":"light","appsTheme":"dark","changeSystem":true,"changeApps":false,"scheduleMode":"FixedHours","manualOverride":true}}
        """;

    private static readonly string[] VersionArguments = { "--version" };
    private static readonly string[] TerminatorArguments = { "status", "--", "--help" };
    private static readonly string[] StatusArguments = { "status" };
    private static readonly string[] LightArguments = { "light" };
    private static readonly string[] ToggleJsonArguments = { "toggle", "--json" };
    private static readonly string[] LightJsonArguments = { "light", "--json" };
    private static readonly string[] StatusJsonArguments = { "status", "--json" };

    [TestMethod]
    [DataRow("status", "status", null)]
    [DataRow("light", "light", null)]
    [DataRow("dark", "dark", null)]
    [DataRow("toggle", "toggle", null)]
    [DataRow("schedule enable", "schedule-enable", null)]
    [DataRow("schedule disable", "schedule-disable", null)]
    [DataRow("schedule enable --mode fixed-hours", "schedule-enable", "FixedHours")]
    [DataRow("schedule enable --mode sunset-to-sunrise", "schedule-enable", "SunsetToSunrise")]
    [DataRow("schedule enable --mode follow-night-light", "schedule-enable", "FollowNightLight")]
    public async Task CommandsSendExactlyTheExpectedRequest(string arguments, string command, string? mode)
    {
        string? receivedRequest = null;
        int calls = 0;
        var application = new CliApplication((request, _) =>
        {
            receivedRequest = request;
            calls++;
            return Task.FromResult(SuccessfulResponse);
        });

        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        int exit = await application.RunAsync((arguments + " --json").Split(' '), stdout, stderr);

        Assert.AreEqual(0, exit);
        Assert.AreEqual(1, calls);
        Assert.IsNotNull(receivedRequest);
        using var requestDocument = JsonDocument.Parse(receivedRequest);
        Assert.AreEqual(1, requestDocument.RootElement.GetProperty("version").GetInt32());
        Assert.AreEqual(command, requestDocument.RootElement.GetProperty("command").GetString());
        if (mode is null)
        {
            Assert.IsFalse(requestDocument.RootElement.TryGetProperty("mode", out _));
        }
        else
        {
            Assert.AreEqual(mode, requestDocument.RootElement.GetProperty("mode").GetString());
        }

        int propertyCount = 0;
        foreach (var property in requestDocument.RootElement.EnumerateObject())
        {
            propertyCount++;
        }

        Assert.AreEqual(mode is null ? 2 : 3, propertyCount, "The native endpoint rejects unknown request fields.");
        using var output = JsonDocument.Parse(stdout.ToString());
        Assert.IsTrue(output.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [DataRow("--json toggle")]
    [DataRow("toggle --json")]
    [DataRow("schedule --json enable")]
    public async Task JsonOptionIsGlobal(string arguments)
    {
        var application = new CliApplication(static (_, _) => Task.FromResult(SuccessfulResponse));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(arguments.Split(' '), stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.IsTrue(response.RootElement.GetProperty("success").GetBoolean());
    }

    [TestMethod]
    [DataRow("schedule enable --mode automatic")]
    [DataRow("schedule enable --mode Off")]
    [DataRow("schedule enable --mode")]
    [DataRow("schedule disable --mode fixed-hours")]
    [DataRow("schedule")]
    [DataRow("light unexpected")]
    [DataRow("missing-command")]
    [DataRow("status --unknown --another-unknown")]
    [DataRow("schedule enable --mode=--json")]
    [DataRow("schedule enable --mode=--help")]
    [DataRow("schedule enable --mode=--version")]
    public async Task InvalidArgumentsProduceOneJsonErrorWithoutCallingTheService(string arguments)
    {
        int calls = 0;
        var application = new CliApplication((_, _) =>
        {
            calls++;
            return Task.FromResult(SuccessfulResponse);
        });
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        int exit = await application.RunAsync((arguments + " --json").Split(' '), stdout, stderr);

        Assert.AreEqual(2, exit);
        Assert.AreEqual(0, calls);
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.IsFalse(response.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("INVALID_ARGUMENT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--help")]
    [DataRow("-h")]
    [DataRow("schedule enable --help")]
    [DataRow("schedule enable --mode --help")]
    [DataRow("schedule enable --mode -h")]
    [DataRow("schedule enable --mode -?")]
    public async Task HelpAndNoArgumentsDoNotContactTheService(string arguments)
    {
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Help must not invoke IPC."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        int exit = await application.RunAsync(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), stdout, stderr);

        Assert.AreEqual(0, exit);
        StringAssert.Contains(stdout.ToString(), "schedule enable");
        StringAssert.Contains(stdout.ToString(), "--mode");
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [DataRow("--json", "help")]
    [DataRow("--json --help", "help")]
    [DataRow("schedule enable --json --help", "help")]
    [DataRow("--json --version", "cliVersion")]
    [DataRow("light --version --json", "cliVersion")]
    [DataRow("schedule enable --mode --help --json", "help")]
    [DataRow("schedule enable --mode --json --help", "help")]
    [DataRow("schedule enable --mode --version --json", "cliVersion")]
    [DataRow("schedule enable --mode --json --version", "cliVersion")]
    public async Task JsonInformationUsesTheLocalInformationEnvelope(string arguments, string informationField)
    {
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Information must not invoke IPC."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(arguments.Split(' '), stdout, stderr));

        using var information = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual(1, information.RootElement.GetProperty("version").GetInt32());
        Assert.IsTrue(information.RootElement.GetProperty("success").GetBoolean());
        Assert.IsFalse(string.IsNullOrWhiteSpace(information.RootElement.GetProperty(informationField).GetString()));
        Assert.IsFalse(information.RootElement.TryGetProperty("state", out _));
        Assert.IsFalse(information.RootElement.TryGetProperty("error", out _));
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    public async Task VersionIsLocalAndPrintedAsText()
    {
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Version must not invoke IPC."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(VersionArguments, stdout, stderr));
        Assert.AreEqual(CliApplication.CliVersion, stdout.ToString().Trim());
    }

    [TestMethod]
    public async Task OptionTerminatorDoesNotTurnAnArgumentIntoHelp()
    {
        var application = new CliApplication(static (_, _) => Task.FromResult(SuccessfulResponse));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(2, await application.RunAsync(TerminatorArguments, stdout, stderr));
        Assert.AreEqual(string.Empty, stdout.ToString());
        StringAssert.Contains(stderr.ToString(), "INVALID_ARGUMENT");
    }

    [TestMethod]
    [DataRow("schedule enable --mode -- --json")]
    [DataRow("schedule enable --mode -- --help")]
    [DataRow("schedule enable --mode -- --version")]
    [DataRow("status -- --json")]
    [DataRow("status -- --version")]
    public async Task FlagsAfterTheTerminatorRemainArguments(string arguments)
    {
        int calls = 0;
        var application = new CliApplication((_, _) =>
        {
            calls++;
            return Task.FromResult(SuccessfulResponse);
        });
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(2, await application.RunAsync(arguments.Split(' '), stdout, stderr));
        Assert.AreEqual(0, calls);
        Assert.AreEqual(string.Empty, stdout.ToString());
        StringAssert.Contains(stderr.ToString(), "INVALID_ARGUMENT");
    }

    [TestMethod]
    [DataRow("schedule enable --mode --json -- --help")]
    [DataRow("schedule enable --mode --json -- --version")]
    public async Task JsonBeforeTheTerminatorStillFormatsInvalidArgumentsAfterIt(string arguments)
    {
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Invalid input must not invoke IPC."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(2, await application.RunAsync(arguments.Split(' '), stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual("INVALID_ARGUMENT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    public async Task InvalidArgumentsReachTheErrorLoggerBeforeAnyTransportCall()
    {
        string? logged = null;
        var application = new CliApplication(
            static (_, _) => throw new InvalidOperationException("Invalid input must not invoke IPC."),
            message => logged = message);
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(2, await application.RunAsync(TerminatorArguments, stdout, stderr));
        Assert.IsNotNull(logged);
        StringAssert.Contains(logged, "INVALID_ARGUMENT");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--json")]
    [DataRow("schedule enable --mode --help")]
    [DataRow("schedule enable --mode --version --json")]
    public async Task InformationDoesNotInitializeLogging(string arguments)
    {
        int logCalls = 0;
        var application = new CliApplication(
            static (_, _) => throw new InvalidOperationException("Information must not invoke IPC."),
            _ => logCalls++);
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), stdout, stderr));
        Assert.AreEqual(0, logCalls);
    }

    [TestMethod]
    public async Task TextStatusIncludesBothTargetsAndTheManualOverride()
    {
        var application = new CliApplication(static (_, _) => Task.FromResult(SuccessfulResponse));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(StatusArguments, stdout, stderr));
        StringAssert.Contains(stdout.ToString(), "System theme: light");
        StringAssert.Contains(stdout.ToString(), "Apps theme: dark");
        StringAssert.Contains(stdout.ToString(), "App theme changes: disabled");
        StringAssert.Contains(stdout.ToString(), "Manual override: active");
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    public async Task TextErrorsGoToStderr()
    {
        var application = new CliApplication(static (_, _) => throw new CliException("SERVICE_UNAVAILABLE", "Service unavailable."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(3, await application.RunAsync(LightArguments, stdout, stderr));
        Assert.AreEqual(string.Empty, stdout.ToString());
        StringAssert.Contains(stderr.ToString(), "SERVICE_UNAVAILABLE");
    }

    [TestMethod]
    [DataRow("INVALID_ARGUMENT", 2)]
    [DataRow("INVALID_CONFIGURATION", 2)]
    [DataRow("SERVICE_UNAVAILABLE", 3)]
    [DataRow("SERVER_BUSY", 3)]
    [DataRow("TIMEOUT", 4)]
    [DataRow("EXECUTION_FAILED", 1)]
    [DataRow("NO_TARGETS", 1)]
    [DataRow("PROTOCOL_ERROR", 1)]
    [DataRow("FUTURE_SERVICE_ERROR", 1)]
    public async Task ServiceErrorsPreserveTheCodeAndMapToAnExitCode(string code, int expectedExit)
    {
        string responseJson = "{\"version\":1,\"success\":false,\"error\":{\"code\":\"" + code + "\",\"message\":\"Failed.\"}}";
        var application = new CliApplication((_, _) => Task.FromResult(responseJson));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(expectedExit, await application.RunAsync(ToggleJsonArguments, stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual(code, response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    public async Task InvalidServiceResponseProducesOneProtocolError()
    {
        var application = new CliApplication(static (_, _) => Task.FromResult("{}"));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(1, await application.RunAsync(StatusJsonArguments, stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.IsFalse(response.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("PROTOCOL_ERROR", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task OverallDeadlineDoesNotRetryAMutation()
    {
        int calls = 0;
        var neverCompletes = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var application = new CliApplication(
            (_, _) =>
            {
                calls++;
                return neverCompletes.Task;
            },
            operationTimeout: TimeSpan.FromMilliseconds(50));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(4, await application.RunAsync(ToggleJsonArguments, stdout, stderr));
        Assert.AreEqual(1, calls);
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual("TIMEOUT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        StringAssert.Contains(response.RootElement.GetProperty("error").GetProperty("message").GetString()!, "may already have taken effect");
    }

    [TestMethod]
    public async Task CancellationProducesASingleTimeoutEnvelope()
    {
        var application = new CliApplication(static (_, token) => Task.FromCanceled<string>(token));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(4, await application.RunAsync(LightJsonArguments, stdout, stderr, cancellation.Token));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual("TIMEOUT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task UnexpectedFailureAndBrokenLoggerDoNotLeakStackTracesIntoJson()
    {
        var application = new CliApplication(
            static (_, _) => throw new InvalidOperationException("Private diagnostic detail."),
            static _ => throw new IOException("Log unavailable."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(1, await application.RunAsync(StatusJsonArguments, stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.AreEqual("EXECUTION_FAILED", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.IsFalse(stdout.ToString().Contains("Private diagnostic detail", StringComparison.Ordinal));
        Assert.AreEqual(string.Empty, stderr.ToString());
    }
}
