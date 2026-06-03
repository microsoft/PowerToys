#pragma once

// Cycles the top-level windows of the currently focused application.
//   forward == true  -> next window of the same app
//   forward == false -> previous window of the same app
//
// "Same application" is defined as the same process image path, which correctly
// groups multi-window apps (and multi-process apps like browsers, whose top-level
// windows belong to the main process). The group is sorted by HWND so the order
// is stable across activations and repeated presses cycle through every window.
void CycleForegroundAppWindows(bool forward);
