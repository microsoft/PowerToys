// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Helpers;
using Windows.Graphics;

namespace PowerDisplay.UnitTests;

[TestClass]
public class FlyoutPlacementSessionTests
{
    [TestMethod]
    public void WithoutActivation_InitializationCannotApplyLayout()
    {
        var session = new FlyoutPlacementSession();

        Assert.IsNull(session.Current);
        Assert.IsFalse(session.IsShowing);
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: false));
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: true));
    }

    [TestMethod]
    public void BeginShow_AllowsLayoutBeforeWindowBecomesVisible()
    {
        var session = new FlyoutPlacementSession();
        var context = Context(1, FlyoutPlacementEdge.Bottom);
        var previousGeneration = session.Generation;

        session.BeginShow(context);

        Assert.AreSame(context, session.Current);
        Assert.IsTrue(session.IsShowing);
        Assert.IsTrue(session.Generation > previousGeneration);
        Assert.IsTrue(session.CanApplyLayout(isWindowVisible: false));
    }

    [TestMethod]
    public void FinishShow_OnlyAllowsLayoutWhileWindowRemainsVisible()
    {
        var session = new FlyoutPlacementSession();
        var context = Context(1, FlyoutPlacementEdge.Bottom);
        session.BeginShow(context);
        var generation = session.Generation;

        session.FinishShow();

        Assert.AreSame(context, session.Current);
        Assert.IsFalse(session.IsShowing);
        Assert.AreEqual(generation, session.Generation);
        Assert.IsTrue(session.CanApplyLayout(isWindowVisible: true));
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: false));
    }

    [TestMethod]
    public void Clear_DisablesQueuedLayoutAndInvalidatesTheSession()
    {
        var session = new FlyoutPlacementSession();
        session.BeginShow(Context(1, FlyoutPlacementEdge.Bottom));
        var generation = session.Generation;

        session.Clear();

        Assert.IsNull(session.Current);
        Assert.IsFalse(session.IsShowing);
        Assert.IsTrue(session.Generation > generation);
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: false));
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: true));
    }

    [TestMethod]
    public void HideAndReopen_UsesTheNewContextAndGeneration()
    {
        var session = new FlyoutPlacementSession();
        session.BeginShow(Context(1, FlyoutPlacementEdge.Bottom));
        session.FinishShow();
        var queuedCallbackGeneration = session.Generation;
        session.Clear();
        var newContext = Context(2, FlyoutPlacementEdge.Top);

        session.BeginShow(newContext);

        Assert.AreSame(newContext, session.Current);
        Assert.AreNotEqual(queuedCallbackGeneration, session.Generation);
        Assert.IsTrue(session.CanApplyLayout(isWindowVisible: false));
        session.FinishShow();
        Assert.IsTrue(session.CanApplyLayout(isWindowVisible: true));
    }

    [TestMethod]
    public void FinishShow_AfterClear_DoesNotRestoreAnInvalidatedContext()
    {
        var session = new FlyoutPlacementSession();
        session.BeginShow(Context(1, FlyoutPlacementEdge.Bottom));
        session.Clear();
        var generation = session.Generation;

        session.FinishShow();

        Assert.IsNull(session.Current);
        Assert.IsFalse(session.IsShowing);
        Assert.AreEqual(generation, session.Generation);
        Assert.IsFalse(session.CanApplyLayout(isWindowVisible: true));
    }

    [TestMethod]
    public void BeginShow_NullContext_DoesNotOverwriteAnActiveSession()
    {
        var session = new FlyoutPlacementSession();
        var context = Context(1, FlyoutPlacementEdge.Bottom);
        session.BeginShow(context);
        var generation = session.Generation;

        Assert.ThrowsExactly<ArgumentNullException>(() => session.BeginShow(null!));

        Assert.AreSame(context, session.Current);
        Assert.IsTrue(session.IsShowing);
        Assert.AreEqual(generation, session.Generation);
    }

    private static FlyoutPlacementContext Context(ulong displayId, FlyoutPlacementEdge edge)
        => new(
            displayId,
            edge == FlyoutPlacementEdge.Bottom ? new RectInt32(700, 760, 40, 40) : new RectInt32(700, 0, 40, 40),
            new RectInt32(0, 0, 1000, 800),
            edge == FlyoutPlacementEdge.Bottom ? new RectInt32(0, 0, 1000, 760) : new RectInt32(0, 40, 1000, 760),
            1.0,
            edge);
}
