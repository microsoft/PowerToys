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
    winrt::SizeInt32 const& size)
{
    m_device = device;
    m_item = item;

    m_nextFrameEvent = wil::shared_event(wil::EventOptions::ManualReset);
    m_endEvent = wil::shared_event(wil::EventOptions::ManualReset);
    m_closedEvent = wil::shared_event(wil::EventOptions::ManualReset);

    m_framePool = winrt::Direct3D11CaptureFramePool::CreateFreeThreaded(
        m_device,
        winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        1,
        size);
    m_session = m_framePool.CreateCaptureSession(m_item);

    m_framePool.FrameArrived({ this, &CaptureFrameWait::OnFrameArrived });
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
    // We might end the capture before we ever get another frame.
    m_closedEvent.wait(200);
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
    if (m_currentFrame != nullptr)
    {
        m_currentFrame.Close();
    }
    m_nextFrameEvent.ResetEvent();

    std::vector<HANDLE> events = { m_endEvent.get(), m_nextFrameEvent.get() };
    auto waitResult = WaitForMultipleObjectsEx(static_cast<DWORD>(events.size()), events.data(), false, INFINITE, false);
    auto eventIndex = -1;
    switch (waitResult)
    {
    case WAIT_OBJECT_0:
    case WAIT_OBJECT_0 + 1:
        eventIndex = waitResult - WAIT_OBJECT_0;
        break;
    }
    WINRT_VERIFY(eventIndex >= 0);

    auto signaledEvent = events[eventIndex];
    if (signaledEvent == m_endEvent.get())
    {
        return std::nullopt;
    }

    return std::optional<CaptureFrame>(
        {
            m_currentFrame.Surface(),
            m_currentFrame.ContentSize(),
            m_currentFrame.SystemRelativeTime(),
        });
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
    auto lock = m_lock.lock_exclusive();
    m_endEvent.SetEvent();
    m_framePool.Close();
    m_session.Close();
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
        m_closedEvent.SetEvent();
        return;
    }
    auto frame = sender.TryGetNextFrame();
    if( frame ) {
        m_currentFrame = frame;
        m_nextFrameEvent.SetEvent();
    }
}
