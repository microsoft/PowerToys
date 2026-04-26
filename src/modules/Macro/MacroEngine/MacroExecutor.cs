// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroExecutor
{
    private readonly ISendInputHelper _input;

    internal MacroExecutor(ISendInputHelper input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
    }

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(macro);
        foreach (var step in macro.Steps)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteStepAsync(step, ct);
        }
    }

    private Task ExecuteStepAsync(MacroStep step, CancellationToken ct) =>
        step.Type switch
        {
            StepType.PressKey => ExecutePressKey(step),
            StepType.TypeText => ExecuteTypeText(step),
            StepType.Wait => ExecuteWait(step, ct),
            StepType.Repeat => ExecuteRepeat(step, ct),
            _ => throw new InvalidOperationException($"Unknown step type: {step.Type}"),
        };

    private Task ExecutePressKey(MacroStep step)
    {
        _input.PressKeyCombo(step.Key
            ?? throw new InvalidOperationException("PressKey step missing Key."));
        return Task.CompletedTask;
    }

    private Task ExecuteTypeText(MacroStep step)
    {
        _input.TypeText(step.Text
            ?? throw new InvalidOperationException("TypeText step missing Text."));
        return Task.CompletedTask;
    }

    private static Task ExecuteWait(MacroStep step, CancellationToken ct) =>
        Task.Delay(step.Ms ?? throw new InvalidOperationException("Wait step missing Ms."), ct);

    private async Task ExecuteRepeat(MacroStep step, CancellationToken ct)
    {
        int count = step.Count
            ?? throw new InvalidOperationException("Repeat step missing Count.");
        var subSteps = step.Steps
            ?? throw new InvalidOperationException("Repeat step missing Steps.");
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var sub in subSteps)
            {
                ct.ThrowIfCancellationRequested();
                await ExecuteStepAsync(sub, ct);
            }
        }
    }
}
