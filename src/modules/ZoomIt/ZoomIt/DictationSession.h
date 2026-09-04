//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Speech to text for snip annotations. The session listens while the user drags
// out a snip rectangle and yields the transcribed text when the drag ends.
//
// Recognition runs on a dedicated multithreaded apartment worker because the
// snip gesture blocks ZoomIt's single threaded apartment UI thread inside
// SelectRectangle's modal message loop.
//
//==============================================================================
#pragma once

#include "pch.h"
#include <condition_variable>
#include <deque>
#include <functional>
#include <mutex>
#include <string>
#include <thread>

class DictationSession
{
public:
    enum class Status
    {
        Idle,          // Nothing in progress.
        Preparing,     // Recognizer is being created and constraints compiled.
        Ready,         // Recognizer is warm and can start immediately.
        Listening,     // Microphone is open and audio is being transcribed.
        Finalizing,    // Stop requested, waiting for the last phrase.
        Unavailable,   // Speech recognition cannot be used on this machine.
    };

    enum class Failure
    {
        None,
        NotSupported,        // No recognizer, or the platform does not provide one.
        MicrophoneDenied,    // Microphone access is blocked by privacy settings.
        PrivacyNotAccepted,  // Online speech recognition has not been enabled.
        MicrophoneNotDefault, // The chosen microphone is not the Windows default,
                              // which the accurate engine cannot honour.
        LanguageNotSupported,
        Unknown,
    };

    DictationSession();
    ~DictationSession();

    DictationSession( const DictationSession& ) = delete;
    DictationSession& operator=( const DictationSession& ) = delete;

    //
    // Creates the recognizer and compiles its constraints so that a later Start
    // is immediate. Safe to call repeatedly; returns straight away and does the
    // work on the worker thread.
    //
    void Prewarm();

    //
    // Opens the microphone and begins transcribing. Returns false if the
    // session is not in a state where it can listen.
    //
    bool Start();

    //
    // Stops transcribing and waits up to graceMilliseconds for the final phrase
    // to arrive, then returns the accumulated text.
    //
    std::wstring Stop( DWORD graceMilliseconds );

    //
    // Abandons the session and discards anything transcribed so far.
    //
    void Cancel();

    //
    // Releases the recognizer and stops the worker thread.
    //
    void Shutdown();

    Status GetStatus() const;
    Failure GetFailure() const;
    std::wstring Text() const;

    //
    // Raised from the worker thread whenever the transcription changes. Callers
    // must marshal to their UI thread before touching windows.
    //
    void OnTextChanged( std::function<void( const std::wstring& )> callback );

    //
    // Raised from the worker thread whenever the session state changes, so the
    // caller can show what is actually happening rather than assuming that a
    // queued Start succeeded.
    //
    void OnStatusChanged( std::function<void( Status, Failure )> callback );

    //
    // Removes both callbacks and waits for any invocation that already copied
    // one to finish. Call before destroying objects captured by the callbacks.
    //
    void ClearCallbacks();

    //
    // True when speech recognition is compiled in and the platform exposes it.
    // Does not guarantee that the user has granted microphone access.
    //
    static bool IsSupported();

    //
    // A short, localized-agnostic description of why dictation is unavailable,
    // suitable for the status badge. Empty when there is nothing to report.
    //
    static const wchar_t* DescribeFailure( Failure failure );

    //
    // True once the session has settled on the SAPI fallback engine. That
    // engine is a generation behind the modern one, so callers surface this to
    // explain why accuracy is poor instead of leaving the user guessing.
    //
    bool IsUsingFallbackEngine() const;

    //
    // Why the preferred engine was rejected, retained even after the fallback
    // starts successfully. Failure::None when the preferred engine is running.
    //
    Failure GetDegradedReason() const;

    //
    // Opens the Windows speech privacy page so the user can accept the policy
    // that unblocks the more accurate engine. Returns false if it could not be
    // launched.
    //
    static bool OpenSpeechPrivacySettings();

    //
    // Whether the speech privacy policy has been accepted. The modern engine
    // refuses to start until it has, so this is the single setting that decides
    // whether dictation gets the accurate engine or the fallback. Reads the
    // consent flag directly so the Options page can report the state without
    // opening the microphone.
    //
    static bool IsSpeechPrivacyAccepted();

    //
    // Whether the microphone chosen on the Record page is the Windows default.
    // The accurate engine can only record from the default, so this decides
    // which engine dictation is able to use.
    //
    static bool SelectedMicrophoneIsDefault();

private:
    enum class Command
    {
        None,
        Prepare,
        Start,
        Stop,
        Cancel,
        Shutdown,
    };

    //
    // Which recognition engine the session settled on. The modern projection is
    // preferred for accuracy, but it refuses to start until the user accepts
    // the online speech privacy policy, so SAPI provides an offline fallback
    // that works with no setup at all.
    //
    enum class Backend
    {
        None,
        Modern,
        Sapi,
    };

    void EnsureWorker();
    void PostCommand( Command command );
    void WorkerThread();

    // Worker thread only.
    void DoPrepare();
    void DoStart();
    void DoStop();
    void DoCancel();
    void DoShutdown();

    // Worker thread only, modern (Windows.Media / Windows AI) backend.
    bool PrepareModern();
    bool StartModern();
    void StopModern( bool discard );
    void ReleaseModern();

    // Worker thread only, SAPI backend.
    bool PrepareSapi();
    bool StartSapi();
    void StopSapi( bool discard );
    void ReleaseSapi();
    void SapiEventLoop();
    void DrainSapiEvents();

    void AppendPhrase( const std::wstring& phrase );
    void SetHypothesis( const std::wstring& hypothesis );
    void SetStatus( Status status );
    void SetFailure( Failure failure );
    void SetDegradedReason( Failure failure );
    void RaiseTextChanged();
    void RaiseStatusChanged();

    mutable std::mutex m_lock;
    std::condition_variable m_commandSignal;
    std::condition_variable m_stateSignal;
    std::condition_variable m_callbackSignal;
    std::deque<Command> m_pending;
    bool m_workerRunning{ false };
    bool m_startRequested{ false };
    unsigned int m_callbacksInFlight{ 0 };
    std::thread m_worker;

    Status m_status{ Status::Idle };
    Failure m_failure{ Failure::None };
    Failure m_degradedReason{ Failure::None };
    Backend m_backend{ Backend::None };

    // Whether the speech privacy policy was accepted when the engine was last
    // chosen, so a later grant can retire the fallback without a restart.
    bool m_preparedWithConsent{ false };

    // Whether the chosen microphone was the Windows default when the engine
    // was last chosen, for the same reason.
    bool m_preparedWithDefaultMic{ false };
    std::wstring m_text;        // Finalized phrases.
    std::wstring m_hypothesis;  // In flight phrase, not yet finalized.
    std::function<void( const std::wstring& )> m_onTextChanged;
    std::function<void( Status, Failure )> m_onStatusChanged;

    struct Recognizer;
    std::unique_ptr<Recognizer> m_recognizer;
};
