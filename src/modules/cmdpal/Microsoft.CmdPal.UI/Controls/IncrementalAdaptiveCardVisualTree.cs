// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Projects the public, renderer-created logical XAML tree into the copied neutral planner. Templated
/// control internals are intentionally excluded because they differ between attached and detached trees.
/// </summary>
internal static class IncrementalAdaptiveCardVisualTree
{
    private static readonly TimeSpan SvgDecodeTimeout = TimeSpan.FromSeconds(3);
    private const string CardSemanticsProperty = "CardSemantics";
    private const string ImageSourceProperty = "ImageSource";
    private const string TextProperty = "Text";

    public static IncrementalNodeSnapshot Build(FrameworkElement root, AdaptiveCard card)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(card);

        var cardJson = card.ToJson().Stringify();
        var mappedImageTargets = new HashSet<Image>();
        var snapshot = BuildNode(root, "$", mappedImageTargets);
        var authoredInlineSvgCount = AdaptiveCardSemanticFingerprint.CountInlineSvgImages(cardJson);
        var allInlineSvgsMapped = authoredInlineSvgCount == mappedImageTargets.Count;
        var fingerprint = AdaptiveCardSemanticFingerprint.Create(cardJson, allInlineSvgsMapped);

        var rootProperties = new List<IncrementalPropertySnapshot>(snapshot.Properties.Count + 1)
        {
            new(
                CardSemanticsProperty,
                IncrementalValue.FromString(fingerprint),
                IncrementalPropertyBehavior.ReplaceRoot),
        };
        foreach (var property in snapshot.Properties)
        {
            rootProperties.Add(property);
        }

