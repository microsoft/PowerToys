// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class MacroExecutorTests
{
    private static readonly string[] SingleCtrlC = ["Ctrl+C"];
    private static readonly string[] SingleHello = ["Hello"];

    private FakeSendInputHelper _input = null!;
    private MacroExecutor _executor = null!;

    [TestInitialize]
    public void Init()
    {
        _input = new FakeSendInputHelper();
        _executor = new MacroExecutor(_input);
    }

    [TestMethod]
    public async Task ExecuteAsync_PressKey_CallsSendInput()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" }],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        CollectionAssert.AreEqual(SingleCtrlC, _input.KeyCombos);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeText_CallsSendInput()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.TypeText, Text = "Hello" }],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        CollectionAssert.AreEqual(SingleHello, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteAsync_Repeat_RunsSubStepsNTimes()
    {
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        Assert.AreEqual(3, _input.KeyCombos.Count);
        Assert.IsTrue(_input.KeyCombos.All(k => k == "Tab"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationToken_StopsExecution()
    {
        using var cts = new CancellationTokenSource();
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep { Type = StepType.PressKey, Key = "A" },
                new MacroStep { Type = StepType.Wait, Ms = 10000 },
                new MacroStep { Type = StepType.PressKey, Key = "B" },
            ],
        };
        cts.CancelAfter(50);
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(
            () => _executor.ExecuteAsync(macro, cts.Token));
        Assert.AreEqual(1, _input.KeyCombos.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_PressKey_MissingKey_Throws()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.PressKey }],
        };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(macro, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeText_MissingText_Throws()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.TypeText }],
        };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(macro, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_Wait_MissingMs_Throws()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.Wait }],
        };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(macro, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_NestedRepeat_RunsCorrectly()
    {
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 2,
                    Steps =
                    [
                        new MacroStep
                        {
                            Type = StepType.Repeat,
                            Count = 3,
                            Steps = [new MacroStep { Type = StepType.PressKey, Key = "X" }],
                        },
                    ],
                },
            ],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        Assert.AreEqual(6, _input.KeyCombos.Count); // 2 × 3
        Assert.IsTrue(_input.KeyCombos.All(k => k == "X"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationToken_StopsExecution_Deterministic()
    {
        using var cts = new CancellationTokenSource();
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep { Type = StepType.PressKey, Key = "A" },
                new MacroStep { Type = StepType.Wait, Ms = int.MaxValue },
                new MacroStep { Type = StepType.PressKey, Key = "B" },
            ],
        };
        var task = _executor.ExecuteAsync(macro, cts.Token);

        // Let "A" execute (synchronous, completes immediately), then cancel before Wait finishes
        await Task.Yield();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => task);
        Assert.AreEqual(1, _input.KeyCombos.Count);
    }
}
