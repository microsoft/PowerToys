//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Video capture code derived from https://github.com/robmikh/capturevideosample
//
//==============================================================================
#include "pch.h"
#include "CaptureFrameWait.h"

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Graphics;
    using namespace Windows::Graphics::Capture;
    using namespace Windows::Graphics::DirectX;
    using namespace Windows::Graphics::DirectX::Direct3D11;
    using namespace Windows::Storage;
    using namespace Windows::UI::Composition;
}

namespace util
{
    using namespace robmikh::common::uwp;
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::CaptureFrameWait
//
//----------------------------------------------------------------------------
CaptureFrameWait::CaptureFrameWait(
    winrt::IDirect3DDevice const& device,
    winrt::GraphicsCaptureItem const& item,
    winrt::SizeInt32 const& size,
    bool borderRequired)
{
    m_device = device;
    m_item = item;

    m_nextFrameEvent = wil::shared_event(wil::EventOptions::ManualReset);
    m_endEvent = wil::shared_event(wil::EventOptions::ManualReset);

    m_framePool = winrt::Direct3D11CaptureFramePool::CreateFreeThreaded(
        m_device,
        winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        FrameBufferCount,
        size);
    m_session = m_framePool.CreateCaptureSession(m_item);

    if (!borderRequired)
    {
        ShowCaptureBorder(false);
    }

    m_frameArrived = m_framePool.FrameArrived(winrt::auto_revoke, { this, &CaptureFrameWait::OnFrameArrived });
    m_session.StartCapture();
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::~CaptureFrameWait
//
//----------------------------------------------------------------------------
CaptureFrameWait::~CaptureFrameWait()
{
    StopCapture();
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::TryGetNextFrame
//
// Fetches next available frame
//
//----------------------------------------------------------------------------
std::optional<CaptureFrame> CaptureFrameWait::TryGetNextFrame()
{
    return TryGetNextFrame(INFINITE);
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::PeekCurrentFrame
//
// Returns the oldest available frame without removing it from the queue.
//
//----------------------------------------------------------------------------
std::optional<CaptureFrame> CaptureFrameWait::PeekCurrentFrame() const
{
    auto lock = m_lock.lock_exclusive();
    if (!m_pendingFrames.empty() && !m_endEvent.is_signaled())
    {
        return m_pendingFrames.front();
    }
    return std::nullopt;
}

void CaptureFrameWait::DiscardPendingFrames()
{
    auto lock = m_lock.lock_exclusive();
    m_pendingFrames.clear();
    m_nextFrameEvent.ResetEvent();
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::TryGetNextFrame (with timeout)
//
// Like TryGetNextFrame() but returns nullopt on timeout instead of
// blocking forever.  The caller can reuse a cached texture when
// nullopt is returned.
//
//----------------------------------------------------------------------------
std::optional<CaptureFrame> CaptureFrameWait::TryGetNextFrame(DWORD timeoutMs)
{
    const HANDLE events[] = { m_endEvent.get(), m_nextFrameEvent.get() };
    const auto waitResult = WaitForMultipleObjectsEx(
        ARRAYSIZE(events), events, false, timeoutMs, false);
    if (waitResult == WAIT_FAILED)
    {
        winrt::throw_last_error();
    }
    if (waitResult == WAIT_TIMEOUT)
    {
        return std::nullopt;
    }

    auto lock = m_lock.lock_exclusive();
    if (m_endEvent.is_signaled() || m_pendingFrames.empty())
    {
        return std::nullopt;
    }

    auto frame = std::move(m_pendingFrames.front());
    m_pendingFrames.pop_front();
    if (m_pendingFrames.empty())
    {
        // Reset under the producer's lock, never after it signals a new frame.
        m_nextFrameEvent.ResetEvent();
    }
    return frame;
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::StopCapture
//
// Stops frame capture and notified any frame waiters
//
//----------------------------------------------------------------------------
void CaptureFrameWait::StopCapture()
{
    std::call_once(m_stopOnce, [this] {
        {
            auto lock = m_lock.lock_exclusive();
            m_endEvent.SetEvent();
            m_pendingFrames.clear();
        }
        // Closing/revoking can wait for a callback; do not hold its lock here.
        m_frameArrived.revoke();
        m_session.Close();
        m_framePool.Close();
    });
}

//----------------------------------------------------------------------------
//
// CaptureFrameWait::OnFrameArrived
//
// Callback for new frames
//
//----------------------------------------------------------------------------
void CaptureFrameWait::OnFrameArrived(
    winrt::Direct3D11CaptureFramePool const& sender,
    winrt::IInspectable const&)
{
    auto lock = m_lock.lock_exclusive();
    if (m_endEvent.is_signaled())
    {
        return;
    }
    auto frame = sender.TryGetNextFrame();
    if (frame)
    {
        auto owner = std::make_shared<CaptureFrame::Lease>(std::move(frame));
        m_pendingFrames.push_back(
            { owner->Frame.Surface(), owner->Frame.ContentSize(), owner->Frame.SystemRelativeTime(), owner });
        m_nextFrameEvent.SetEvent();
    }
}
