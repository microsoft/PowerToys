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
    [DataRow("--json=true toggle")]
    [DataRow("toggle --json:true")]
    [DataRow("schedule --json True enable")]
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
    [DataRow("light --help=invalid")]
    [DataRow("light --help:invalid")]
    [DataRow("light --help=light")]
    [DataRow("light -h:invalid")]
    [DataRow("toggle --version=invalid")]
    [DataRow("toggle --version:invalid")]
    [DataRow("status --json=invalid")]
    [DataRow("status --json:invalid")]
    [DataRow("light --help=--json")]
    [DataRow("schedule enable --mode --help=invalid")]
    [DataRow("light --help=true --help=false")]
    [DataRow("light --help=false --help=true")]
    [DataRow("toggle --version=false --version=true")]
    [DataRow("[parse] toggle")]
    [DataRow("[parse] dark --help=false")]
    [DataRow("[suggest] light")]
    [DataRow("[suggest:6] dark")]
    [DataRow("[bogus] schedule disable")]
    [DataRow("[env:FOO=bar] schedule enable --mode fixed-hours")]
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
    [DataRow("light --help=true")]
    [DataRow("light --help:true")]
    [DataRow("light -h:true")]
    [DataRow("light -?:true")]
    [DataRow("light --help True")]
    [DataRow("light --help --help")]
    [DataRow("light --help=true --help")]
    [DataRow("--json=false")]
    [DataRow("--json false")]
    [DataRow("--json=false --help=true")]
    [DataRow("schedule enable --mode --json false --help")]
    [DataRow("[parse] light --help")]
    [DataRow("[suggest] dark --help=true")]
    [DataRow("light --help -- [parse]")]
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
    [DataRow("--json=true", "help")]
    [DataRow("--json:True", "help")]
    [DataRow("--json true", "help")]
    [DataRow("light --help=true --json=true", "help")]
    [DataRow("light --help:true --json:true", "help")]
    [DataRow("--help=true --json=true", "help")]
    [DataRow("toggle --version=true --json=true", "cliVersion")]
    [DataRow("toggle --version:true --json:true", "cliVersion")]
    [DataRow("toggle --version True --json true", "cliVersion")]
    [DataRow("toggle --help=false --version=true --json", "cliVersion")]
    [DataRow("--version=true --json=true", "cliVersion")]
    [DataRow("schedule enable --mode --help=true --json=true", "help")]
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
    [DataRow("light --help=false")]
    [DataRow("light --help false")]
    [DataRow("light -h:false")]
    [DataRow("light --help --help=false")]
    [DataRow("toggle --version=false")]
    [DataRow("toggle --version false")]
    [DataRow("toggle --version --version=false")]
    [DataRow("status --json=false")]
    [DataRow("status --json false")]
    [DataRow("status --json --json=false")]
    [DataRow("status --json=false --json")]
    public async Task FalsePresentationOptionsDoNotSuppressCommandsOrEnableJson(string arguments)
    {
        int calls = 0;
        var application = new CliApplication((request, _) =>
        {
            calls++;
            using var parsed = JsonDocument.Parse(request);
            Assert.AreEqual(arguments.Split(' ')[0], parsed.RootElement.GetProperty("command").GetString());
            return Task.FromResult(SuccessfulResponse);
        });
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(0, await application.RunAsync(arguments.Split(' '), stdout, stderr));
        Assert.AreEqual(1, calls);
        StringAssert.StartsWith(stdout.ToString(), "System theme:");
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [DataRow("status --json=true --json=false")]
    [DataRow("status --json:false --json:true")]
    public async Task ConflictingJsonValuesFailWithoutCallingTheService(string arguments)
    {
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Invalid input must not invoke IPC."));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreEqual(2, await application.RunAsync(arguments.Split(' '), stdout, stderr));
        Assert.AreEqual(string.Empty, stdout.ToString());
        StringAssert.Contains(stderr.ToString(), "INVALID_ARGUMENT");
    }

    [TestMethod]
    [DataRow("light --help=true --json", "help", 0, 0)]
    [DataRow("toggle --version:true --json", "cliVersion", 0, 0)]
    [DataRow("status --json:true", "state", 0, 1)]
    [DataRow("light --help=invalid --json", "error", 2, 0)]
    [DataRow("[parse] toggle --json", "error", 2, 0)]
    [DataRow("[suggest] dark --json", "error", 2, 0)]
    [DataRow("[bogus] schedule disable --json", "error", 2, 0)]
    public async Task ResponseFilesKeepPresentationAndCommandParsingConsistent(string contents, string outputField, int expectedExit, int expectedCalls)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, contents);
            int calls = 0;
            var application = new CliApplication((_, _) =>
            {
                calls++;
                return Task.FromResult(SuccessfulResponse);
            });
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);

            Assert.AreEqual(expectedExit, await application.RunAsync(new[] { "@" + path }, stdout, stderr));
            Assert.AreEqual(expectedCalls, calls);
            using var response = JsonDocument.Parse(stdout.ToString());
            Assert.IsTrue(response.RootElement.TryGetProperty(outputField, out _));
            Assert.AreEqual(string.Empty, stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow("--mode fixed-hours --json", "state", 0, 1)]
    [DataRow("--mode fixed-hours --help --json", "help", 0, 0)]
    [DataRow("--help --json", "help", 0, 0)]
    [DataRow("--mode --help --json", "help", 0, 0)]
    [DataRow("--mode --help=true --json", "help", 0, 0)]
    [DataRow("--mode=--help --json", "error", 2, 0)]
    public async Task ResponseFilesCanSupplyOptionsForAnExistingCommand(string contents, string outputField, int expectedExit, int expectedCalls)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, contents);
            int calls = 0;
            var application = new CliApplication((request, _) =>
            {
                calls++;
                using var parsed = JsonDocument.Parse(request);
                Assert.AreEqual("schedule-enable", parsed.RootElement.GetProperty("command").GetString());
                Assert.AreEqual("FixedHours", parsed.RootElement.GetProperty("mode").GetString());
                return Task.FromResult(SuccessfulResponse);
            });
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);

            Assert.AreEqual(expectedExit, await application.RunAsync(new[] { "schedule", "enable", "@" + path }, stdout, stderr));
            Assert.AreEqual(expectedCalls, calls);
            using var response = JsonDocument.Parse(stdout.ToString());
            Assert.IsTrue(response.RootElement.TryGetProperty(outputField, out _));
            Assert.AreEqual(string.Empty, stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow("--help", "--json=invalid", 0)]
    [DataRow("--json", "--help=true", 2)]
    public async Task ResponseFileTerminatorAppliesToLaterArguments(string before, string after, int expectedExit)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "--");
            var application = new CliApplication(static (_, _) => throw new InvalidOperationException("These arguments must not invoke IPC."));
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);

            Assert.AreEqual(expectedExit, await application.RunAsync(new[] { "light", before, "@" + path, after }, stdout, stderr));
            if (expectedExit == 0)
            {
                StringAssert.StartsWith(stdout.ToString(), "PowerToys Light Switch");
            }
            else
            {
                using var response = JsonDocument.Parse(stdout.ToString());
                Assert.AreEqual("INVALID_ARGUMENT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
            }

            Assert.AreEqual(string.Empty, stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CommandParsingReusesTheResponseFileSnapshot()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "--mode fixed-hours --json");
            var presentation = CliCommandLine.ParsePresentationOptions(new[] { "schedule", "enable", "@" + path });
            Assert.IsNull(presentation.Error);
            Assert.IsTrue(presentation.Json);
            Assert.IsFalse(presentation.Help);

            File.WriteAllText(path, "--mode follow-night-light");
            var commandLine = new CliCommandLine();
            var parsed = commandLine.Parse(presentation.Arguments);
            Assert.AreEqual(0, parsed.Errors.Count);
            Assert.AreEqual("FixedHours", commandLine.CreateRequest(parsed).Mode);
        }
        finally
        {
            File.Delete(path);
        }
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
    [DataRow("light -- --help=true")]
    [DataRow("toggle -- --version:true")]
    [DataRow("status -- --json=true")]
    [DataRow("light -- [parse]")]
    [DataRow("toggle -- [suggest]")]
    [DataRow("schedule disable -- [bogus]")]
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
    [DataRow("status --json=true -- --help=true")]
    [DataRow("schedule enable --mode --json:true -- --version:true")]
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
    [DataRow("light --help:true --json:true")]
    [DataRow("toggle --version=true --json=false")]
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
