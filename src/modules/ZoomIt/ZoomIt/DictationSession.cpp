//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Speech to text for snip annotations.
//
// Two engines are used, in preference order:
//
//   * whisper.cpp with an embedded quantized small.en model. It runs entirely
//     in process, needs no package identity or network service, and records
//     directly from the microphone selected on ZoomIt's Record page.
//
//   * SAPI (ISpInprocRecognizer) with a dictation grammar. Fully on device,
//     needs no privacy consent or package identity, and remains the fallback if
//     the embedded model or Whisper audio capture cannot be initialized.
//
// The older Windows speech implementation remains below for reference but is
// no longer selected: its accurate recognizer needs package identity, which a
// single-file standalone ZoomIt cannot provide.
//
//==============================================================================
#include "pch.h"
#include "DictationSession.h"
#include "WhisperRecognizer.h"

#include <appmodel.h>
#include <deque>
#include <winrt/Windows.Media.SpeechRecognition.h>
#include <winrt/Windows.Devices.Enumeration.h>

#include <sapi.h>
#include <wil/com.h>
#include <mmdeviceapi.h>

#ifdef ZOOMIT_WINAI_SPEECH
#include <winrt/Microsoft.Windows.AI.h>
#include <winrt/Microsoft.Windows.AI.Speech.h>
#endif

// Defined in Zoomit.cpp
void OutputDebug( const TCHAR* format, ... );

// The microphone device chosen on the Record page of the ZoomIt options.
// Dictation captures from the same device, so there is one microphone setting
// for the whole application. The inbox Windows.Media.SpeechRecognition engine
// is the exception: it exposes no way to select a capture device and always
// uses the system default.
extern TCHAR g_MicrophoneDeviceId[];

// Defined below. Decides whether the modern engine, which always records from
// the Windows default, would honour the microphone chosen on the Record page.
static bool SelectedMicrophoneIsSystemDefault();

namespace
{
    using namespace std::chrono_literals;

    // Speech platform errors that map to actionable user guidance.
    constexpr HRESULT SPERR_PRIVACY_POLICY_NOT_ACCEPTED = static_cast<HRESULT>(0x80045509);
    constexpr HRESULT SPERR_MICROPHONE_NOT_FOUND = static_cast<HRESULT>(0x8004503A);

    bool HasPackageIdentity()
    {
        UINT32 length = 0;
        return GetCurrentPackageFullName( &length, nullptr ) == ERROR_INSUFFICIENT_BUFFER;
    }

    std::wstring TrimCopy( const std::wstring& text )
    {
        const size_t first = text.find_first_not_of( L" \t\r\n" );
        if( first == std::wstring::npos )
        {
            return {};
        }
        const size_t last = text.find_last_not_of( L" \t\r\n" );
        return text.substr( first, last - first + 1 );
    }
}

//
// Backend specific state. Kept out of the header so that the Windows App SDK
// headers are not pulled into every translation unit that includes it.
//
struct DictationSession::Recognizer
{
    std::unique_ptr<WhisperRecognizer> whisper;

#ifdef ZOOMIT_WINAI_SPEECH
    winrt::Microsoft::Windows::AI::Speech::SpeechRecognitionModel model{ nullptr };
    winrt::Microsoft::Windows::AI::Speech::StreamingRecognition recognition{ nullptr };
#else
    winrt::Windows::Media::SpeechRecognition::SpeechRecognizer recognizer{ nullptr };
    winrt::event_token hypothesisToken{};
    winrt::event_token resultToken{};
#endif

    // SAPI fallback.
    wil::com_ptr<ISpRecognizer> sapiRecognizer;
    wil::com_ptr<ISpRecoContext> sapiContext;
    wil::com_ptr<ISpRecoGrammar> sapiGrammar;
    std::thread sapiEvents;
    wil::unique_event sapiQuit;

    bool listening{ false };
};

//----------------------------------------------------------------------------
//
// DictationSession::~DictationSession
//
//----------------------------------------------------------------------------
DictationSession::DictationSession() = default;

DictationSession::~DictationSession()
{
    Shutdown();
}

//----------------------------------------------------------------------------
//
// DictationSession::IsSupported
//
//----------------------------------------------------------------------------
bool DictationSession::IsSupported()
{
    return true;
}

//----------------------------------------------------------------------------
//
// DictationSession::DescribeFailure
//
//----------------------------------------------------------------------------
const wchar_t* DictationSession::DescribeFailure( Failure failure )
{
    switch( failure )
    {
    case Failure::NotSupported:
        return L"Speech recognition is not available on this PC";
    case Failure::MicrophoneDenied:
        return L"Microphone access is blocked in Privacy settings";
    case Failure::PrivacyNotAccepted:
        return L"Turn on online speech recognition in Privacy settings";
    case Failure::MicrophoneNotDefault:
        return L"The accurate engine only records from the default microphone";
    case Failure::LanguageNotSupported:
        return L"Speech recognition does not support this display language";
    case Failure::Unknown:
        return L"Dictation failed";
    default:
        return L"";
    }
}

//----------------------------------------------------------------------------
//
// DictationSession accessors
//
//----------------------------------------------------------------------------
DictationSession::Status DictationSession::GetStatus() const
{
    std::lock_guard<std::mutex> guard( m_lock );
    return m_status;
}

DictationSession::Failure DictationSession::GetFailure() const
{
    std::lock_guard<std::mutex> guard( m_lock );
    return m_failure;
}

bool DictationSession::IsUsingFallbackEngine() const
{
    std::lock_guard<std::mutex> guard( m_lock );
    return m_backend == Backend::Sapi;
}

DictationSession::Failure DictationSession::GetDegradedReason() const
{
    std::lock_guard<std::mutex> guard( m_lock );
    return m_degradedReason;
}

bool DictationSession::OpenSpeechPrivacySettings()
{
    //
    // ms-settings: is handled by the Settings app, so ShellExecute rather than
    // CreateProcess. INT_PTR > 32 is the documented success test.
    //
    const auto result = reinterpret_cast<INT_PTR>( ShellExecute(
        nullptr, L"open", L"ms-settings:privacy-speech", nullptr, nullptr, SW_SHOWNORMAL ) );
    return result > 32;
}

bool DictationSession::IsSpeechPrivacyAccepted()
{
    DWORD accepted = 0;
    DWORD size = sizeof( accepted );
    const LSTATUS status = RegGetValue(
        HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy",
        L"HasAccepted",
        RRF_RT_REG_DWORD,
        nullptr,
        &accepted,
        &size );

    // A missing key means the user was never asked, which the engine treats
    // exactly like a refusal.
    return status == ERROR_SUCCESS && accepted != 0;
}

