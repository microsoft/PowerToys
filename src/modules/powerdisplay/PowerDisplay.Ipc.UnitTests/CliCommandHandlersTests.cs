// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Invariant guards for the <see cref="CliCommandHandlers"/> registry that replaced the dispatcher's
/// per-command switch. End-to-end command behavior is covered by <see cref="CliRequestDispatcherTests"/>;
/// these pin the registry's shape so a new command cannot be added without a handler, and unknown
/// names cannot silently resolve.
/// </summary>
[TestClass]
public class CliCommandHandlersTests
{
    [TestMethod]
    public void Registry_ResolvesAHandlerForEveryCommandName()
    {
        // Reflection over CliCommandNames means adding a command constant without registering a
        // handler fails here — the guard the old switch's default arm used to provide implicitly.
        var commandNames = typeof(CliCommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.AreNotEqual(0, commandNames.Length, "expected CliCommandNames to expose command constants");

        foreach (var command in commandNames)
        {
            Assert.IsTrue(
                CliCommandHandlers.TryGet(command, out var handler),
                $"no handler registered for command '{command}'");
            Assert.IsNotNull(handler, $"handler for '{command}' must not be null");
        }
    }

    [TestMethod]
    public void Registry_UnknownCommand_ReturnsFalseAndNullHandler()
    {
        var found = CliCommandHandlers.TryGet("does-not-exist", out var handler);

        Assert.IsFalse(found, "an unrecognized command name must not resolve to a handler");
        Assert.IsNull(handler);
    }

    [TestMethod]
    public void Registry_EmptyCommand_ReturnsFalse()
    {
        Assert.IsFalse(CliCommandHandlers.TryGet(string.Empty, out _));
    }

    [TestMethod]
    public void Registry_LookupIsOrdinalCaseSensitive()
    {
        // The registry is built with StringComparer.Ordinal to match the canonical constants exactly,
        // so a case variant of a real command must not resolve.
        Assert.IsFalse(CliCommandHandlers.TryGet("LIST", out _), "lookup must be case-sensitive (ordinal)");
    }

    [TestMethod]
    public void Registry_UpAndDown_ShareTheSameHandlerImplementation()
    {
        // up and down are both relative-adjust commands that differ only by direction, which the
        // handler derives from the envelope command name — so they route to the same implementation.
        Assert.IsTrue(CliCommandHandlers.TryGet(CliCommandNames.Up, out var up));
        Assert.IsTrue(CliCommandHandlers.TryGet(CliCommandNames.Down, out var down));

        Assert.AreEqual(up!.GetType(), down!.GetType());
    }
}
