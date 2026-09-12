// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using AdaptiveCards.Templating;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Renders Adaptive Cards and updates safe properties without replacing the visible card.
/// Unsupported changes replace the complete card.
/// </summary>
public sealed partial class IncrementalAdaptiveCardUpdater
{
    private readonly AdaptiveCardRenderer _renderer;
    private readonly Border _host;
    private readonly AdaptiveElementParserRegistration _elementParsers;
    private readonly AdaptiveActionParserRegistration _actionParsers;
    private readonly LatestWinsUpdateQueue<UpdateRequest> _updates;
    private Action? _cancelActiveUpdate;
    private IncrementalTreeSnapshot? _snapshot;

    public IncrementalAdaptiveCardUpdater(
        AdaptiveCardRenderer renderer,
        Border host,
        AdaptiveElementParserRegistration? elementParsers = null,
        AdaptiveActionParserRegistration? actionParsers = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(host);

        _renderer = renderer;
        _host = host;
        _elementParsers = elementParsers ?? new AdaptiveElementParserRegistration();
        _actionParsers = actionParsers ?? new AdaptiveActionParserRegistration();
        _updates = new LatestWinsUpdateQueue<UpdateRequest>(ProcessUpdateAsync);
    }

    /// <summary>Gets the retained rendered card.</summary>
    public RenderedAdaptiveCard? RenderedCard { get; private set; }

    /// <summary>Gets the card model that belongs to <see cref="RenderedCard"/>.</summary>
    public AdaptiveCard? Card { get; private set; }

    /// <summary>
    /// Expands a card template with the specified data, then updates the visible card.
    /// </summary>
    public Task UpdateAsync(
        string templateJson,
        string dataJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataJson);

        var template = new AdaptiveCardTemplate(templateJson);
        var cardJson = template.Expand(dataJson);
        var parseResult = AdaptiveCard.FromJsonString(
            cardJson,
            _elementParsers,
            _actionParsers);
        return UpdateAsync(parseResult.AdaptiveCard, cancellationToken);
    }

    /// <summary>Updates the visible card from an already parsed card.</summary>
    public async Task UpdateAsync(
        AdaptiveCard card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        var request = new UpdateRequest(
            card,
            cancellationToken);
        await _updates.EnqueueAsync(request);
    }

    private async Task ProcessUpdateAsync(UpdateRequest request)
    {
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(request.CallerCancellation);
        _cancelActiveUpdate = cancellation.Cancel;
        try
        {
            await UpdateCoreAsync(request.Card, cancellation.Token);
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested
            && !request.CallerCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _cancelActiveUpdate = null;
        }
    }

    /// <summary>Removes the visible card and all incremental state.</summary>
    public void Reset()
    {
        ResetCore();
    }

    private async Task UpdateCoreAsync(
        AdaptiveCard card,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = _renderer.RenderAdaptiveCard(card);
        if (candidate.FrameworkElement is not FrameworkElement candidateRoot)
        {
            throw new InvalidOperationException(
                "Adaptive Card rendering did not produce a framework element.");
        }

        var candidateJson = card.ToJson().Stringify();
        var candidateSnapshot = IncrementalAdaptiveCardVisualTree.Build(
            candidateRoot,
            candidateJson);

        if (RenderedCard?.FrameworkElement is FrameworkElement currentRoot
            && _snapshot is not null)
        {
            var plan = IncrementalTreeDiffer.CreatePlan(_snapshot, candidateSnapshot);
            if (plan.Disposition != IncrementalPlanDisposition.ReplaceRoot
                && await IncrementalAdaptiveCardVisualTree.TryApplyAsync(
                    currentRoot,
                    candidateRoot,
                    plan,
                    cancellationToken))
            {
                _snapshot = candidateSnapshot;
                return;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        RenderedCard = candidate;
        Card = card;
        _snapshot = candidateSnapshot;
        _host.Child = candidateRoot;
    }

    private void ResetCore()
    {
        _cancelActiveUpdate?.Invoke();
        _updates.ClearPending();
        _host.Child = null;
        RenderedCard = null;
        Card = null;
        _snapshot = null;
    }

    private sealed record UpdateRequest(
        AdaptiveCard Card,
        CancellationToken CallerCancellation);
}