bool DictationSession::SelectedMicrophoneIsDefault()
{
    return SelectedMicrophoneIsSystemDefault();
}

std::wstring DictationSession::Text() const
{
    std::lock_guard<std::mutex> guard( m_lock );
    return TrimCopy( m_text );
}

void DictationSession::OnTextChanged( std::function<void( const std::wstring& )> callback )
{
    std::lock_guard<std::mutex> guard( m_lock );
    m_onTextChanged = std::move( callback );
}

void DictationSession::OnStatusChanged( std::function<void( Status, Failure )> callback )
{
    std::lock_guard<std::mutex> guard( m_lock );
    m_onStatusChanged = std::move( callback );
}

void DictationSession::ClearCallbacks()
{
    std::unique_lock<std::mutex> lock( m_lock );
    m_onTextChanged = nullptr;
    m_onStatusChanged = nullptr;
    m_callbackSignal.wait( lock, [this] { return m_callbacksInFlight == 0; } );
}

//----------------------------------------------------------------------------
//
// DictationSession::SetStatus / SetFailure
//
//----------------------------------------------------------------------------
void DictationSession::SetStatus( Status status )
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( m_status == status )
        {
            return;
        }
        m_status = status;
    }
    m_stateSignal.notify_all();
    RaiseStatusChanged();
}

void DictationSession::SetFailure( Failure failure )
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( m_failure == failure )
        {
            return;
        }
        m_failure = failure;
    }
    RaiseStatusChanged();
}

void DictationSession::SetDegradedReason( Failure failure )
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( m_degradedReason == failure )
        {
            return;
        }
        m_degradedReason = failure;
    }
    RaiseStatusChanged();
}

//----------------------------------------------------------------------------
//
// DictationSession::RaiseStatusChanged
//
// Lets the caller show what the engine is really doing. Without this the UI
// can only report that a command was queued, which is not the same thing as
// the microphone being open.
//
//----------------------------------------------------------------------------
void DictationSession::RaiseStatusChanged()
{
    std::function<void( Status, Failure )> callback;
    Status status{};
    Failure failure{};
    {
        std::lock_guard<std::mutex> guard( m_lock );
        callback = m_onStatusChanged;
        status = m_status;
        failure = m_failure;
        if( callback )
        {
            ++m_callbacksInFlight;
        }
    }

    if( callback )
    {
        callback( status, failure );
        {
            std::lock_guard<std::mutex> guard( m_lock );
            --m_callbacksInFlight;
        }
        m_callbackSignal.notify_all();
    }
}

//----------------------------------------------------------------------------
//
// DictationSession::RaiseTextChanged
//
// Invoked from recognition callbacks, so the handler is copied and called
// without the lock held.
//
//----------------------------------------------------------------------------
void DictationSession::RaiseTextChanged()
{
    std::function<void( const std::wstring& )> callback;
    std::wstring display;
    {
        std::lock_guard<std::mutex> guard( m_lock );
        callback = m_onTextChanged;
        display = m_text;
        if( !m_hypothesis.empty() )
        {
            if( !display.empty() )
            {
                display += L' ';
            }
            display += m_hypothesis;
        }
        if( callback )
        {
            ++m_callbacksInFlight;
        }
    }

    if( callback )
    {
        callback( TrimCopy( display ) );
        {
            std::lock_guard<std::mutex> guard( m_lock );
            --m_callbacksInFlight;
        }
        m_callbackSignal.notify_all();
    }
}

void DictationSession::AppendPhrase( const std::wstring& phrase )
{
    const std::wstring trimmed = TrimCopy( phrase );
    if( trimmed.empty() )
    {
        return;
    }

    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( !m_text.empty() )
        {
            m_text += L' ';
        }
        m_text += trimmed;
        m_hypothesis.clear();
    }

    RaiseTextChanged();
}

void DictationSession::SetHypothesis( const std::wstring& hypothesis )
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_hypothesis = TrimCopy( hypothesis );
    }

    RaiseTextChanged();
}

//----------------------------------------------------------------------------
//
// DictationSession::EnsureWorker / PostCommand
//
//----------------------------------------------------------------------------
void DictationSession::EnsureWorker()
{
    std::lock_guard<std::mutex> guard( m_lock );
    if( m_workerRunning )
    {
        return;
    }

    // A previous session may have left a finished thread object behind.
    if( m_worker.joinable() )
    {
        m_worker.join();
    }

    m_workerRunning = true;
    m_worker = std::thread( [this] { WorkerThread(); } );
}

void DictationSession::PostCommand( Command command )
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_pending.push_back( command );
    }
    m_commandSignal.notify_all();
}

//----------------------------------------------------------------------------
//
// DictationSession::WorkerThread
//
// Runs in a multithreaded apartment so that the blocking WinRT calls below are
// legal; the ZoomIt UI thread is single threaded and busy in a modal loop.
//
//----------------------------------------------------------------------------
void DictationSession::WorkerThread()
{
    winrt::init_apartment( winrt::apartment_type::multi_threaded );

    for( ;; )
    {
        Command command = Command::None;
        {
            std::unique_lock<std::mutex> lock( m_lock );
            m_commandSignal.wait( lock, [this] { return !m_pending.empty(); } );
            command = m_pending.front();
            m_pending.pop_front();
        }

        switch( command )
        {
        case Command::Prepare:
            DoPrepare();
            break;
        case Command::Start:
            DoStart();
            break;
        case Command::Stop:
            DoStop();
            break;
        case Command::Cancel:
            DoCancel();
            break;
        case Command::Shutdown:
            DoShutdown();
            break;
        default:
            break;
        }

        m_stateSignal.notify_all();

        if( command == Command::Shutdown )
        {
            break;
        }
    }

    winrt::uninit_apartment();
}

//----------------------------------------------------------------------------
//
// DictationSession::Prewarm
//
//----------------------------------------------------------------------------
void DictationSession::Prewarm()
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( m_status == Status::Preparing || m_status == Status::Ready ||
            m_status == Status::Listening || m_status == Status::Unavailable )
        {
            return;
        }
        m_status = Status::Preparing;
    }

    EnsureWorker();
    PostCommand( Command::Prepare );
}

