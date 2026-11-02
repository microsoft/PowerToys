// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerDisplay.UITests;

internal sealed class VirtualMonitorFixture
{
    private readonly string executable;

    public VirtualMonitorFixture(string executable)
    {
        Assert.IsTrue(Path.IsPathFullyQualified(executable) && File.Exists(executable), $"Invalid IDD fixture probe path: '{executable}'.");
        this.executable = executable;
        var snapshot = Snapshot();
        Assert.AreEqual(2, snapshot.Version, "The fixture and UI suite use incompatible protocols.");
        Assert.AreEqual(2, snapshot.Monitors.Length, "The fixture must expose exactly two independently controlled monitor slots.");
    }

    public FixtureSnapshot Snapshot() => Invoke("--snapshot");

    public void SetConnected(int connector, bool connected) =>
        Invoke("--connected", connector.ToString(System.Globalization.CultureInfo.InvariantCulture), connected ? "1" : "0");

    public void SetMode(int connector, int mode) =>
        Invoke("--mode", connector.ToString(System.Globalization.CultureInfo.InvariantCulture), mode.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public void Reset()
    {
        for (var connector = 0; connector < 2; connector++)
        {
            SetMode(connector, 0);
            SetConnected(connector, true);
        }
    }

    public static bool IsFixtureId(string id) =>
        id.Contains("DISPLAY#DELD0E6#", StringComparison.OrdinalIgnoreCase) ||
        id.Contains("DISPLAY#LEN65BF#", StringComparison.OrdinalIgnoreCase);

    public static int ConnectorForId(string id) =>
        id.Contains("DISPLAY#DELD0E6#", StringComparison.OrdinalIgnoreCase) ? 0 :
        id.Contains("DISPLAY#LEN65BF#", StringComparison.OrdinalIgnoreCase) ? 1 :
        throw new ArgumentException("Not a fixture monitor identity.", nameof(id));

    private FixtureSnapshot Invoke(params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the IDD fixture probe.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("The fixture control request timed out.");
        }

        Task.WaitAll(stdout, stderr);
        Assert.AreEqual(0, process.ExitCode, $"Fixture command '{string.Join(' ', arguments)}' failed: {stderr.Result}\n{stdout.Result}");
        return JsonSerializer.Deserialize<FixtureSnapshot>(stdout.Result) ?? throw new JsonException("Missing fixture snapshot.");
    }

    internal sealed record FixtureSnapshot(int Version, MonitorSnapshot[] Monitors);

    internal sealed record MonitorSnapshot(
        int Connector,
        int Brightness,
        int Gets,
        int Sets,
        int Failures,
        bool Connected,
        int Mode,
        int Arrivals,
        int Departures);
}
