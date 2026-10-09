// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

/// <summary>
/// Wraps the built-in container renderers to support <c>roundedCorners</c> and <c>showBorder</c>,
/// which the WinUI 3 renderer ignores, with theme-aware styles from the override styles.
/// </summary>
internal sealed partial class AdaptiveContainerDecoratorRenderer : IAdaptiveElementRenderer
{
    internal const string RoundedStyleKey = "CmdPal.Adaptive.Container.Rounded";
    internal const string BorderedStyleKey = "CmdPal.Adaptive.Container.Bordered";
    internal const string RoundedBorderedStyleKey = "CmdPal.Adaptive.Container.RoundedBordered";

    private const string RoundedCornersProperty = "roundedCorners";
    private const string ShowBorderProperty = "showBorder";

    private static readonly string[] DecoratedElementTypes = ["Container", "ColumnSet", "Column"];

    private readonly IAdaptiveElementRenderer _inner;

    private AdaptiveContainerDecoratorRenderer(IAdaptiveElementRenderer inner)
    {
        _inner = inner;
    }

    /// <summary>Wraps the container renderers registered on <paramref name="renderer"/>.</summary>
    public static void Register(AdaptiveCardRenderer renderer)
    {
        foreach (var elementType in DecoratedElementTypes)
        {
            var inner = renderer.ElementRenderers.Get(elementType) ?? CreateBuiltInRenderer(elementType);
            if (inner is not null and not AdaptiveContainerDecoratorRenderer)
            {
                renderer.ElementRenderers.Set(elementType, new AdaptiveContainerDecoratorRenderer(inner));
            }
        }
    }

    // Under native AOT, the casts to these types need their metadata.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Border))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Style))]
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var rendered = _inner.Render(element, context, renderArgs);

        var styleKey = GetStyleKey(element.AdditionalProperties);
        if (styleKey is not null
            && GetBackgroundBorder(rendered) is Border border
            && context.OverrideStyles is ResourceDictionary styles
            && styles.TryGetValue(styleKey, out var value)
            && value is Style style)
        {
            border.Style = style;
        }

        return rendered;
    }

    internal static string? GetStyleKey(JsonObject? properties)
    {
        var rounded = GetBoolean(properties, RoundedCornersProperty);
        var bordered = GetBoolean(properties, ShowBorderProperty);
        return (rounded, bordered) switch
        {
            (true, true) => RoundedBorderedStyleKey,
            (true, false) => RoundedStyleKey,
            (false, true) => BorderedStyleKey,
            _ => null,
        };
    }

    private static bool GetBoolean(JsonObject? properties, string name) =>
        properties is not null
            && properties.TryGetValue(name, out var value)
            && value.ValueType == JsonValueType.Boolean
            && value.GetBoolean();

    // Containers render as a Border. A selectAction wraps that Border in a button.
    private static Border? GetBackgroundBorder(UIElement rendered) => rendered switch
    {
        Border border => border,
        ContentControl { Content: Border border } => border,
        _ => null,
    };

    private static IAdaptiveElementRenderer? CreateBuiltInRenderer(string elementType) => elementType switch
    {
        "Container" => new AdaptiveContainerRenderer(),
        "ColumnSet" => new AdaptiveColumnSetRenderer(),
        "Column" => new AdaptiveColumnRenderer(),
        _ => null,
    };
}