//----------------------------------------------------------------------------
//
// DictationSession::Start
//
//----------------------------------------------------------------------------
bool DictationSession::Start()
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( m_status == Status::Unavailable ||
            m_status == Status::Listening ||
            m_status == Status::Finalizing )
        {
            return false;
        }
        m_text.clear();
        m_hypothesis.clear();
        m_failure = Failure::None;
        m_startRequested = true;
        m_cancelRequested = false;

        // Reflect that work is queued, so a Stop that arrives before the engine
        // is listening waits for it instead of returning nothing.
        if( m_status != Status::Ready )
        {
            m_status = Status::Preparing;
        }
    }

    RaiseStatusChanged();
    EnsureWorker();

    // Prepare is queued ahead of Start when the recognizer is not warm yet, and
    // the worker processes the queue in order.
    if( GetStatus() != Status::Ready )
    {
        PostCommand( Command::Prepare );
    }
    PostCommand( Command::Start );
    return true;
}

//----------------------------------------------------------------------------
//
// DictationSession::Stop
//
// Waits for the recognizer to deliver its final phrase, bounded by the grace
// period so that releasing the mouse never leaves the user waiting.
//
//----------------------------------------------------------------------------
std::wstring DictationSession::Stop( DWORD graceMilliseconds, bool* cancelled )
{
    bool cancelPendingStart = false;
    bool waitForWhisper = false;
    bool cancelledByUser = false;
    if( cancelled )
    {
        *cancelled = false;
    }
    {
        std::unique_lock<std::mutex> lock( m_lock );

        //
        // Start only queues work, so a quick drag can reach Stop while the
        // engine is still being prepared. Waiting for the queued Start to land
        // is what keeps short dictations from being silently discarded.
        //
        if( m_status == Status::Preparing || m_status == Status::Ready )
        {
            const auto startDeadline =
                std::chrono::steady_clock::now() + std::chrono::seconds( 5 );
            while( m_status != Status::Listening &&
                   m_status != Status::Unavailable &&
                   m_status != Status::Idle &&
                   std::chrono::steady_clock::now() < startDeadline )
            {
                if( GetAsyncKeyState( VK_ESCAPE ) & 0x8000 )
                {
                    cancelledByUser = true;
                    m_cancelRequested = true;
                    break;
                }
                m_stateSignal.wait_for( lock, std::chrono::milliseconds( 50 ) );
            }
        }

        if( m_status != Status::Listening )
        {
            // Preparation can outlive the bounded wait above. Revoke the
            // queued start so it cannot open the microphone after the snip
            // has already completed.
            m_startRequested = false;
            m_pending.erase(
                std::remove( m_pending.begin(), m_pending.end(), Command::Start ),
                m_pending.end() );
            cancelPendingStart = true;
            OutputDebug( L"[Dictation] Stop with status=%d, nothing to finalize\n",
                         static_cast<int>(m_status) );
        }
        else
        {
            m_startRequested = false;
            m_status = Status::Finalizing;
            waitForWhisper = m_backend == Backend::Whisper;
        }
    }

    if( cancelPendingStart )
    {
        PostCommand( Command::Cancel );
        if( cancelled )
        {
            *cancelled = cancelledByUser;
        }
        return Text();
    }

    RaiseStatusChanged();
    PostCommand( Command::Stop );

    std::unique_lock<std::mutex> lock( m_lock );
    const DWORD waitMilliseconds =
        waitForWhisper ? std::max<DWORD>( graceMilliseconds, 10000 ) : graceMilliseconds;
    bool finalized = false;
    const auto deadline =
        std::chrono::steady_clock::now() + std::chrono::milliseconds( waitMilliseconds );
    while( m_status == Status::Finalizing && std::chrono::steady_clock::now() < deadline )
    {
        if( GetAsyncKeyState( VK_ESCAPE ) & 0x8000 )
        {
            cancelledByUser = true;
            m_cancelRequested = true;
            break;
        }
        m_stateSignal.wait_for( lock, std::chrono::milliseconds( 50 ) );
    }
    finalized = m_status != Status::Finalizing;

    if( !finalized )
    {
        if( cancelledByUser )
        {
            OutputDebug( L"[Dictation] Finalize cancelled by user\n" );
        }
        else
        {
            OutputDebug( L"[Dictation] Finalize timed out after %lu ms\n", waitMilliseconds );
        }
        if( waitForWhisper )
        {
            m_cancelRequested = true;
            m_stateSignal.wait( lock, [this] { return m_status != Status::Finalizing; } );
        }
        else if( cancelledByUser )
        {
            // SAPI cannot abort an in-progress final drain. Wait for it to
            // quiesce before the caller destroys the badge callbacks.
            m_stateSignal.wait( lock, [this] { return m_status != Status::Finalizing; } );
        }
    }

    if( cancelled )
    {
        *cancelled = cancelledByUser;
    }
    return TrimCopy( m_text );
}

//----------------------------------------------------------------------------
//
// DictationSession::Cancel
//
//----------------------------------------------------------------------------
void DictationSession::Cancel()
{
    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_text.clear();
        m_hypothesis.clear();
        m_startRequested = false;
        m_cancelRequested = true;
        m_pending.erase(
            std::remove( m_pending.begin(), m_pending.end(), Command::Start ),
            m_pending.end() );
    }

    // Always queue cancellation. Prepare or Start may already be executing,
    // and DoStart rechecks m_startRequested after opening the microphone.
    PostCommand( Command::Cancel );
}

//----------------------------------------------------------------------------
//
// DictationSession::Shutdown
//
//----------------------------------------------------------------------------
void DictationSession::Shutdown()
{
    bool running = false;
    {
        std::lock_guard<std::mutex> guard( m_lock );
        running = m_workerRunning;
        m_workerRunning = false;
        m_startRequested = false;
        m_cancelRequested = true;
    }

    ClearCallbacks();

    if( !running )
    {
        return;
    }

    PostCommand( Command::Shutdown );
    if( m_worker.joinable() )
    {
        m_worker.join();
    }
}

//----------------------------------------------------------------------------
//
// Worker thread implementation
//
// DoPrepare / DoStart / DoStop dispatch to whichever engine the session
// settled on, so the command queue above is engine agnostic.
//
//----------------------------------------------------------------------------
void DictationSession::DoPrepare()
{
    if( m_backend != Backend::None )
    {
        SetStatus( Status::Ready );
        return;
    }

    if( !m_whisperFailed && PrepareWhisper() )
    {
        m_backend = Backend::Whisper;
        SetFailure( Failure::None );
        SetDegradedReason( Failure::None );
        SetStatus( Status::Ready );
        return;
    }

    ReleaseWhisper();

    if( PrepareSapi() )
    {
        OutputDebug( L"[Dictation] Embedded Whisper unavailable, using SAPI\n" );
        m_backend = Backend::Sapi;
        SetFailure( Failure::None );
        SetDegradedReason( Failure::NotSupported );
        SetStatus( Status::Ready );
        return;
    }

    ReleaseSapi();
    m_recognizer.reset();
    m_backend = Backend::None;
    SetStatus( Status::Unavailable );
}

