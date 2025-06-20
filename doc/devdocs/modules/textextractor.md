# Text Extractor

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/text-extractor)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Extractor%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Text%20Extractor%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Text+Extractor%22)

## Overview
Text Extractor is a PowerToys utility that enables users to extract and copy text from anywhere on the screen, including inside images and videos. The module uses Optical Character Recognition (OCR) technology to recognize text in visual content. This module is based on Joe Finney's Text Grab.

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
