#pragma once

#include <functional>
#include <string>

// Shows a small native progress/result window while the Bug Report tool runs.
// - Displays an animated "Generating bug report..." state while toolPath executes.
// - When the tool finishes, switches to a result state that shows where the
//   .zip was saved and offers "Open folder" and "Report on GitHub" actions.
//
// This function blocks until the user closes the window, so it must be called
// from a dedicated worker thread. onProcessFinished is invoked exactly once,
// as soon as the tool process exits (before the window is closed), so callers
// can clear any "running" state independently of the result window lifetime.
void run_bug_report_dialog(const std::wstring& toolPath, const std::function<void()>& onProcessFinished);