void DictationSession::DoStart()
{
    const auto startStillRequested = [this] {
        std::lock_guard<std::mutex> guard( m_lock );
        return m_startRequested;
    };

    const auto discardStartedRecognition = [this] {
        if( m_backend == Backend::Whisper )
        {
            StopWhisper( true );
        }
        else if( m_backend == Backend::Modern )
        {
            StopModern( true );
        }
        else
        {
            StopSapi( true );
        }
        m_recognizer->listening = false;
    };

    if( !startStillRequested() )
    {
        return;
    }

    if( m_backend == Backend::None )
    {
        SetStatus( Status::Idle );
        return;
    }

    const bool started =
        m_backend == Backend::Whisper ? StartWhisper() :
        m_backend == Backend::Modern ? StartModern() :
        StartSapi();
    if( started )
    {
        m_recognizer->listening = true;

        // Cancel may have arrived while the backend was inside its blocking
        // start call. Close the microphone immediately instead of publishing
        // a Listening state for a snip that no longer exists.
        if( !startStillRequested() )
        {
            discardStartedRecognition();
            return;
        }

        SetFailure( Failure::None );
        SetStatus( Status::Listening );
        return;
    }

    //
    // The modern engine usually fails here rather than during prepare, because
    // the privacy policy is only consulted once audio is requested. Fall back
    // rather than telling the user that dictation is impossible.
    //
    if( m_backend == Backend::Whisper )
    {
        OutputDebug( L"[Dictation] Whisper capture could not start, falling back to SAPI\n" );

        // Captured before the fallback clears it; this is the reason the user
        // needs to see to understand why recognition is about to be worse.
        const Failure modernFailure = GetFailure();

        ReleaseWhisper();
        m_backend = Backend::None;

        if( !startStillRequested() )
        {
            return;
        }

        if( PrepareSapi() && StartSapi() )
        {
            m_backend = Backend::Sapi;
            m_recognizer->listening = true;

            if( !startStillRequested() )
            {
                discardStartedRecognition();
                return;
            }

            SetFailure( Failure::None );
            SetDegradedReason( modernFailure );
            SetStatus( Status::Listening );
            return;
        }

        ReleaseSapi();
    }

    SetStatus( Status::Unavailable );
}

void DictationSession::DoStop()
{
    if( m_recognizer && m_recognizer->listening )
    {
        if( m_backend == Backend::Whisper )
        {
            StopWhisper( false );
        }
        else if( m_backend == Backend::Modern )
        {
            StopModern( false );
        }
        else
        {
            StopSapi( false );
        }
        m_recognizer->listening = false;
    }

    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_hypothesis.clear();
    }

    SetStatus( m_backend == Backend::None ? Status::Idle : Status::Ready );
}

//----------------------------------------------------------------------------
//
// DictationSession::DoCancel
//
//----------------------------------------------------------------------------
void DictationSession::DoCancel()
{
    if( m_recognizer && m_recognizer->listening )
    {
        if( m_backend == Backend::Whisper )
        {
            StopWhisper( true );
        }
        else if( m_backend == Backend::Modern )
        {
            StopModern( true );
        }
        else
        {
            StopSapi( true );
        }
        m_recognizer->listening = false;
    }

    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_text.clear();
        m_hypothesis.clear();
    }

    SetStatus( m_backend == Backend::None ? Status::Idle : Status::Ready );
}

//----------------------------------------------------------------------------
//
// DictationSession::DoShutdown
//
//----------------------------------------------------------------------------
void DictationSession::DoShutdown()
{
    DoCancel();
    ReleaseWhisper();
    ReleaseModern();
    ReleaseSapi();
    m_recognizer.reset();
    m_backend = Backend::None;
    SetStatus( Status::Idle );
}

//----------------------------------------------------------------------------
//
// Embedded Whisper engine
//
//----------------------------------------------------------------------------
bool DictationSession::PrepareWhisper()
{
    if( !m_recognizer )
    {
        m_recognizer = std::make_unique<Recognizer>();
    }
    if( !m_recognizer->whisper )
    {
        m_recognizer->whisper = std::make_unique<WhisperRecognizer>();
    }

    if( m_recognizer->whisper->Prepare() )
    {
        return true;
    }

    const auto failure = m_recognizer->whisper->GetFailure();
    SetFailure(
        failure == WhisperRecognizer::Failure::MicrophoneDenied
            ? Failure::MicrophoneDenied
            : Failure::NotSupported );
    return false;
}

bool DictationSession::StartWhisper()
{
    if( m_recognizer &&
        m_recognizer->whisper &&
        m_recognizer->whisper->Start() )
    {
        return true;
    }

    const auto failure =
        m_recognizer && m_recognizer->whisper
            ? m_recognizer->whisper->GetFailure()
            : WhisperRecognizer::Failure::NotSupported;
    SetFailure(
        failure == WhisperRecognizer::Failure::MicrophoneDenied
            ? Failure::MicrophoneDenied
            : Failure::Unknown );
    return false;
}

void DictationSession::StopWhisper( bool discard )
{
    if( !m_recognizer || !m_recognizer->whisper )
    {
        return;
    }

    const std::wstring text = m_recognizer->whisper->Stop( discard, m_cancelRequested );
    const auto failure = m_recognizer->whisper->GetFailure();
    if( failure != WhisperRecognizer::Failure::None )
    {
        SetFailure(
            failure == WhisperRecognizer::Failure::MicrophoneDenied
                ? Failure::MicrophoneDenied
                : Failure::Unknown );
        ReleaseWhisper();
        m_backend = Backend::None;
        m_whisperFailed = true;
    }
    const std::wstring trimmed = TrimCopy( text );
    bool changed = false;
    if( !discard && !trimmed.empty() )
    {
        std::lock_guard<std::mutex> guard( m_lock );
        if( !m_cancelRequested )
        {
            if( !m_text.empty() )
            {
                m_text += L' ';
            }
            m_text += trimmed;
            m_hypothesis.clear();
            changed = true;
        }
    }
    if( changed )
    {
        RaiseTextChanged();
    }
}

