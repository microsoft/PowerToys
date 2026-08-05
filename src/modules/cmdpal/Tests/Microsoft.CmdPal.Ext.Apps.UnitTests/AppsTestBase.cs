// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Base class for Apps unit tests that provides common setup and teardown functionality.
/// </summary>
public abstract class AppsTestBase
{
    private string _settingsPath = string.Empty;

    /// <summary>
    /// Gets the mock application cache used in tests.
    /// </summary>
    protected MockAppCatalog MockCatalog { get; private set; } = null!;

    /// <summary>
    /// Gets the AllAppsPage instance used in tests.
    /// </summary>
    protected AllAppsPage Page { get; private set; } = null!;

    /// <summary>
    /// Gets the isolated settings instance shared by the test's application services.
    /// </summary>
    protected AllAppsSettings Settings { get; private set; } = null!;

    /// <summary>
    /// Sets up the test environment before each test method.
    /// </summary>
    /// <returns>A task representing the asynchronous setup operation.</returns>
    [TestInitialize]
    public virtual async Task Setup()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), $"apps-settings-{Guid.NewGuid():N}.json");
        Settings = new AllAppsSettings(_settingsPath);
        MockCatalog = new MockAppCatalog();
        Page = new AllAppsPage(MockCatalog, Settings);

        await WaitForPageInitializationAsync();
    }

    /// <summary>
    /// Cleans up the test environment after each test method.
    /// </summary>
    [TestCleanup]
    public virtual void Cleanup()
    {
        MockCatalog?.Dispose();
        File.Delete(_settingsPath);
    }

    /// <summary>
    /// Forces synchronous initialization of the page for testing.
    /// </summary>
    protected void EnsurePageInitialized()
    {
        // Trigger BuildListItems by accessing items
        _ = Page.GetItems();
    }

    /// <summary>
    /// Waits for page initialization with timeout.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    protected async Task WaitForPageInitializationAsync(int timeoutMs = 1000)
    {
        await WaitForPageInitializationAsync(Page, timeoutMs);
    }

    /// <summary>
    /// Waits for the supplied page to finish loading with a timeout.
    /// </summary>
    /// <param name="page">The page to observe.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    protected static async Task WaitForPageInitializationAsync(AllAppsPage page, int timeoutMs = 1000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (page.IsLoading && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }

        if (page.IsLoading)
        {
            throw new TimeoutException("The All apps page did not finish loading in time.");
        }
    }
}
