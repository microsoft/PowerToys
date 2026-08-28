// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

// A provider-side example using only today's GetContent/PropChanged contract.
// The host has no cancellation or loading-state contract with this sample.
internal sealed partial class SampleDeferredDetails : ContentDetails
{
    private readonly Lock _gate = new();
    private readonly Func<Task> _simulateWork;
    private readonly bool _failFirstAttempt;
    private readonly HeaderContent _header;
    private readonly SectionContent _preview;
    private readonly MarkdownContent _status = new("**Loading...** The preview is already usable.");
    private readonly AnonymousCommand _reload;
    private Task _loadTask = Task.CompletedTask;
    private int _loadCount;

    internal Task Completion
    {
        get
        {
            lock (_gate)
            {
                return _loadTask;
            }
        }
    }

    internal int LoadCount => Volatile.Read(ref _loadCount);

    public SampleDeferredDetails(string title, Func<Task> simulateWork, bool failFirstAttempt = false)
    {
        _simulateWork = simulateWork;
        _failFirstAttempt = failFirstAttempt;
        Size = ContentSize.Medium;
        _header = new HeaderContent { Title = title, Subtitle = "Cached preview followed by a complete replacement snapshot" };
        _preview = new SectionContent
        {
            Title = "Available immediately",
            PreviewItemCount = 1,
            Content =
            [
                new MarkdownContent("This preview was available before the slow work started."),
                new MarkdownContent("Expand me while loading. This section object is retained when the result arrives."),
            ],
        };
        _reload = new AnonymousCommand(() => StartLoading(force: true))
        {
            Name = "Load again",
            Icon = new IconInfo("\uE72C"),
            Result = CommandResult.KeepOpen(),
        };
        Content = [_header, _preview, _status];
    }

    public override IContent[] GetContent()
    {
        StartLoading(force: false);
        return base.GetContent();
    }

    private void StartLoading(bool force)
    {
        lock (_gate)
        {
            // Repeated getters, including notifications during loading, share one
            // operation. Only the explicit button starts a fresh attempt.
            if (!_loadTask.IsCompleted || (!force && _loadCount != 0))
            {
                return;
            }

            var attempt = ++_loadCount;

            // Do not call extension notifications while holding _gate.
            _loadTask = Task.Run(() => LoadAsync(attempt));
        }
    }

    private async Task LoadAsync(int attempt)
    {
        try
        {
            if (attempt > 1)
            {
                _status.Body = "**Loading...** The preview is already usable.";
                Content = [_header, _preview, _status];
            }

            await _simulateWork().ConfigureAwait(false);
            if (_failFirstAttempt && attempt == 1)
            {
                throw new InvalidOperationException("Simulated failure. Choose Retry to finish loading.");
            }

            _status.Body = "**Loaded.** Reselect this item to reuse its cached result.";
            _reload.Name = "Load again";
            Content =
            [
                _header,
                _preview,
                _status,
                new PropertyGridContent
                {
                    Properties =
                    [
                        new PropertyContent { Label = "Result", Value = new TextContent("Simulated metadata") },
                        new PropertyContent { Label = "Load attempts", Value = new TextContent(attempt.ToString(CultureInfo.CurrentCulture)) },
                        new PropertyContent { Label = "Status", Value = new TagsContent { Tags = [new Tag("Cached")] } },
                    ],
                },
                new CommandsContent { Commands = [_reload] },
            ];
        }
        catch (Exception ex)
        {
            // Keep the cheap preview after failure; getters do not retry implicitly.
            _status.Body = $"**Could not load details.** {ex.Message}";
            _reload.Name = "Retry";
            Content = [_header, _preview, _status, new CommandsContent { Commands = [_reload] }];
        }
    }
}
