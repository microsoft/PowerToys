#pragma once

// Detect AI capabilities by calling ImageResizer in detection mode.
// This runs in a background thread to avoid blocking.
// ImageResizer writes the result to a cache file that it reads on normal startup.
//
// Parameters:
//   skipSettingsCheck - If true, skip checking if ImageResizer is enabled in settings.
//                       Use this when called from apply_general_settings where we know
//                       ImageResizer is being enabled but settings file may not be saved yet.
void DetectAiCapabilitiesAsync(bool skipSettingsCheck = false);
