// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LightSwitch.Cli.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.Cli.UnitTests;

[TestClass]
public sealed class CliLocalizationTests
{
    private static readonly (string Arguments, string Command, string? Mode)[] Commands =
    {
        ("status", "status", null),
        ("light", "light", null),
        ("dark", "dark", null),
        ("toggle", "toggle", null),
        ("schedule disable", "schedule-disable", null),
        ("schedule enable", "schedule-enable", null),
        ("schedule enable --mode fixed-hours", "schedule-enable", "FixedHours"),
        ("schedule enable --mode sunset-to-sunrise", "schedule-enable", "SunsetToSunrise"),
        ("schedule enable --mode follow-night-light", "schedule-enable", "FollowNightLight"),
    };

    private static readonly (string Code, int ExitCode)[] Errors =
    {
        ("INVALID_ARGUMENT", 2),
        ("SERVICE_UNAVAILABLE", 3),
        ("TIMEOUT", 4),
        ("THEME_WRITE_FAILED", 1),
    };

    private static readonly string[] StatusArguments = { "status" };
    private static readonly string[] JsonStatusArguments = { "status", "--json" };
    private static readonly string[] InvalidCommandArguments = { "invalid-command", "--json" };

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("tr-TR")]
    [DataRow("zh-CN")]
    [DataRow("ar-SA")]
    public async Task JsonCommandsKeepTheirMachineContractAcrossCultures(string cultureName)
    {
        using var culture = new CultureScope(cultureName);
        foreach (var command in Commands)
        {
            string? sentRequest = null;
            var application = new CliApplication((request, _) =>
            {
                sentRequest = request;
                return Task.FromResult(CliApplicationTests.SuccessfulResponse);
            });
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            Assert.AreEqual(0, await application.RunAsync((command.Arguments + " --json").Split(' '), stdout, stderr));
            Assert.IsNotNull(sentRequest);
            using var request = JsonDocument.Parse(sentRequest);
            Assert.AreEqual(command.Command, request.RootElement.GetProperty("command").GetString());
            Assert.AreEqual(1, request.RootElement.GetProperty("version").GetInt32());
            if (command.Mode is null)
            {
                Assert.IsFalse(request.RootElement.TryGetProperty("mode", out _));
            }
            else
            {
                Assert.AreEqual(command.Mode, request.RootElement.GetProperty("mode").GetString());
            }

            // Parse the entire output, so localized prefixes, a second object or
            // logging on stdout would fail instead of being ignored.
            using var response = JsonDocument.Parse(stdout.ToString());
            Assert.AreEqual(JsonValueKind.Object, response.RootElement.ValueKind);
            Assert.AreEqual(1, response.RootElement.GetProperty("version").GetInt32());
            Assert.IsTrue(response.RootElement.GetProperty("success").GetBoolean());
            var state = response.RootElement.GetProperty("state");
            Assert.AreEqual("light", state.GetProperty("systemTheme").GetString());
            Assert.AreEqual("dark", state.GetProperty("appsTheme").GetString());
            Assert.AreEqual("FixedHours", state.GetProperty("scheduleMode").GetString());
            Assert.IsTrue(state.GetProperty("changeSystem").GetBoolean());
            Assert.IsFalse(state.GetProperty("changeApps").GetBoolean());
            Assert.IsTrue(state.GetProperty("manualOverride").GetBoolean());
            Assert.AreEqual(string.Empty, stderr.ToString());
        }
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("zh-CN")]
    [DataRow("ar-SA")]
    public async Task LocalizedServiceErrorsRoundTripWithoutChangingCodes(string cultureName)
    {
        using var culture = new CultureScope(cultureName);
        const string message = "主题 \"深色\" — échec العربية\n路径 C:\\配置\\LightSwitch\t再试一次。";
        foreach (var error in Errors)
        {
            string serviceResponse = JsonSerializer.Serialize(new
            {
                version = 1,
                success = false,
                error = new { code = error.Code, message },
            });
            var application = new CliApplication((_, _) => Task.FromResult(serviceResponse));
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            Assert.AreEqual(error.ExitCode, await application.RunAsync(JsonStatusArguments, stdout, stderr));
            using var response = JsonDocument.Parse(stdout.ToString());
            Assert.AreEqual(1, response.RootElement.GetProperty("version").GetInt32());
            Assert.IsFalse(response.RootElement.GetProperty("success").GetBoolean());
            var returnedError = response.RootElement.GetProperty("error");
            Assert.AreEqual(error.Code, returnedError.GetProperty("code").GetString());
            Assert.AreEqual(message, returnedError.GetProperty("message").GetString());
            Assert.AreEqual(string.Empty, stderr.ToString());
        }
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("zh-CN")]
    [DataRow("ar-SA")]
    public async Task LocalInformationAndArgumentErrorsKeepJsonEnvelopes(string cultureName)
    {
        using var culture = new CultureScope(cultureName);
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Local output must not contact the service."));
        foreach (var (arguments, field, expectedText, exitCode) in new[]
        {
            ("--help --json", "help", Resources.Help_Text, 0),
            ("--version --json", "cliVersion", CliApplication.CliVersion, 0),
            ("schedule enable --mode invalid --json", "error", Resources.Error_UnknownScheduleMode, 2),
        })
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Assert.AreEqual(exitCode, await application.RunAsync(arguments.Split(' '), stdout, stderr));
            using var response = JsonDocument.Parse(stdout.ToString());
            Assert.AreEqual(1, response.RootElement.GetProperty("version").GetInt32());
            Assert.AreEqual(exitCode == 0, response.RootElement.GetProperty("success").GetBoolean());
            if (exitCode == 0)
            {
                Assert.AreEqual(expectedText, response.RootElement.GetProperty(field).GetString());
            }
            else
            {
                var error = response.RootElement.GetProperty(field);
                Assert.AreEqual("INVALID_ARGUMENT", error.GetProperty("code").GetString());
                Assert.AreEqual(expectedText, error.GetProperty("message").GetString());
            }

            Assert.AreEqual(string.Empty, stderr.ToString());
        }
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("zh-CN")]
    [DataRow("ar-SA")]
    public async Task TextStatusUsesDisplayResourcesWithoutChangingTheResponse(string cultureName)
    {
        using var culture = new CultureScope(cultureName);
        var application = new CliApplication(static (_, _) => Task.FromResult(CliApplicationTests.SuccessfulResponse));
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        Assert.AreEqual(0, await application.RunAsync(StatusArguments, stdout, stderr));
        string expected = string.Join(
            Environment.NewLine,
            Resources.Text_SystemTheme(Resources.Value_Light),
            Resources.Text_AppsTheme(Resources.Value_Dark),
            Resources.Text_ChangeSystem(Resources.Value_Enabled),
            Resources.Text_ChangeApps(Resources.Value_Disabled),
            Resources.Text_ScheduleMode(Resources.Value_FixedHours),
            Resources.Text_ManualOverride(Resources.Value_Active)) + Environment.NewLine;
        Assert.AreEqual(expected, stdout.ToString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("zh-CN")]
    [DataRow("ar-SA")]
    public async Task ParserErrorsKeepTheirJsonContractAcrossCultures(string cultureName)
    {
        using var culture = new CultureScope(cultureName);
        var application = new CliApplication(static (_, _) => throw new InvalidOperationException("Invalid input must not contact the service."));
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Assert.AreEqual(2, await application.RunAsync(InvalidCommandArguments, stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.IsFalse(response.RootElement.GetProperty("success").GetBoolean());
        var error = response.RootElement.GetProperty("error");
        Assert.AreEqual("INVALID_ARGUMENT", error.GetProperty("code").GetString());
        Assert.AreEqual(string.Empty, stderr.ToString());
        string? message = error.GetProperty("message").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(message));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUICulture = CultureInfo.CurrentUICulture;

        internal CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }
}
