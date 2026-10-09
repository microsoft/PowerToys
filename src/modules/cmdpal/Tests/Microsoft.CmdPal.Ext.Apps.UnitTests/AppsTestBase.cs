// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Base class for Apps unit tests that provides common setup and teardown functionality.
/// </summary>
public abstract class AppsTestBase
{
    /// <summary>
    /// Gets the mock application cache used in tests.
    /// </summary>
    protected MockAppCache MockCache { get; private set; } = null!;

    /// <summary>
    /// Gets the AllAppsPage instance used in tests.
    /// </summary>
    protected AllAppsPage Page { get; private set; } = null!;

    /// <summary>
    /// Sets up the test environment before each test method.
    /// </summary>
    /// <returns>A task representing the asynchronous setup operation.</returns>
    [TestInitialize]
    public virtual async Task Setup()
    {
        MockCache = new MockAppCache();
        Page = new AllAppsPage(MockCache);

        // Ensure initialization is complete
        await MockCache.RefreshAsync();
    }

    /// <summary>
    /// Cleans up the test environment after each test method.
    /// </summary>
    [TestCleanup]
    public virtual void Cleanup()
    {
        MockCache?.Dispose();
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
        await MockCache.RefreshAsync();
        EnsurePageInitialized();
    }
}