        return new IncrementalNodeSnapshot(
            snapshot.Path,
            snapshot.Type,
            snapshot.StableId,
            rootProperties,
            snapshot.Children);
    }

    public static async Task<bool> TryApplyAsync(
        FrameworkElement currentRoot,
        IncrementalUpdatePlan plan,
        long currentVersion,
        Func<bool> canCommit)
    {
        if (plan.Disposition == IncrementalPlanDisposition.NoChanges)
        {
            return plan.ExpectedVersion == currentVersion && canCommit();
        }

        if (plan.Disposition != IncrementalPlanDisposition.PatchInPlace
            || plan.ExpectedVersion != currentVersion)
        {
            return false;
        }

        if (!TryCollectValidatedNodes(currentRoot, plan, validateText: true, out var nodes))
        {
            return false;
        }

        // Text is independent of asynchronous image decoding. Apply it immediately so a slow or
        // broken decoder can never freeze live metric text behind the image pipeline.
        foreach (var update in plan.PropertyUpdates)
        {
            if (string.Equals(update.PropertyName, TextProperty, StringComparison.Ordinal))
            {
                var textBlock = (TextBlock)nodes[update.NodePath];
                textBlock.Text = update.NewValue.GetString() ?? string.Empty;
            }
        }

        var preparedImages = new Dictionary<string, PreparedImageSource>(StringComparer.Ordinal);
        foreach (var update in plan.PropertyUpdates)
        {
            if (!string.Equals(update.PropertyName, ImageSourceProperty, StringComparison.Ordinal))
            {
                continue;
            }

            var imageNode = nodes[update.NodePath];
            if (!TryGetInlineSvgTarget(imageNode, out var imageTarget))
            {
                return false;
            }

            var source = await LoadInlineSvgAsync(
                update.NewValue.GetString()!,
                imageTarget.Source as SvgImageSource);
            if (!canCommit())
            {
                return false;
            }

            if (source is not null)
            {
                preparedImages.Add(update.NodePath, new PreparedImageSource(imageTarget, source));
            }
        }

        if (!canCommit())
        {
            return false;
        }

        // The visible tree may have changed while an SVG was decoding. Revalidate image targets
        // before committing ready sources; text was already committed independently above.
        if (!TryCollectValidatedNodes(currentRoot, plan, validateText: false, out nodes))
        {
            return true;
        }

        foreach (var update in plan.PropertyUpdates)
        {
            if (string.Equals(update.PropertyName, ImageSourceProperty, StringComparison.Ordinal)
                && preparedImages.TryGetValue(update.NodePath, out var prepared))
            {
                prepared.Image.Source = prepared.Source;
            }
        }

        // A failed image retains its last good pixels. Returning success advances the logical
        // snapshot so text keeps flowing; the next live image URL will attempt another handoff.
        return true;
    }

    private static bool TryCollectValidatedNodes(
        FrameworkElement currentRoot,
        IncrementalUpdatePlan plan,
        bool validateText,
        out Dictionary<string, DependencyObject> nodes)
    {
        nodes = new Dictionary<string, DependencyObject>(StringComparer.Ordinal);
        CollectNodes(currentRoot, "$", nodes);

        // Validate every operation before changing the visible tree so a stale or unexpected node
        // cannot leave a partially applied card.
        foreach (var update in plan.PropertyUpdates)
        {
            if (!validateText && string.Equals(update.PropertyName, TextProperty, StringComparison.Ordinal))
            {
                continue;
            }

            if (!nodes.TryGetValue(update.NodePath, out var node)
                || !string.Equals(NodeType(node), update.ExpectedNodeType, StringComparison.Ordinal)
                || !CanApply(node, update))
            {
                return false;
            }
        }

        return true;
    }

    private static IncrementalNodeSnapshot BuildNode(
        DependencyObject node,
        string path,
        HashSet<Image> mappedImageTargets)
    {
        var properties = new List<IncrementalPropertySnapshot>();
        if (node is TextBlock textBlock)
        {
            properties.Add(new IncrementalPropertySnapshot(
                TextProperty,
                IncrementalValue.FromString(textBlock.Text),
                IncrementalPropertyBehavior.PatchInPlace));
        }
        else if (TryMapInlineSvg(node, mappedImageTargets, out var image, out var imageResource))
        {
            properties.Add(new IncrementalPropertySnapshot(
                ImageSourceProperty,
                IncrementalValue.FromString(imageResource),
                IncrementalPropertyBehavior.PatchInPlace));
        }

        var logicalChildren = GetLogicalChildren(node);
        var children = new List<IncrementalNodeSnapshot>(logicalChildren.Count);
        for (var i = 0; i < logicalChildren.Count; i++)
        {
            children.Add(BuildNode(logicalChildren[i], $"{path}/{i}", mappedImageTargets));
        }

        return new IncrementalNodeSnapshot(path, NodeType(node), null, properties, children);
    }

    private static void CollectNodes(
        DependencyObject node,
        string path,
        Dictionary<string, DependencyObject> nodes)
    {
        nodes.Add(path, node);
        var children = GetLogicalChildren(node);
        for (var i = 0; i < children.Count; i++)
        {
            CollectNodes(children[i], $"{path}/{i}", nodes);
        }
    }

    private static List<DependencyObject> GetLogicalChildren(DependencyObject node)
    {
        var children = new List<DependencyObject>();
        switch (node)
        {
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    children.Add(child);
                }

                break;
            case Border border when border.Child is not null:
                children.Add(border.Child);
                break;
            case Viewbox viewbox when viewbox.Child is not null:
                children.Add(viewbox.Child);
                break;
            case ContentControl contentControl when contentControl.Content is DependencyObject content:
                children.Add(content);
                break;
            case ContentPresenter contentPresenter when contentPresenter.Content is DependencyObject content:
                children.Add(content);
                break;
        }

        return children;
    }

    private static bool CanApply(DependencyObject node, IncrementalPropertyUpdate update)
    {
        if (update.ExpectedOldValue.Kind != IncrementalValueKind.String
            || update.NewValue.Kind != IncrementalValueKind.String)
        {
            return false;
        }

        if (node is TextBlock textBlock
            && string.Equals(update.PropertyName, TextProperty, StringComparison.Ordinal))
        {
            return string.Equals(textBlock.Text, update.ExpectedOldValue.GetString(), StringComparison.Ordinal);
        }

        // Image resource state is already versioned by the immutable current snapshot and updates are
        // serialized by the presentation session. Re-resolve the target from renderer metadata rather
        // than keying state by WinRT RCW identity, which is not stable across tree traversals.
        return string.Equals(update.PropertyName, ImageSourceProperty, StringComparison.Ordinal)
            && TryGetInlineSvgTarget(node, out _);
    }

    private static bool TryMapInlineSvg(
        DependencyObject node,
        HashSet<Image> mappedImageTargets,
        out Image image,
        out string resource)
    {
        image = null!;
        resource = string.Empty;
        if (node is not FrameworkElement frameworkElement
            || frameworkElement.Tag is not ElementTagContent tag
            || tag.CardElement is not AdaptiveImage adaptiveImage
            || !IsInlineSvg(adaptiveImage.Url)
            || !TryFindSingleImage(node, out image)
            || !mappedImageTargets.Add(image))
        {
            return false;
        }

        resource = adaptiveImage.Url;
        return true;
    }

    private static bool TryFindSingleImage(DependencyObject root, out Image image)
    {
        image = null!;
        var images = new List<Image>(2);
        CollectImageTargets(root, root, images);
        if (images.Count != 1)
        {
            return false;
        }

        image = images[0];
        return true;
    }

    private static void CollectImageTargets(
        DependencyObject root,
        DependencyObject node,
        List<Image> images)
    {
        if (node is Image image)
        {
            images.Add(image);
            return;
        }

        if (!ReferenceEquals(root, node)
            && node is FrameworkElement frameworkElement
            && frameworkElement.Tag is ElementTagContent)
        {
            return;
        }

        var children = GetLogicalChildren(node);
        for (var i = 0; i < children.Count && images.Count < 2; i++)
        {
            CollectImageTargets(root, children[i], images);
        }
    }

    private static bool IsInlineSvg(string? resource)
    {
        return resource?.StartsWith("data:image/svg+xml", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TryGetInlineSvgTarget(DependencyObject node, out Image image)
    {
        image = null!;
        return node is FrameworkElement frameworkElement
            && frameworkElement.Tag is ElementTagContent tag
            && tag.CardElement is AdaptiveImage adaptiveImage
            && IsInlineSvg(adaptiveImage.Url)
            && TryFindSingleImage(node, out image);
    }

    private static async Task<SvgImageSource?> LoadInlineSvgAsync(
        string resource,
        SvgImageSource? currentSource)
    {
        try
        {
            var commaIndex = resource.IndexOf(',');
            if (commaIndex <= 0)
            {
                return null;
            }

            var metadata = resource[..commaIndex];
            var payload = resource[(commaIndex + 1)..];
            byte[] bytes;
            if (metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromBase64String(payload);
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            }

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            stream.Seek(0);

            var source = new SvgImageSource();
            if (currentSource is not null)
            {
                source.RasterizePixelWidth = currentSource.RasterizePixelWidth;
                source.RasterizePixelHeight = currentSource.RasterizePixelHeight;
            }

            using var timeout = new CancellationTokenSource(SvgDecodeTimeout);
            var status = await source.SetSourceAsync(stream).AsTask(timeout.Token);
            if (status != SvgImageSourceLoadStatus.Success)
            {
                return null;
            }

            return source;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string NodeType(DependencyObject node) => node.GetType().FullName ?? node.GetType().Name;

    private sealed class PreparedImageSource(
        Image image,
        SvgImageSource source)
    {
        public Image Image { get; } = image;

        public SvgImageSource Source { get; } = source;
    }
}