void DictationSession::ReleaseWhisper()
{
    if( m_recognizer && m_recognizer->whisper )
    {
        m_recognizer->whisper->Shutdown();
        m_recognizer->whisper.reset();
    }
}

//----------------------------------------------------------------------------
//
// Modern engine
//
//----------------------------------------------------------------------------
#ifdef ZOOMIT_WINAI_SPEECH

bool DictationSession::PrepareModern()
{
    using namespace winrt::Microsoft::Windows::AI;
    using namespace winrt::Microsoft::Windows::AI::Speech;

    try
    {
        if( SpeechRecognitionModel::GetReadyState() != AIFeatureReadyState::Ready )
        {
            // The optional model is downloaded from the settings page, never
            // in the middle of a snip.
            SetFailure( Failure::NotSupported );
            return false;
        }

        auto result = SpeechRecognitionModel::TryCreateAsync().get();
        if( result.SpeechModel() == nullptr )
        {
            SetFailure( Failure::NotSupported );
            return false;
        }

        if( !m_recognizer )
        {
            m_recognizer = std::make_unique<Recognizer>();
        }
        m_recognizer->model = result.SpeechModel();
        return true;
    }
    catch( const winrt::hresult_error& error )
    {
        OutputDebug( L"[Dictation] Modern prepare failed hr=0x%08x\n", error.code().value );
        SetFailure( Failure::Unknown );
        return false;
    }
}

bool DictationSession::StartModern()
{
    using namespace winrt::Microsoft::Windows::AI::Speech;

    if( !m_recognizer || !m_recognizer->model )
    {
        return false;
    }

    try
    {
        // AudioConfiguration identifies the input by device name, while ZoomIt
        // persists a device id, so resolve one to the other. An empty setting
        // means the system default device.
        winrt::hstring deviceName;
        if( g_MicrophoneDeviceId[0] != 0 )
        {
            auto information = winrt::Windows::Devices::Enumeration::DeviceInformation::CreateFromIdAsync(
                winrt::to_hstring( g_MicrophoneDeviceId ) ).get();
            if( information != nullptr )
            {
                deviceName = information.Name();
            }
        }

        auto configuration = deviceName.empty()
            ? AudioConfiguration::FromDefaultAudioDevice()
            : AudioConfiguration::FromAudioDevice( deviceName );

        m_recognizer->recognition = StreamingRecognition( configuration, m_recognizer->model );
        m_recognizer->recognition.Recognized( [this]( auto&&, auto&& args ) {
            AppendPhrase( std::wstring( args.Text() ) );
        } );

        m_recognizer->recognition.StartContinuousRecognitionAsync().get();
        return true;
    }
    catch( const winrt::hresult_error& error )
    {
        OutputDebug( L"[Dictation] Modern start failed hr=0x%08x\n", error.code().value );
        SetFailure( error.code() == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown );
        return false;
    }
}

void DictationSession::StopModern( bool discard )
{
    UNREFERENCED_PARAMETER( discard );
    try
    {
        m_recognizer->recognition.StopContinuousRecognition();
    }
    catch( const winrt::hresult_error& error )
    {
        OutputDebug( L"[Dictation] Modern stop failed hr=0x%08x\n", error.code().value );
    }
    m_recognizer->recognition = nullptr;
}

void DictationSession::ReleaseModern()
{
    if( m_recognizer )
    {
        m_recognizer->recognition = nullptr;
        m_recognizer->model = nullptr;
    }
}

#else // Inbox Windows.Media.SpeechRecognition

bool DictationSession::PrepareModern()
{
    using namespace winrt::Windows::Media::SpeechRecognition;

    try
    {
        auto recognizer = SpeechRecognizer();

        // Free form dictation rather than a fixed grammar.
        recognizer.Constraints().Append(
            SpeechRecognitionTopicConstraint( SpeechRecognitionScenario::Dictation, L"dictation" ) );

        // The caller decides when listening ends, so silence should not end the
        // session prematurely. These properties are best effort.
        try
        {
            auto timeouts = recognizer.Timeouts();
            timeouts.InitialSilenceTimeout( std::chrono::seconds( 20 ) );
            timeouts.EndSilenceTimeout( std::chrono::seconds( 2 ) );
        }
        catch( const winrt::hresult_error& )
        {
        }

        auto compilation = recognizer.CompileConstraintsAsync().get();
        if( compilation.Status() != SpeechRecognitionResultStatus::Success )
        {
            OutputDebug( L"[Dictation] CompileConstraints status=%d\n",
                         static_cast<int>(compilation.Status()) );
            SetFailure( compilation.Status() == SpeechRecognitionResultStatus::TopicLanguageNotSupported
                            ? Failure::LanguageNotSupported
                            : Failure::NotSupported );
            return false;
        }

        if( !m_recognizer )
        {
            m_recognizer = std::make_unique<Recognizer>();
        }

        m_recognizer->recognizer = recognizer;
        m_recognizer->hypothesisToken = recognizer.HypothesisGenerated(
            [this]( auto&&, auto&& args ) { SetHypothesis( std::wstring( args.Hypothesis().Text() ) ); } );

        m_recognizer->resultToken = recognizer.ContinuousRecognitionSession().ResultGenerated(
            [this]( auto&&, auto&& args ) {
                auto result = args.Result();
                if( result != nullptr &&
                    ( result.Status() == SpeechRecognitionResultStatus::Success ) )
                {
                    AppendPhrase( std::wstring( result.Text() ) );
                }
            } );

        return true;
    }
    catch( const winrt::hresult_error& error )
    {
        OutputDebug( L"[Dictation] Modern prepare failed hr=0x%08x\n", error.code().value );
        if( error.code() == SPERR_PRIVACY_POLICY_NOT_ACCEPTED )
        {
            SetFailure( Failure::PrivacyNotAccepted );
        }
        else if( error.code() == SPERR_MICROPHONE_NOT_FOUND )
        {
            SetFailure( Failure::MicrophoneDenied );
        }
        else
        {
            SetFailure( Failure::NotSupported );
        }
        return false;
    }
}

