# Advanced Paste

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/advanced-paste)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22%20label%3AIssue-Bug)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Advanced+Paste%22)

## Overview

Advanced Paste is a PowerToys module that provides enhanced clipboard pasting with formatting options and additional functionality.

## Implementation Details

[Source code](/src/modules/AdvancedPaste)

TODO: Add implementation details

### Paste with AI Preview

The "Show preview" setting (`ShowCustomPreview`) controls whether AI-generated results are displayed in a preview window before pasting. **The preview feature does not consume additional AI credits**—the preview displays the same AI response that was already generated, cached locally from a single API call.

The implementation flow:
1. User initiates "Paste with AI" action
2. A single AI API call is made via `ExecutePasteFormatAsync`
3. The result is cached in `GeneratedResponses`
4. If preview is enabled, the cached result is displayed in the preview UI
5. User can paste the cached result without any additional API calls

See the `ExecutePasteFormatAsync(PasteFormat, PasteActionSource)` method in `OptionsViewModel.cs` for the implementation.

## Debugging

TODO: Add debugging information

## Settings

| Setting | Description |
|---------|-------------|
| `ShowCustomPreview` | When enabled, shows AI-generated results in a preview window before pasting. Does not affect AI credit consumption. |

## Future Improvements

TODO: Add potential future improvements
