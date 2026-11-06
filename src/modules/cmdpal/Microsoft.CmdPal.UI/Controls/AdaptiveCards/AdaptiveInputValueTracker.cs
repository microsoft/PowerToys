// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    // Each render gets its own AdaptiveInputs. The projection can return a different wrapper for the
    // same native object, so entries are matched with Equals rather than by reference.
    private readonly List<RenderInputs> _renders = [];

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
        if (card?.UserInputs is AdaptiveInputs userInputs && Find(userInputs) is RenderInputs render)
        {
            foreach (var input in render.Inputs)
            {
                values[input.Id] = input.Value.CurrentValue ?? string.Empty;
            }
        }

        return values;
    }

    /// <summary>Forgets renders other than <paramref name="card"/> and the latest render, which may still be in progress.</summary>
    public void Retain(RenderedAdaptiveCard? card)
    {
        var current = card?.UserInputs is AdaptiveInputs userInputs ? Find(userInputs) : null;
        var latest = _renders.Count > 0 ? _renders[^1] : null;
        _renders.RemoveAll(render => render != current && render != latest);
    }

    private RenderInputs? Find(AdaptiveInputs userInputs) =>
        _renders.FindLast(render => ReferenceEquals(render.UserInputs, userInputs) || render.UserInputs.Equals(userInputs));

    private void Track(AdaptiveInputs userInputs, string id, IAdaptiveInputValue value)
    {
        var render = Find(userInputs);
        if (render is null)
        {
            render = new RenderInputs(userInputs);
            _renders.Add(render);
        }

        render.Inputs.Add(new TrackedInput(id, value));
    }

    private sealed record TrackedInput(string Id, IAdaptiveInputValue Value);

    private sealed class RenderInputs(AdaptiveInputs userInputs)
    {
        public AdaptiveInputs UserInputs { get; } = userInputs;

        public List<TrackedInput> Inputs { get; } = [];
    }

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