bool DictationSession::StartModern()
{
    if( !m_recognizer || !m_recognizer->recognizer )
    {
        return false;
    }

    try
    {
        m_recognizer->recognizer.ContinuousRecognitionSession().StartAsync().get();
        return true;
    }
    catch( const winrt::hresult_error& error )
    {
        // This is where a machine without online speech consent lands.
        OutputDebug( L"[Dictation] Modern start failed hr=0x%08x\n", error.code().value );
        if( error.code() == E_ACCESSDENIED )
        {
            SetFailure( Failure::MicrophoneDenied );
        }
        else if( error.code() == SPERR_PRIVACY_POLICY_NOT_ACCEPTED )
        {
            SetFailure( Failure::PrivacyNotAccepted );
        }
        else
        {
            SetFailure( Failure::Unknown );
        }
        return false;
    }
}

void DictationSession::StopModern( bool discard )
{
    try
    {
        auto session = m_recognizer->recognizer.ContinuousRecognitionSession();
        if( discard )
        {
            session.CancelAsync().get();
        }
        else
        {
            // StopAsync completes once the trailing audio has been recognized,
            // which is what turns the grace period into real transcription.
            session.StopAsync().get();
        }
    }
    catch( const winrt::hresult_error& error )
    {
        OutputDebug( L"[Dictation] Modern stop failed hr=0x%08x\n", error.code().value );
    }
}

void DictationSession::ReleaseModern()
{
    if( !m_recognizer || !m_recognizer->recognizer )
    {
        return;
    }

    try
    {
        m_recognizer->recognizer.HypothesisGenerated( m_recognizer->hypothesisToken );
        m_recognizer->recognizer.ContinuousRecognitionSession().ResultGenerated( m_recognizer->resultToken );
    }
    catch( const winrt::hresult_error& )
    {
    }

    m_recognizer->hypothesisToken = {};
    m_recognizer->resultToken = {};
    m_recognizer->recognizer = nullptr;
}

#endif // ZOOMIT_WINAI_SPEECH

//----------------------------------------------------------------------------
//
// SelectedMicrophoneIsSystemDefault
//
// Windows.Media.SpeechRecognition exposes no way to choose a capture device -
// SpeechRecognizer offers only CurrentLanguage, Constraints, Timeouts, State,
// UIOptions and ContinuousRecognitionSession - so it always records from the
// Windows default. When the user has pointed ZoomIt at some other microphone,
// running that engine would quietly record the wrong device and transcribe
// silence, which is far worse than the older engine's lower accuracy.
//
// The Record page stores a Windows.Devices.Enumeration identifier, which
// embeds the multimedia endpoint id, so a containment test against the default
// endpoint is enough to tell whether the two agree.
//
//----------------------------------------------------------------------------
static bool SelectedMicrophoneIsSystemDefault()
{
    // No explicit choice means "whatever Windows is using", which is exactly
    // what the modern engine would pick anyway.
    if( g_MicrophoneDeviceId[0] == 0 )
    {
        return true;
    }

    wil::com_ptr<IMMDeviceEnumerator> enumerator;
    HRESULT hr = CoCreateInstance( __uuidof( MMDeviceEnumerator ), nullptr, CLSCTX_ALL,
                                   IID_PPV_ARGS( enumerator.put() ) );
    if( FAILED( hr ) )
    {
        // Cannot tell, so do not strand the user on the weaker engine.
        return true;
    }

    std::wstring selected = g_MicrophoneDeviceId;
    std::transform( selected.begin(), selected.end(), selected.begin(), ::towlower );

    // SpeechRecognizer uses the normal default input (eConsole), not the
    // separate communications default used by calling applications.
    wil::com_ptr<IMMDevice> device;
    if( FAILED( enumerator->GetDefaultAudioEndpoint( eCapture, eConsole, device.put() ) ) )
    {
        return true;
    }

    wil::unique_cotaskmem_string id;
    if( FAILED( device->GetId( id.put() ) ) || !id )
    {
        return true;
    }

    std::wstring endpoint = id.get();
    std::transform( endpoint.begin(), endpoint.end(), endpoint.begin(), ::towlower );
    return endpoint.empty() || selected.find( endpoint ) != std::wstring::npos;
}

//----------------------------------------------------------------------------
//
// GetSapiTokenDescription
//
// SpGetDescription lives in sphelper.h, which drags in ATL and a number of
// deprecated APIs, so read the description directly instead. SAPI keeps it as
// the default value of the token key, optionally overridden per UI language.
//
//----------------------------------------------------------------------------
static bool GetSapiTokenDescription( ISpObjectToken* token, std::wstring& description )
{
    wchar_t languageKey[16] = {};
    swprintf_s( languageKey, L"%x", GetUserDefaultUILanguage() );

    wil::unique_cotaskmem_string value;
    if( FAILED( token->GetStringValue( languageKey, value.put() ) ) )
    {
        value.reset();
        if( FAILED( token->GetStringValue( nullptr, value.put() ) ) )
        {
            return false;
        }
    }

    description = value.get();
    return true;
}

//----------------------------------------------------------------------------
//
// ResolveAudioInputToken
//
// Picks the SAPI audio input token for the microphone selected on the Record
// page, so dictation and recording capture from the same device.
//
// SAPI identifies capture devices by its own token ids rather than by the
// Windows device id that ZoomIt persists, and there is no documented mapping
// between the two, so they are matched on the device's friendly name. Anything
// that does not resolve - including the empty "default device" setting and a
// microphone that has since been unplugged - falls back to SAPI's default, so
// dictation still works rather than failing outright.
//
//----------------------------------------------------------------------------
static HRESULT ResolveAudioInputToken( ISpObjectTokenCategory* category, ISpObjectToken** token )
{
    *token = nullptr;

    std::wstring wanted;
    if( g_MicrophoneDeviceId[0] != 0 )
    {
        try
        {
            auto information = winrt::Windows::Devices::Enumeration::DeviceInformation::CreateFromIdAsync(
                winrt::to_hstring( g_MicrophoneDeviceId ) ).get();
            if( information != nullptr )
            {
                wanted = information.Name();
            }
        }
        catch( const winrt::hresult_error& error )
        {
            OutputDebug( L"[Dictation] Microphone lookup failed hr=0x%08x\n", error.code().value );
        }
    }

    if( !wanted.empty() )
    {
        wil::com_ptr<IEnumSpObjectTokens> tokens;
        if( SUCCEEDED( category->EnumTokens( nullptr, nullptr, tokens.put() ) ) )
        {
            wil::com_ptr<ISpObjectToken> candidate;
            while( tokens->Next( 1, candidate.put(), nullptr ) == S_OK )
            {
                std::wstring description;
                if( GetSapiTokenDescription( candidate.get(), description ) &&
                    _wcsicmp( description.c_str(), wanted.c_str() ) == 0 )
                {
                    OutputDebug( L"[Dictation] Using microphone '%s'\n", description.c_str() );
                    *token = candidate.detach();
                    return S_OK;
                }

                candidate.reset();
            }
        }

        OutputDebug( L"[Dictation] No SAPI device matches '%s', falling back to the default\n",
                     wanted.c_str() );
    }

    wil::unique_cotaskmem_string defaultId;
    HRESULT hr = category->GetDefaultTokenId( defaultId.put() );
    if( FAILED( hr ) ) return hr;

    wil::com_ptr<ISpObjectToken> defaultToken;
    hr = CoCreateInstance( CLSID_SpObjectToken, nullptr, CLSCTX_ALL, IID_PPV_ARGS( defaultToken.put() ) );
    if( FAILED( hr ) ) return hr;

    hr = defaultToken->SetId( nullptr, defaultId.get(), FALSE );
    if( FAILED( hr ) ) return hr;

    *token = defaultToken.detach();
    return S_OK;
}

