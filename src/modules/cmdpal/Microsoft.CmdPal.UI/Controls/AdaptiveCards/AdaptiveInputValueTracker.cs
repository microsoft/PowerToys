// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

/// <summary>
/// Keeps the input values of each rendered card. The renderer only exposes current values
/// through validation, which shows errors and moves focus.
/// </summary>
internal sealed partial class AdaptiveInputValueTracker
{
    private static readonly string[] BuiltInInputTypes =
    [
        "Input.ChoiceSet",
        "Input.Date",
        "Input.Number",
        "Input.Text",
        "Input.Time",
        "Input.Toggle",
    ];

    private readonly ConditionalWeakTable<AdaptiveInputs, List<TrackedInput>> _inputs = new();

    /// <summary>Wraps the renderer's input renderers. Register custom input renderers first.</summary>
    public void Attach(AdaptiveCardRenderer renderer, IEnumerable<string> customInputTypes)
    {
        foreach (var type in BuiltInInputTypes.Concat(customInputTypes))
        {
            if (renderer.ElementRenderers.Get(type) is IAdaptiveElementRenderer inner)
            {
                renderer.ElementRenderers.Set(type, new TrackingRenderer(inner, this));
            }
        }
    }

    /// <summary>Returns the current value of each input in <paramref name="card"/>, by input ID.</summary>
    public Dictionary<string, string> GetCurrentValues(RenderedAdaptiveCard? card)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (card?.UserInputs is AdaptiveInputs userInputs && _inputs.TryGetValue(userInputs, out var inputs))
        {
            foreach (var input in inputs)
            {
                values[input.Id] = input.Value.CurrentValue ?? string.Empty;
            }
        }

        return values;
    }

    private void Track(AdaptiveInputs userInputs, string id, IAdaptiveInputValue value) =>
        _inputs.GetOrCreateValue(userInputs).Add(new TrackedInput(id, value));

    private sealed record TrackedInput(string Id, IAdaptiveInputValue Value);

    private sealed partial class TrackingRenderer(IAdaptiveElementRenderer inner, AdaptiveInputValueTracker tracker) : IAdaptiveElementRenderer
    {
        public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
        {
            var rendered = inner.Render(element, context, renderArgs);
            if (rendered is not null
                && !string.IsNullOrEmpty(element.Id)
                && element is IAdaptiveInputElement input
                && context.GetInputValue(input) is IAdaptiveInputValue value)
            {
                tracker.Track(context.UserInputs, element.Id, value);
            }

            return rendered!;
        }
    }
}
