// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Pages;
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
    /// Gets the shared application list-item source used by the page and command-provider tests.
    /// </summary>
    protected IAppListItemSource AppListItemSource { get; private set; } = null!;

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
        AppListItemSource = new AppListItemSource(MockCatalog, Settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<AppListItemSource>.Instance);
        Page = new AllAppsPage(AppListItemSource, TestDataHelper.CreateFuzzyMatcherProvider());

        await WaitForPageInitializationAsync();
    }

    /// <summary>
    /// Cleans up the test environment after each test method.
    /// </summary>
    [TestCleanup]
    public virtual void Cleanup()
    {
        Page?.Dispose();
        AppListItemSource?.Dispose();
        MockCatalog?.Dispose();
        TestDataHelper.DeleteSettingsFiles(_settingsPath);
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
    internal static async Task WaitForPageInitializationAsync(AllAppsPage page, int timeoutMs = 1000)
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

    /// <summary>
    /// Waits for an asynchronous application-list transition with a timeout.
    /// </summary>
    protected static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 1000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }

        if (!condition())
        {
            throw new TimeoutException("The expected application list state was not reached in time.");
        }
    }
}