//----------------------------------------------------------------------------
//
// SAPI engine
//
// Entirely on device and available without any privacy consent, which is what
// lets dictation work the first time a user turns the feature on.
//
//----------------------------------------------------------------------------
bool DictationSession::PrepareSapi()
{
    if( !m_recognizer )
    {
        m_recognizer = std::make_unique<Recognizer>();
    }

    if( m_recognizer->sapiGrammar )
    {
        return true;
    }

    auto fail = [this]( const wchar_t* what, HRESULT hr ) {
        OutputDebug( L"[Dictation] SAPI %s failed hr=0x%08x\n", what, hr );
        SetFailure( hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::NotSupported );
        return false;
    };

    HRESULT hr = CoCreateInstance( CLSID_SpInprocRecognizer, nullptr, CLSCTX_INPROC_SERVER,
                                   IID_PPV_ARGS( m_recognizer->sapiRecognizer.put() ) );
    if( FAILED( hr ) ) return fail( L"CoCreateInstance", hr );

    //
    // The in process recognizer has no audio source until one is set, and
    // passing a null token does not pick one, so resolve the capture device
    // explicitly. Without this the engine runs but never hears anything.
    //
    wil::com_ptr<ISpObjectTokenCategory> category;
    hr = CoCreateInstance( CLSID_SpObjectTokenCategory, nullptr, CLSCTX_ALL,
                           IID_PPV_ARGS( category.put() ) );
    if( FAILED( hr ) ) return fail( L"SpObjectTokenCategory", hr );

    hr = category->SetId( SPCAT_AUDIOIN, FALSE );
    if( FAILED( hr ) ) return fail( L"SetId(SPCAT_AUDIOIN)", hr );

    wil::com_ptr<ISpObjectToken> audioToken;
    hr = ResolveAudioInputToken( category.get(), audioToken.put() );
    if( FAILED( hr ) ) return fail( L"ResolveAudioInputToken", hr );

    hr = m_recognizer->sapiRecognizer->SetInput( audioToken.get(), TRUE );
    if( FAILED( hr ) ) return fail( L"SetInput", hr );

    hr = m_recognizer->sapiRecognizer->CreateRecoContext( m_recognizer->sapiContext.put() );
    if( FAILED( hr ) ) return fail( L"CreateRecoContext", hr );

    hr = m_recognizer->sapiContext->SetNotifyWin32Event();
    if( FAILED( hr ) ) return fail( L"SetNotifyWin32Event", hr );

    constexpr ULONGLONG interest = SPFEI( SPEI_RECOGNITION ) | SPFEI( SPEI_HYPOTHESIS );
    hr = m_recognizer->sapiContext->SetInterest( interest, interest );
    if( FAILED( hr ) ) return fail( L"SetInterest", hr );

    hr = m_recognizer->sapiContext->CreateGrammar( 1, m_recognizer->sapiGrammar.put() );
    if( FAILED( hr ) ) return fail( L"CreateGrammar", hr );

    hr = m_recognizer->sapiGrammar->LoadDictation( nullptr, SPLO_STATIC );
    if( FAILED( hr ) )
    {
        m_recognizer->sapiGrammar.reset();
        return fail( L"LoadDictation", hr );
    }

    // Stay inactive until the user actually asks to dictate so that preparing
    // the engine never opens the microphone.
    m_recognizer->sapiRecognizer->SetRecoState( SPRST_INACTIVE );
    return true;
}

bool DictationSession::StartSapi()
{
    if( !m_recognizer || !m_recognizer->sapiGrammar )
    {
        return false;
    }

    {
        std::lock_guard<std::mutex> guard( m_lock );
        m_hypothesis.clear();
    }

    // Discard anything the engine buffered before this drag.
    DrainSapiEvents();

    HRESULT hr = m_recognizer->sapiGrammar->SetDictationState( SPRS_ACTIVE );
    if( FAILED( hr ) )
    {
        OutputDebug( L"[Dictation] SAPI SetDictationState failed hr=0x%08x\n", hr );
        SetFailure( Failure::Unknown );
        return false;
    }

    hr = m_recognizer->sapiRecognizer->SetRecoState( SPRST_ACTIVE );
    if( FAILED( hr ) )
    {
        OutputDebug( L"[Dictation] SAPI SetRecoState failed hr=0x%08x\n", hr );
        m_recognizer->sapiGrammar->SetDictationState( SPRS_INACTIVE );
        SetFailure( hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown );
        return false;
    }

    m_recognizer->sapiQuit.create( wil::EventOptions::ManualReset );
    m_recognizer->sapiEvents = std::thread( [this] { SapiEventLoop(); } );
    return true;
}

