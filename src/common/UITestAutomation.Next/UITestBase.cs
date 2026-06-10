// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Base class for the next-generation PowerToys UI tests. Engine is winappcli — every UI call
/// shells out to <c>winapp.exe</c>. No WinAppDriver, no Selenium, no third-party NuGet packages.
/// </summary>
/// <remarks>
/// <para>
/// Drop-in shape replacement for the existing <c>Microsoft.PowerToys.UITest.UITestBase</c>:
/// inherit, pass a <see cref="PowerToysModule"/>, and use <c>Session</c> / <c>Find&lt;T&gt;</c> in tests.
/// </para>
/// <para>
/// Test Explorer integration is automatic — MSTest's <c>[TestClass]</c> / <c>[TestInitialize]</c> /
/// <c>[TestCleanup]</c> plus the Microsoft.Testing.Platform runner (enabled repo-wide in
/// <c>Directory.Build.props</c>) are everything Test Explorer and <c>dotnet test</c> need.
/// </para>
/// </remarks>
[TestClass]
public class UITestBase : IDisposable
{
    private readonly PowerToysModule scope;
    private SessionHelper? sessionHelper;
    private bool disposed;

    public required TestContext TestContext { get; set; }

    public Session Session { get; private set; } = null!;

    protected UITestBase(PowerToysModule scope = PowerToysModule.PowerToysSettings)
    {
        this.scope = scope;
    }

    [TestInitialize]
    public void TestInit()
    {
        sessionHelper = new SessionHelper(scope);
        Session = sessionHelper.Init();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        try
        {
            Session?.Cleanup();
        }
        catch
        {
        }

        Dispose();
    }

    /// <summary>Find an element on the session's window. Shortcut for <c>Session.Find&lt;T&gt;</c>.</summary>
    protected T Find<T>(By by, int timeoutMS = 5000)
        where T : Element, new() => Session.Find<T>(by, timeoutMS);

    /// <summary>Find an element by Name. Shortcut for <c>Session.Find&lt;T&gt;(By.Name(name))</c>.</summary>
    protected T Find<T>(string name, int timeoutMS = 5000)
        where T : Element, new() => Session.Find<T>(By.Name(name), timeoutMS);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        GC.SuppressFinalize(this);
    }
}
