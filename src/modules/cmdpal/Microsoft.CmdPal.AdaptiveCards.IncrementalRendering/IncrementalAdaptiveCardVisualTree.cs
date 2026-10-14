// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

internal static class IncrementalAdaptiveCardVisualTree
{
    private static readonly TimeSpan ImageLoadTimeout = TimeSpan.FromSeconds(3);
    private const string CardSemanticsProperty = "CardSemantics";
    private const string ImageSourceProperty = "ImageSource";
    private const string TextProperty = "Text";

    public static IncrementalTreeSnapshot Build(FrameworkElement root, string cardJson)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);

        var mappedTextTargets = new HashSet<TextBlock>();
        var mappedImageTargets = new HashSet<Image>();
        var nodes = new List<IncrementalNodeSnapshot>();
        BuildNode(root, nodes, mappedTextTargets, mappedImageTargets);

        var rootNode = nodes[0];
        var rootProperties = new List<IncrementalPropertySnapshot>(rootNode.Properties.Count + 1)
        {
            new(
                CardSemanticsProperty,
                AdaptiveCardSemanticFingerprint.Create(
                    cardJson,
                    mappedTextTargets.Count,
                    mappedImageTargets.Count),
                IncrementalPropertyBehavior.ReplaceRoot),
        };
        foreach (var property in rootNode.Properties)
        {
            rootProperties.Add(property);
        }

        nodes[0] = new IncrementalNodeSnapshot(
            rootNode.Type,
            rootNode.ChildCount,
            rootProperties);
        return new IncrementalTreeSnapshot(nodes);
    }

    public static async Task<bool> TryApplyAsync(
        FrameworkElement currentRoot,
        FrameworkElement candidateRoot,
        IncrementalUpdatePlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Disposition == IncrementalPlanDisposition.NoChanges)
        {
            return true;
        }

        if (plan.Disposition != IncrementalPlanDisposition.PatchInPlace)
        {
            return false;
        }

        var currentNodes = new List<DependencyObject>();
        var candidateNodes = new List<DependencyObject>();
        CollectNodes(currentRoot, currentNodes);
        CollectNodes(candidateRoot, candidateNodes);
        if (currentNodes.Count != candidateNodes.Count)
        {
            return false;
        }

        foreach (var update in plan.PropertyUpdates)
        {
            if (!TryGetNode(currentNodes, update, out var currentNode)
                || !TryGetNode(candidateNodes, update, out var candidateNode)
                || !HasExpectedValue(
                    currentNode,
                    update.PropertyName,
                    update.ExpectedOldValue,
                    validateImageValue: false)
                || !HasExpectedValue(
                    candidateNode,
                    update.PropertyName,
                    update.NewValue,
                    validateImageValue: true))
            {
                return false;
            }
        }

        var imageUpdates = new List<ImageUpdate>();
        foreach (var update in plan.PropertyUpdates)
        {
            if (string.Equals(update.PropertyName, ImageSourceProperty, StringComparison.Ordinal))
            {
                TryGetInlineSvgTarget(
                    candidateNodes[update.NodeIndex],
                    out var candidateImage,
                    out var resource);
                imageUpdates.Add(new ImageUpdate(
                    update.NodeIndex,
                    candidateImage,
                    resource));
            }
        }

        var preparedImages = new Dictionary<int, SvgImageSource>();
        if (imageUpdates.Count > 0)
        {
            using var imageLoadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            imageLoadCancellation.CancelAfter(ImageLoadTimeout);
            var imageLoadTasks = new Task<PreparedImage?>[imageUpdates.Count];
            for (var i = 0; i < imageUpdates.Count; i++)
            {
                imageLoadTasks[i] = PrepareImageAsync(
                    imageUpdates[i],
                    imageLoadCancellation.Token);
            }

            PreparedImage?[] imageLoadResults;
            try
            {
                imageLoadResults = await Task.WhenAll(imageLoadTasks);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            foreach (var preparedImage in imageLoadResults)
            {
                if (preparedImage is null)
                {
                    return false;
                }

                preparedImages.Add(
                    preparedImage.NodeIndex,
                    preparedImage.Source);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var update in plan.PropertyUpdates)
        {
            var currentNode = currentNodes[update.NodeIndex];
            var candidateNode = candidateNodes[update.NodeIndex];
            if (string.Equals(update.PropertyName, TextProperty, StringComparison.Ordinal))
            {
                TryGetPlainTextTarget(currentNode, out var currentText);
                TryGetPlainTextTarget(candidateNode, out var candidateText);
                ApplyPlainText(currentText, candidateText);
            }
            else
            {
                TryGetInlineSvgTarget(currentNode, out var currentImage, out _);
                TryGetInlineSvgTarget(candidateNode, out var candidateImage, out _);
                currentImage.Source = preparedImages[update.NodeIndex];
                currentImage.Width = candidateImage.Width;
                currentImage.Height = candidateImage.Height;
                currentImage.MaxWidth = candidateImage.MaxWidth;
                currentImage.MaxHeight = candidateImage.MaxHeight;
                currentImage.Visibility = candidateImage.Visibility;
            }
        }

        return true;
    }

    private static void BuildNode(
        DependencyObject node,
        List<IncrementalNodeSnapshot> nodes,
        HashSet<TextBlock> mappedTextTargets,
        HashSet<Image> mappedImageTargets)
    {
        var properties = new List<IncrementalPropertySnapshot>(1);
        if (TryMapPlainText(node, mappedTextTargets, out var textBlock))
        {
            properties.Add(new IncrementalPropertySnapshot(
                TextProperty,
                textBlock.Text,
                IncrementalPropertyBehavior.PatchInPlace));
        }
        else if (TryMapInlineSvg(node, mappedImageTargets, out _, out var imageResource))
        {
            properties.Add(new IncrementalPropertySnapshot(
                ImageSourceProperty,
                imageResource,
                IncrementalPropertyBehavior.PatchInPlace));
        }

        var childCount = GetLogicalChildCount(node);
        nodes.Add(new IncrementalNodeSnapshot(NodeType(node), childCount, properties));
        for (var i = 0; i < childCount; i++)
        {
            BuildNode(GetLogicalChild(node, i), nodes, mappedTextTargets, mappedImageTargets);
        }
    }

    private static void CollectNodes(
        DependencyObject node,
        List<DependencyObject> nodes)
    {
        nodes.Add(node);
        var childCount = GetLogicalChildCount(node);
        for (var i = 0; i < childCount; i++)
        {
            CollectNodes(GetLogicalChild(node, i), nodes);
        }
    }

    private static bool TryGetNode(
        IReadOnlyList<DependencyObject> nodes,
        IncrementalPropertyUpdate update,
        out DependencyObject node)
    {
        if (update.NodeIndex >= 0 && update.NodeIndex < nodes.Count)
        {
            node = nodes[update.NodeIndex];
            return string.Equals(NodeType(node), update.ExpectedNodeType, StringComparison.Ordinal);
        }

        node = null!;
        return false;
    }

    private static bool HasExpectedValue(
        DependencyObject node,
        string propertyName,
        string? expectedValue,
        bool validateImageValue)
    {
        if (string.Equals(propertyName, TextProperty, StringComparison.Ordinal)
            && TryGetPlainTextTarget(node, out var textBlock))
        {
            return string.Equals(textBlock.Text, expectedValue, StringComparison.Ordinal);
        }

        return string.Equals(propertyName, ImageSourceProperty, StringComparison.Ordinal)
            && TryGetInlineSvgTarget(node, out _, out var imageResource)
            && (!validateImageValue
                || string.Equals(imageResource, expectedValue, StringComparison.Ordinal));
    }

    private static bool TryMapPlainText(
        DependencyObject node,
        HashSet<TextBlock> mappedTargets,
        out TextBlock textBlock)
    {
        textBlock = null!;
        return node is FrameworkElement frameworkElement
            && frameworkElement.Tag is ElementTagContent tag
            && tag.CardElement is AdaptiveTextBlock
            && TryFindSingleTarget(node, out textBlock)
            && IsPlainText(textBlock)
            && mappedTargets.Add(textBlock);
    }

    private static bool TryGetPlainTextTarget(
        DependencyObject node,
        out TextBlock textBlock)
    {
        textBlock = null!;
        return node is FrameworkElement frameworkElement
            && frameworkElement.Tag is ElementTagContent tag
            && tag.CardElement is AdaptiveTextBlock
            && TryFindSingleTarget(node, out textBlock)
            && IsPlainText(textBlock);
    }

    private static bool IsPlainText(TextBlock textBlock)
    {
        if (textBlock.Inlines.Count == 0)
        {
            return true;
        }

        return textBlock.Inlines.Count == 1
            && textBlock.Inlines[0] is Run run
            && run.FontWeight.Weight == textBlock.FontWeight.Weight
            && run.FontStyle == textBlock.FontStyle
            && run.FontStretch == textBlock.FontStretch
            && run.FontSize == textBlock.FontSize
            && string.Equals(run.FontFamily.Source, textBlock.FontFamily.Source, StringComparison.Ordinal)
            && run.CharacterSpacing == textBlock.CharacterSpacing
            && run.TextDecorations == textBlock.TextDecorations;
    }

    private static void ApplyPlainText(TextBlock current, TextBlock candidate)
    {
        if (current.Inlines.Count == 1
            && candidate.Inlines.Count == 1
            && current.Inlines[0] is Run currentRun
            && candidate.Inlines[0] is Run candidateRun)
        {
            currentRun.Text = candidateRun.Text;
            return;
        }

        current.Text = candidate.Text;
    }

    private static async Task<PreparedImage?> PrepareImageAsync(
        ImageUpdate update,
        CancellationToken cancellationToken)
    {
        var commaIndex = update.Resource.IndexOf(',');
        if (commaIndex <= 0)
        {
            return null;
        }

        var metadata = update.Resource[..commaIndex];
        var payload = update.Resource[(commaIndex + 1)..];
        byte[] bytes;
        try
        {
            bytes = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(payload)
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        }
        catch (FormatException)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask(cancellationToken);
            writer.DetachStream();
        }

        stream.Seek(0);
        var source = new SvgImageSource();
        if (update.CandidateImage.Source is SvgImageSource candidateSource)
        {
            source.RasterizePixelWidth = candidateSource.RasterizePixelWidth;
            source.RasterizePixelHeight = candidateSource.RasterizePixelHeight;
        }

        try
        {
            var status = await source.SetSourceAsync(stream).AsTask(cancellationToken);
            return status == SvgImageSourceLoadStatus.Success
                ? new PreparedImage(update.NodeIndex, source)
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool TryMapInlineSvg(
        DependencyObject node,
        HashSet<Image> mappedTargets,
        out Image image,
        out string resource)
    {
        if (TryGetInlineSvgTarget(node, out image, out resource)
            && mappedTargets.Add(image))
        {
            return true;
        }

        image = null!;
        resource = string.Empty;
        return false;
    }

    private static bool TryGetInlineSvgTarget(
        DependencyObject node,
        out Image image,
        out string resource)
    {
        image = null!;
        resource = string.Empty;
        if (node is not FrameworkElement frameworkElement
            || frameworkElement.Tag is not ElementTagContent tag
            || tag.CardElement is not AdaptiveImage adaptiveImage
            || !IsInlineSvg(adaptiveImage.Url)
            || !TryFindSingleTarget(node, out image))
        {
            return false;
        }

        resource = adaptiveImage.Url;
        return true;
    }

    private static bool TryFindSingleTarget<T>(
        DependencyObject root,
        out T target)
        where T : DependencyObject
    {
        T? found = null;
        var foundMultiple = false;
        CollectTargets(root, root, ref found, ref foundMultiple);
        target = found!;
        return found is not null && !foundMultiple;
    }

    private static void CollectTargets<T>(
        DependencyObject root,
        DependencyObject node,
        ref T? found,
        ref bool foundMultiple)
        where T : DependencyObject
    {
        if (node is T target)
        {
            if (found is not null)
            {
                foundMultiple = true;
            }
            else
            {
                found = target;
            }

            return;
        }

        if (foundMultiple
            || (!ReferenceEquals(root, node)
                && node is FrameworkElement { Tag: ElementTagContent }))
        {
            return;
        }

        var childCount = GetLogicalChildCount(node);
        for (var i = 0; i < childCount; i++)
        {
            CollectTargets(root, GetLogicalChild(node, i), ref found, ref foundMultiple);
        }
    }

    private static int GetLogicalChildCount(DependencyObject node)
    {
        if (node is Panel panel)
        {
            return panel.Children.Count;
        }

        return GetSingleLogicalChild(node) is null ? 0 : 1;
    }

    private static DependencyObject GetLogicalChild(DependencyObject node, int index)
    {
        if (node is Panel panel)
        {
            return panel.Children[index];
        }

        var child = GetSingleLogicalChild(node);
        if (index == 0 && child is not null)
        {
            return child;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static DependencyObject? GetSingleLogicalChild(DependencyObject node) => node switch
    {
        Border { Child: not null } border => border.Child,
        Viewbox { Child: not null } viewbox => viewbox.Child,
        ContentControl { Content: DependencyObject content } => content,
        ContentPresenter { Content: DependencyObject content } => content,
        _ => null,
    };

    private static bool IsInlineSvg(string? resource)
    {
        return resource?.StartsWith("data:image/svg+xml,", StringComparison.OrdinalIgnoreCase) == true
            || resource?.StartsWith("data:image/svg+xml;", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string NodeType(DependencyObject node) => node.GetType().FullName ?? node.GetType().Name;

    private sealed record ImageUpdate(
        int NodeIndex,
        Image CandidateImage,
        string Resource);

    private sealed record PreparedImage(
        int NodeIndex,
        SvgImageSource Source);
}
