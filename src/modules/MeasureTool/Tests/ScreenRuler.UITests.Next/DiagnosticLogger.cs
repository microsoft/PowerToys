// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

/// <summary>
/// Verbose per-test execution log. Every <see cref="Step"/> is timestamped with the elapsed seconds
/// since the test began, echoed to the <see cref="TestContext"/> (so it shows inline in the run
/// output) AND accumulated; <see cref="Save"/> writes the whole thing out as a
/// <c>TestExecutionLog_*.log</c> result artifact for post-mortem on CI. ScreenRuler UI tests run
/// sequentially, so a single ambient instance (see <c>TestHelper</c>) is safe.
/// </summary>
internal sealed class DiagnosticLogger
{
    private readonly UITestBase testBase;
    private readonly string testName;
    private readonly StringBuilder buffer = new();
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public DiagnosticLogger(UITestBase testBase, string testName)
    {
        this.testBase = testBase;
        this.testName = testName;
        Step($"===== {testName}: execution log started =====");
    }

    /// <summary>Append one timestamped step, echoing it to the TestContext immediately.</summary>
    public void Step(string message)
    {
        var line = $"[+{stopwatch.Elapsed.TotalSeconds,8:F2}s] {message}";
        buffer.AppendLine(line);
        try
        {
            testBase.TestContext.WriteLine(line);
        }
        catch
        {
            // TestContext can be unavailable late in teardown — the buffered copy is still saved.
        }
    }

    /// <summary>Flush the whole log to a result-attached file artifact (best-effort).</summary>
    public void Save()
    {
        Step($"===== {testName}: execution log ended =====");
        try
        {
            var dir = testBase.TestContext.TestResultsDirectory ?? Path.GetTempPath();
            Directory.CreateDirectory(dir);
            var safeName = string.Concat((testName ?? "test").Split(Path.GetInvalidFileNameChars()));
            var file = Path.Combine(dir, $"TestExecutionLog_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
            File.WriteAllText(file, buffer.ToString());
            testBase.TestContext.AddResultFile(file);
        }
        catch
        {
            // Best-effort artifact; the inline TestContext copy is the fallback.
        }
    }
}