void DictationSession::StopSapi( bool discard )
{
    //
    // Deactivating the grammar makes SAPI finalize whatever it has heard, so
    // the trailing words spoken just before the mouse was released still turn
    // into a recognition event. Those events are collected below.
    //
    if( m_recognizer->sapiGrammar )
    {
        m_recognizer->sapiGrammar->SetDictationState( SPRS_INACTIVE );
    }
    if( m_recognizer->sapiRecognizer )
    {
        m_recognizer->sapiRecognizer->SetRecoState( SPRST_INACTIVE );
    }

    if( m_recognizer->sapiQuit )
    {
        m_recognizer->sapiQuit.SetEvent();
    }
    if( m_recognizer->sapiEvents.joinable() )
    {
        m_recognizer->sapiEvents.join();
    }
    m_recognizer->sapiQuit.reset();

    if( discard )
    {
        DrainSapiEvents();
    }
    else
    {
        // Collect the final recognition that deactivation flushed.
        SPEVENT event{};
        ULONG fetched = 0;
        while( m_recognizer->sapiContext->GetEvents( 1, &event, &fetched ) == S_OK && fetched != 0 )
        {
            if( event.eEventId == SPEI_RECOGNITION && event.lParam != 0 )
            {
                auto* result = reinterpret_cast<ISpRecoResult*>( event.lParam );
                wil::unique_cotaskmem_string text;
                if( SUCCEEDED( result->GetText( SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, TRUE, text.put(), nullptr ) ) )
                {
                    AppendPhrase( text.get() );
                }
            }

            if( event.elParamType == SPET_LPARAM_IS_OBJECT && event.lParam != 0 )
            {
                reinterpret_cast<IUnknown*>( event.lParam )->Release();
            }
            else if( ( event.elParamType == SPET_LPARAM_IS_POINTER ||
                       event.elParamType == SPET_LPARAM_IS_STRING ) && event.lParam != 0 )
            {
                CoTaskMemFree( reinterpret_cast<void*>( event.lParam ) );
            }
            event = {};
            fetched = 0;
        }

        // Short dictations commonly have a hypothesis but no final recognition
        // event by the time the mouse is released. Deactivation does not
        // guarantee that SAPI promotes it to SPEI_RECOGNITION, so preserve the
        // user's last words rather than returning an empty clipboard payload.
        std::wstring hypothesis;
        {
            std::lock_guard<std::mutex> guard( m_lock );
            if( m_text.empty() )
            {
                hypothesis = m_hypothesis;
            }
        }
        if( !hypothesis.empty() )
        {
            AppendPhrase( hypothesis );
        }
    }
}

//
// Runs while the microphone is open, turning SAPI events into transcription
// updates so the badge shows words as they are recognized.
//
void DictationSession::SapiEventLoop()
{
    //
    // GetEvents is a COM call, so this thread must join an apartment before it
    // touches the context. It has to be the same multithreaded apartment the
    // context was created in, otherwise every call would need marshalling.
    //
    const HRESULT hr = CoInitializeEx( nullptr, COINIT_MULTITHREADED );
    if( FAILED( hr ) )
    {
        OutputDebug( L"[Dictation] SAPI event thread CoInitializeEx failed hr=0x%08x\n", hr );
        return;
    }
    auto uninitialize = wil::scope_exit( [] { CoUninitialize(); } );

    HANDLE notify = m_recognizer->sapiContext->GetNotifyEventHandle();
    if( notify == INVALID_HANDLE_VALUE )
    {
        OutputDebug( L"[Dictation] SAPI notify handle unavailable\n" );
        return;
    }

    HANDLE waits[] = { notify, m_recognizer->sapiQuit.get() };
    for( ;; )
    {
        const DWORD wait = WaitForMultipleObjects( ARRAYSIZE( waits ), waits, FALSE, INFINITE );
        if( wait != WAIT_OBJECT_0 )
        {
            // Quit signalled, or the wait failed. Remaining events are drained
            // by StopSapi on the worker thread.
            return;
        }

        SPEVENT event{};
        ULONG fetched = 0;
        HRESULT events = S_OK;
        while( ( events = m_recognizer->sapiContext->GetEvents( 1, &event, &fetched ) ) == S_OK &&
               fetched != 0 )
        {
            if( ( event.eEventId == SPEI_RECOGNITION || event.eEventId == SPEI_HYPOTHESIS ) &&
                event.lParam != 0 )
            {
                auto* result = reinterpret_cast<ISpRecoResult*>( event.lParam );
                wil::unique_cotaskmem_string text;
                if( SUCCEEDED( result->GetText( SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, TRUE, text.put(), nullptr ) ) )
                {
                    if( event.eEventId == SPEI_RECOGNITION )
                    {
                        AppendPhrase( text.get() );
                    }
                    else
                    {
                        SetHypothesis( text.get() );
                    }
                }
            }

            if( event.elParamType == SPET_LPARAM_IS_OBJECT && event.lParam != 0 )
            {
                reinterpret_cast<IUnknown*>( event.lParam )->Release();
            }
            else if( ( event.elParamType == SPET_LPARAM_IS_POINTER ||
                       event.elParamType == SPET_LPARAM_IS_STRING ) && event.lParam != 0 )
            {
                CoTaskMemFree( reinterpret_cast<void*>( event.lParam ) );
            }
            event = {};
            fetched = 0;
        }
        if( FAILED( events ) )
        {
            OutputDebug( L"[Dictation] SAPI GetEvents failed hr=0x%08x\n", events );
            return;
        }
    }
}

void DictationSession::DrainSapiEvents()
{
    if( !m_recognizer || !m_recognizer->sapiContext )
    {
        return;
    }

    SPEVENT event{};
    ULONG fetched = 0;
    while( m_recognizer->sapiContext->GetEvents( 1, &event, &fetched ) == S_OK && fetched != 0 )
    {
        if( event.elParamType == SPET_LPARAM_IS_OBJECT && event.lParam != 0 )
        {
            reinterpret_cast<IUnknown*>( event.lParam )->Release();
        }
        else if( ( event.elParamType == SPET_LPARAM_IS_POINTER ||
                   event.elParamType == SPET_LPARAM_IS_STRING ) && event.lParam != 0 )
        {
            CoTaskMemFree( reinterpret_cast<void*>( event.lParam ) );
        }
        event = {};
        fetched = 0;
    }
}

void DictationSession::ReleaseSapi()
{
    if( !m_recognizer )
    {
        return;
    }

    if( m_recognizer->sapiQuit )
    {
        m_recognizer->sapiQuit.SetEvent();
    }
    if( m_recognizer->sapiEvents.joinable() )
    {
        m_recognizer->sapiEvents.join();
    }
    m_recognizer->sapiQuit.reset();

    if( m_recognizer->sapiGrammar )
    {
        m_recognizer->sapiGrammar->SetDictationState( SPRS_INACTIVE );
    }
    if( m_recognizer->sapiRecognizer )
    {
        m_recognizer->sapiRecognizer->SetRecoState( SPRST_INACTIVE );
    }

    DrainSapiEvents();

    m_recognizer->sapiGrammar.reset();
    m_recognizer->sapiContext.reset();
    m_recognizer->sapiRecognizer.reset();
}
