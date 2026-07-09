// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleToastsPage : DynamicListPage
{
    public SampleToastsPage()
    {
        Icon = new IconInfo("\uE789"); // QuickNote glyph
        Name = "Toast Notifications";
        Title = "Toast Notification Samples";
        PlaceholderText = "Type a custom message and press Enter…";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        // When the user has typed something, the first item becomes a
        // "send this custom message" affordance — the easiest way to play
        // with the toast window from the palette.
        var customItem = !string.IsNullOrEmpty(query)
            ? new ListItem(new AnonymousCommand(() => { })
            {
                Name = "Send custom toast",
                Result = CommandResult.ShowToast(new ToastArgs
                {
                    Message = query,

                    // KeepOpen lets you fire more toasts without re-opening
                    // the palette, which is handy for rapid-fire testing.
                    Result = CommandResult.KeepOpen(),
                }),
            })
            {
                Title = $"Show toast: \"{query}\"",
                Subtitle = "Uses CommandResult.ShowToast(...) and keeps the palette open",
                Icon = new IconInfo("\uE724"), // Send
            }
            : new ListItem(new NoOpCommand())
            {
                Title = "Type a message above to send a custom toast",
                Subtitle = "Start typing — the first item becomes a 'Show toast' action",
                Icon = new IconInfo("\uE8BD"), // Edit
            };

        return
        [
            customItem,

            // Simplest possible call: dismiss the palette and show a short toast.
            new ListItem(new ShowToastCommand("Hello from the Command Palette!"))
            {
                Title = "Short toast (dismisses the palette)",
                Subtitle = "CommandResult.ShowToast(\"...\") with the default Dismiss result",
                Icon = new IconInfo("\uE91C"),
            },

            // Same call, but keep the palette visible after firing.
            new ListItem(new AnonymousCommand(() => { })
            {
                Name = "Show toast (keep palette open)",
                Result = CommandResult.ShowToast(new ToastArgs
                {
                    Message = "The palette stays open — press Enter again to re-fire.",
                    Result = CommandResult.KeepOpen(),
                }),
            })
            {
                Title = "Short toast (keeps the palette open)",
                Subtitle = "ToastArgs.Result = CommandResult.KeepOpen()",
                Icon = new IconInfo("\uE8A7"),
            },

            // Long message exercises wrapping inside the banner.
            new ListItem(new AnonymousCommand(() => { })
            {
                Name = "Show long toast",
                Result = CommandResult.ShowToast(new ToastArgs
                {
                    Message = "This is a much longer toast message designed to verify that the banner inside the transparent toast window wraps gracefully across multiple lines without clipping its drop shadow or its slide-in animation.",
                    Result = CommandResult.KeepOpen(),
                }),
            })
            {
                Title = "Long, wrapping toast",
                Subtitle = "Verifies multi-line wrapping inside the banner",
                Icon = new IconInfo("\uE7C3"),
            },

            // Re-invoking the same item should cancel the pending hide and
            // restart the show animation with the same message.
            new ListItem(new AnonymousCommand(() => { })
            {
                Name = "Re-fire toast",
                Result = CommandResult.ShowToast(new ToastArgs
                {
                    Message = "Re-fired! Press Enter again to restart the toast.",
                    Result = CommandResult.KeepOpen(),
                }),
            })
            {
                Title = "Rapid re-fire (press Enter repeatedly)",
                Subtitle = "Each press resets the hide timer and restarts the show animation",
                Icon = new IconInfo("\uE895"),
            },

            // The other path: ToastStatusMessage routes through the extension
            // host and renders as an in-page status banner instead of the
            // transparent toast window. Included for contrast.
            new ListItem(new ToastCommand("This is an in-page status message", MessageState.Success))
            {
                Title = "In-page status message (different path)",
                Subtitle = "Uses ToastStatusMessage — renders inline, NOT in the toast window",
                Icon = new IconInfo("\uE7BA"),
            },
        ];
    }
}
