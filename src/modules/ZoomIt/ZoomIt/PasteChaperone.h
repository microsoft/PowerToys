//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Delivers a dictation snip's image and transcription to applications whose
// paste handler accepts either an image or text, but never both.
//
// Chat surfaces such as the GitHub Copilot CLI and the Visual Studio Code chat
// input choose exactly one clipboard payload: if the clipboard carries text
// they insert the text and never look at the image. No combination of
// clipboard formats can change that, because the decision is made in the
// application, not by the clipboard.
//
// The chaperone works around it by splitting the single user gesture into two
// pastes. It publishes the image on its own, watches for the user's Ctrl+V,
// and once that paste has been consumed it swaps the clipboard to the
// transcription and synthesizes a second Ctrl+V. The user presses Ctrl+V once
// and the target receives the screenshot as an attachment followed by the
// dictated instruction.
//
//==============================================================================
#pragma once

#include "pch.h"
#include <string>

namespace PasteChaperone
{
    //
    // Publishes the image stage and arms the sequence. bitmap and text are
    // copied, so the caller keeps ownership of bitmap.
    //
    // combinedFormats is the full set of formats to restore once the sequence
    // has finished, so that a later paste into an application that does accept
    // both - such as OneNote or Word - still receives everything.
    //
    // Returns false if there is nothing to chaperone, in which case the caller
    // is responsible for publishing the clipboard itself.
    //
    bool Arm( HBITMAP bitmap, const std::wstring& text, DWORD combinedFormats );

    //
    // Cancels an armed sequence. Safe to call when nothing is armed.
    //
    void Cancel();

    bool IsArmed();
}
