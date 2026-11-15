# Text Extractor

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/text-extractor)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Extractor%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Text%20Extractor%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Text+Extractor%22)

## Overview
Text Extractor is a PowerToys utility that enables users to extract and copy text from anywhere on the screen, including inside images and videos. The module uses Optical Character Recognition (OCR) technology to recognize text in visual content. This module is based on Joe Finney's Text Grab.

## UI tests

The winappcli migration lives in [PowerOCR.UITests.Next](../../../src/modules/PowerOCR/PowerOCR.UITests.Next), alongside the unchanged legacy suite. It ports all 10 existing tests, including the toolbar, failed-capture keyboard, and cross-cursor regressions; it does not add multi-monitor coverage.

Build with `tools\build\build.cmd -Path src\modules\PowerOCR\PowerOCR.UITests.Next -Platform x64 -Configuration Debug` (use `ARM64` for the ARM64 build). Run the resulting `PowerOCR.UITests.Next.exe` on an interactive standard-user desktop with `--report-trx --results-directory <directory>`. See the [UI test guide](../development/ui-tests.md) for framework setup.

The language-change tests require at least two installed Windows OCR language capabilities, including English (`en-US`) for the deterministic text fixture. The suite seeds the English language's native name in module settings and checks the captured fixture text. It restores module settings, clipboard text (including an initially empty clipboard), and pointer position after each test.

On an English Windows installation, install a second recognizer from an elevated Windows PowerShell terminal:

```powershell
Add-WindowsCapability -Online -Name Language.Basic~~~en-GB~0.0.1.0
Add-WindowsCapability -Online -Name Language.OCR~~~en-GB~0.0.1.0
```

OCR capabilities depend on the basic language capability for the same language. CI's **Provision PowerOCR test languages** step installs the required `en-US` and `en-GB` capabilities without changing the agent user's language list or keyboard layouts. A provisioning failure is reported as blocked infrastructure for PowerOCR; other selected suites still run. Local language-change tests fail explicitly when the required recognizers are missing. Release CI uses the existing Runner/Settings companion-signing setup so the disable test exercises real Settings IPC.

## How it works
Text Extractor captures the screen content and uses OCR to identify and extract text from the selected area. Users can select a region of the screen, and Text Extractor will convert any visible text in that region into copyable text.

## Architecture

### Components
- **EventMonitor**: Handles the `ShowPowerOCRSharedEvent` which triggers the OCR functionality
- **OCROverlay**: The main UI component that provides:
  - Language selection for OCR processing
  - Canvas for selecting the screen area to extract text from
- **Screen Capture**: Uses `CopyFromScreen` to capture the screen content as the overlay background image

### Activation Methods
- **Global Shortcut**: Activates Text Extractor through a keyboard shortcut
- **LaunchOCROverlayOnEveryScreen**: Functionality to display the OCR overlay across multiple monitors

## Technical Implementation
Text Extractor is implemented using Windows Presentation Foundation (WPF) technology, which provides the UI framework for the selection canvas and other interface elements.

## User Experience
When activated, Text Extractor displays an overlay on the screen that allows users to select an area containing text. Once selected, the OCR engine processes the image and extracts any text found, which can then be copied to the clipboard.
