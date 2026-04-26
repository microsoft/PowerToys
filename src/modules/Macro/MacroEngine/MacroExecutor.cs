// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroExecutor(ISendInputHelper input)
{
    private readonly ISendInputHelper _input = input;

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken ct)
    {
        foreach (var step in macro.Steps)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteStepAsync(step, ct);
        }
    }

    private async Task ExecuteStepAsync(MacroStep step, CancellationToken ct)
    {
        switch (step.Type)
        {
            case StepType.PressKey:
                _input.PressKeyCombo(step.Key
                    ?? throw new InvalidOperationException("PressKey step missing Key."));
                break;

            case StepType.TypeText:
                _input.TypeText(step.Text
                    ?? throw new InvalidOperationException("TypeText step missing Text."));
                break;

            case StepType.Wait:
                await Task.Delay(step.Ms
                    ?? throw new InvalidOperationException("Wait step missing Ms."), ct);
                break;

            case StepType.Repeat:
                int count = step.Count
                    ?? throw new InvalidOperationException("Repeat step missing Count.");
                var subSteps = step.Steps
                    ?? throw new InvalidOperationException("Repeat step missing Steps.");
                for (int i = 0; i < count; i++)
                {
                    foreach (var sub in subSteps)
                    {
                        ct.ThrowIfCancellationRequested();
                        await ExecuteStepAsync(sub, ct);
                    }
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown step type: {step.Type}");
        }
    }
}
