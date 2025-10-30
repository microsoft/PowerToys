//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// GIF recording support using Windows Imaging Component (WIC)
//
//==============================================================================
#include "pch.h"
#include "GifRecordingSession.h"
#include "CaptureFrameWait.h"
#include <shcore.h>

extern DWORD g_RecordScaling;

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

const float CLEAR_COLOR[] = { 0.0f, 0.0f, 0.0f, 1.0f };

int32_t EnsureEvenGif(int32_t value)
{
    if (value % 2 == 0)
    {
        return value;
    }
    else
    {
        return value + 1;
    }
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::GifRecordingSession
//
//----------------------------------------------------------------------------
GifRecordingSession::GifRecordingSession(
    winrt::IDirect3DDevice const& device,
    winrt::GraphicsCaptureItem const& item,
    RECT const cropRect,
    uint32_t frameRate,
    winrt::Streams::IRandomAccessStream const& stream)
{
    m_device = device;
    m_d3dDevice = GetDXGIInterfaceFromObject<ID3D11Device>(m_device);
    m_d3dDevice->GetImmediateContext(m_d3dContext.put());
    m_item = item;
    m_frameRate = frameRate;
    m_stream = stream;

    auto itemSize = item.Size();
    auto inputWidth = EnsureEvenGif(itemSize.Width);
    auto inputHeight = EnsureEvenGif(itemSize.Height);
    m_frameWait = std::make_shared<CaptureFrameWait>(m_device, m_item, winrt::SizeInt32{ inputWidth, inputHeight });
    auto weakPointer{ std::weak_ptr{ m_frameWait } };
    m_itemClosed = item.Closed(winrt::auto_revoke, [weakPointer](auto&, auto&)
        {
            auto sharedPointer{ weakPointer.lock() };
            if (sharedPointer)
            {
                sharedPointer->StopCapture();
            }
        });

    // Get crop dimension
    if ((cropRect.right - cropRect.left) != 0)
    {
        m_rcCrop = cropRect;
        m_frameWait->ShowCaptureBorder(false);
    }
    else
    {
        m_rcCrop.left = 0;
        m_rcCrop.top = 0;
        m_rcCrop.right = inputWidth;
        m_rcCrop.bottom = inputHeight;
    }

    // Apply scaling
    constexpr int c_minimumSize = 34;
    auto scaledWidth = MulDiv(m_rcCrop.right - m_rcCrop.left, g_RecordScaling, 100);
    auto scaledHeight = MulDiv(m_rcCrop.bottom - m_rcCrop.top, g_RecordScaling, 100);
    m_width = scaledWidth;
    m_height = scaledHeight;
    if (m_width < c_minimumSize)
    {
        m_width = c_minimumSize;
        m_height = MulDiv(m_height, m_width, scaledWidth);
    }
    if (m_height < c_minimumSize)
    {
        m_height = c_minimumSize;
        m_width = MulDiv(m_width, m_height, scaledHeight);
    }
    if (m_width > inputWidth)
    {
        m_width = inputWidth;
        m_height = c_minimumSize, MulDiv(m_height, scaledWidth, m_width);
    }
    if (m_height > inputHeight)
    {
        m_height = inputHeight;
        m_width = c_minimumSize, MulDiv(m_width, scaledHeight, m_height);
    }
    m_width = EnsureEvenGif(m_width);
    m_height = EnsureEvenGif(m_height);

    m_frameDelay = (frameRate > 0) ? (100 / frameRate) : 15;

    // Initialize WIC
    winrt::check_hresult(CoCreateInstance(
        CLSID_WICImagingFactory,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(m_wicFactory.put())));

    // Create WIC stream from IRandomAccessStream
    winrt::check_hresult(m_wicFactory->CreateStream(m_wicStream.put()));

    // Get the IStream from the IRandomAccessStream
    winrt::com_ptr<IStream> streamInterop;
    winrt::check_hresult(CreateStreamOverRandomAccessStream(
        winrt::get_unknown(stream),
        IID_PPV_ARGS(streamInterop.put())));
    winrt::check_hresult(m_wicStream->InitializeFromIStream(streamInterop.get()));

    // Create GIF encoder
    winrt::check_hresult(m_wicFactory->CreateEncoder(
        GUID_ContainerFormatGif,
        nullptr,
        m_gifEncoder.put()));

    winrt::check_hresult(m_gifEncoder->Initialize(m_wicStream.get(), WICBitmapEncoderNoCache));

    // Set global GIF metadata for looping (NETSCAPE2.0 application extension)
    try
    {
        winrt::com_ptr<IWICMetadataQueryWriter> encoderMetadataWriter;
        if (SUCCEEDED(m_gifEncoder->GetMetadataQueryWriter(encoderMetadataWriter.put())) && encoderMetadataWriter)
        {
            OutputDebugStringW(L"Setting NETSCAPE2.0 looping extension on encoder...\n");

            // Set application extension
            PROPVARIANT propValue;
            PropVariantInit(&propValue);
            propValue.vt = VT_UI1 | VT_VECTOR;
            propValue.caub.cElems = 11;
            propValue.caub.pElems = static_cast<UCHAR*>(CoTaskMemAlloc(11));
            if (propValue.caub.pElems != nullptr)
            {
                memcpy(propValue.caub.pElems, "NETSCAPE2.0", 11);
                HRESULT hr = encoderMetadataWriter->SetMetadataByName(L"/appext/application", &propValue);
                if (SUCCEEDED(hr))
                {
                    OutputDebugStringW(L"Encoder application extension set successfully\n");
                }
                else
                {
                    OutputDebugStringW(L"Failed to set encoder application extension\n");
                }
                PropVariantClear(&propValue);

                // Set loop count (0 = infinite)
                PropVariantInit(&propValue);
                propValue.vt = VT_UI1 | VT_VECTOR;
                propValue.caub.cElems = 5;
                propValue.caub.pElems = static_cast<UCHAR*>(CoTaskMemAlloc(5));
                if (propValue.caub.pElems != nullptr)
                {
                    propValue.caub.pElems[0] = 3;
                    propValue.caub.pElems[1] = 1;
                    propValue.caub.pElems[2] = 0;
                    propValue.caub.pElems[3] = 0;
                    propValue.caub.pElems[4] = 0;
                    hr = encoderMetadataWriter->SetMetadataByName(L"/appext/data", &propValue);
                    if (SUCCEEDED(hr))
                    {
                        OutputDebugStringW(L"Encoder loop count set successfully\n");
                    }
                    else
                    {
                        OutputDebugStringW(L"Failed to set encoder loop count\n");
                    }
                    PropVariantClear(&propValue);
                }
            }
        }
        else
        {
            OutputDebugStringW(L"Failed to get encoder metadata writer\n");
        }
    }
    catch (...)
    {
        OutputDebugStringW(L"Warning: Failed to set GIF encoder looping metadata\n");
    }
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::~GifRecordingSession
//
//----------------------------------------------------------------------------
GifRecordingSession::~GifRecordingSession()
{
    Close();
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::Create
//
//----------------------------------------------------------------------------
std::shared_ptr<GifRecordingSession> GifRecordingSession::Create(
    winrt::IDirect3DDevice const& device,
    winrt::GraphicsCaptureItem const& item,
    RECT const& crop,
    uint32_t frameRate,
    winrt::Streams::IRandomAccessStream const& stream)
{
    return std::shared_ptr<GifRecordingSession>(new GifRecordingSession(device, item, crop, frameRate, stream));
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::EncodeFrame
//
//----------------------------------------------------------------------------
HRESULT GifRecordingSession::EncodeFrame(ID3D11Texture2D* frameTexture)
{
    try
    {
        // Create a staging texture for CPU access
        D3D11_TEXTURE2D_DESC frameDesc;
        frameTexture->GetDesc(&frameDesc);

        // GIF encoding with palette generation is VERY slow at high resolutions (4K takes 1 second per frame!)

        UINT targetWidth = frameDesc.Width;
        UINT targetHeight = frameDesc.Height;

        if (frameDesc.Width > static_cast<uint32_t>(m_width) || frameDesc.Height > static_cast<uint32_t>(m_height))
        {
            float scaleX = static_cast<float>(m_width) / frameDesc.Width;
            float scaleY = static_cast<float>(m_height) / frameDesc.Height;
            float scale = min(scaleX, scaleY);

            targetWidth = static_cast<UINT>(frameDesc.Width * scale);
            targetHeight = static_cast<UINT>(frameDesc.Height * scale);

            // Ensure even dimensions for GIF
            targetWidth = (targetWidth / 2) * 2;
            targetHeight = (targetHeight / 2) * 2;
        }

        D3D11_TEXTURE2D_DESC stagingDesc = frameDesc;
        stagingDesc.Usage = D3D11_USAGE_STAGING;
        stagingDesc.BindFlags = 0;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.MiscFlags = 0;

        winrt::com_ptr<ID3D11Texture2D> stagingTexture;
        winrt::check_hresult(m_d3dDevice->CreateTexture2D(&stagingDesc, nullptr, stagingTexture.put()));

        // Copy the frame to staging texture
        m_d3dContext->CopyResource(stagingTexture.get(), frameTexture);

        // Map the staging texture
        D3D11_MAPPED_SUBRESOURCE mappedResource;
        winrt::check_hresult(m_d3dContext->Map(stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mappedResource));

        // Create a new frame in the GIF
        winrt::com_ptr<IWICBitmapFrameEncode> frameEncode;
        winrt::com_ptr<IPropertyBag2> propertyBag;
        winrt::check_hresult(m_gifEncoder->CreateNewFrame(frameEncode.put(), propertyBag.put()));

        // Initialize the frame encoder with property bag
        winrt::check_hresult(frameEncode->Initialize(propertyBag.get()));

        // CRITICAL: For GIF, we MUST set size and pixel format BEFORE WriteSource
        // Use target dimensions (may be downsampled)
        winrt::check_hresult(frameEncode->SetSize(targetWidth, targetHeight));

        // Set the pixel format to 8-bit indexed (required for GIF)
        WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat8bppIndexed;
        winrt::check_hresult(frameEncode->SetPixelFormat(&pixelFormat));

        // Create a WIC bitmap from the BGRA texture data
        winrt::com_ptr<IWICBitmap> sourceBitmap;
        winrt::check_hresult(m_wicFactory->CreateBitmapFromMemory(
            frameDesc.Width,
            frameDesc.Height,
            GUID_WICPixelFormat32bppBGRA,
            mappedResource.RowPitch,
            frameDesc.Height * mappedResource.RowPitch,
            static_cast<BYTE*>(mappedResource.pData),
            sourceBitmap.put()));

        // If we need downsampling, use WIC scaler
        winrt::com_ptr<IWICBitmapSource> finalSource = sourceBitmap;
        if (targetWidth != frameDesc.Width || targetHeight != frameDesc.Height)
        {
            winrt::com_ptr<IWICBitmapScaler> scaler;
            winrt::check_hresult(m_wicFactory->CreateBitmapScaler(scaler.put()));
            winrt::check_hresult(scaler->Initialize(
                sourceBitmap.get(),
                targetWidth,
                targetHeight,
                WICBitmapInterpolationModeHighQualityCubic));
            finalSource = scaler;

            OutputDebugStringW((L"Downsampled from " + std::to_wstring(frameDesc.Width) + L"x" + std::to_wstring(frameDesc.Height) +
                               L" to " + std::to_wstring(targetWidth) + L"x" + std::to_wstring(targetHeight) + L"\n").c_str());
        }

        // Use WriteSource - WIC will handle the BGRA to 8bpp indexed conversion
        winrt::check_hresult(frameEncode->WriteSource(finalSource.get(), nullptr));

        try
        {
            winrt::com_ptr<IWICMetadataQueryWriter> frameMetadataWriter;
            if (SUCCEEDED(frameEncode->GetMetadataQueryWriter(frameMetadataWriter.put())) && frameMetadataWriter)
            {
                // Set the frame delay in the metadata (in hundredths of a second)
                PROPVARIANT propValue;
                PropVariantInit(&propValue);
                propValue.vt = VT_UI2;
                propValue.uiVal = static_cast<USHORT>(m_frameDelay);
                frameMetadataWriter->SetMetadataByName(L"/grctlext/Delay", &propValue);
                PropVariantClear(&propValue);

                // Set disposal method (2 = restore to background, needed for animation)
                PropVariantInit(&propValue);
                propValue.vt = VT_UI1;
                propValue.bVal = 2; // Disposal method: restore to background color
                frameMetadataWriter->SetMetadataByName(L"/grctlext/Disposal", &propValue);
                PropVariantClear(&propValue);
            }
        }
        catch (...)
        {
            // Metadata setting failed, continue anyway
            OutputDebugStringW(L"Warning: Failed to set GIF frame metadata\n");
        }

        // Commit the frame
        OutputDebugStringW(L"About to commit frame to encoder...\n");
        winrt::check_hresult(frameEncode->Commit());
        OutputDebugStringW(L"Frame committed successfully\n");

        // Unmap the staging texture
        m_d3dContext->Unmap(stagingTexture.get(), 0);

        // Increment and log frame count
        m_frameCount++;
        OutputDebugStringW((L"GIF Frame #" + std::to_wstring(m_frameCount) + L" fully encoded and committed\n").c_str());

        return S_OK;
    }
    catch (const winrt::hresult_error& error)
    {
        OutputDebugStringW(error.message().c_str());
        return error.code();
    }
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::StartAsync
//
//----------------------------------------------------------------------------
winrt::IAsyncAction GifRecordingSession::StartAsync()
{
    auto expected = false;
    if (m_isRecording.compare_exchange_strong(expected, true))
    {
        auto self = shared_from_this();

        try
        {
            // Start capturing frames
            auto frameStartTime = std::chrono::high_resolution_clock::now();
            int captureAttempts = 0;
            int successfulCaptures = 0;
            int duplicatedFrames = 0;

            // Keep track of the last frame to duplicate when needed
            winrt::com_ptr<ID3D11Texture2D> lastCroppedTexture;

            while (m_isRecording && !m_closed)
            {
                captureAttempts++;
                auto frame = m_frameWait->TryGetNextFrame();

                winrt::com_ptr<ID3D11Texture2D> croppedTexture;

                if (frame)
                {
                    successfulCaptures++;
                    auto contentSize = frame->ContentSize;
                    auto frameTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(frame->FrameTexture);
                    D3D11_TEXTURE2D_DESC desc = {};
                    frameTexture->GetDesc(&desc);

                    // Use the smaller of the crop size or content size
                    auto width = min(m_rcCrop.right - m_rcCrop.left, contentSize.Width);
                    auto height = min(m_rcCrop.bottom - m_rcCrop.top, contentSize.Height);

                    D3D11_TEXTURE2D_DESC croppedDesc = {};
                    croppedDesc.Width = width;
                    croppedDesc.Height = height;
                    croppedDesc.MipLevels = 1;
                    croppedDesc.ArraySize = 1;
                    croppedDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                    croppedDesc.SampleDesc.Count = 1;
                    croppedDesc.Usage = D3D11_USAGE_DEFAULT;
                    croppedDesc.BindFlags = D3D11_BIND_RENDER_TARGET;

                    winrt::check_hresult(m_d3dDevice->CreateTexture2D(&croppedDesc, nullptr, croppedTexture.put()));

                    // Set the content region to copy and clamp the coordinates
                    D3D11_BOX region = {};
                    region.left = std::clamp(m_rcCrop.left, static_cast<LONG>(0), static_cast<LONG>(desc.Width));
                    region.right = std::clamp(m_rcCrop.left + width, static_cast<LONG>(0), static_cast<LONG>(desc.Width));
                    region.top = std::clamp(m_rcCrop.top, static_cast<LONG>(0), static_cast<LONG>(desc.Height));
                    region.bottom = std::clamp(m_rcCrop.top + height, static_cast<LONG>(0), static_cast<LONG>(desc.Height));
                    region.back = 1;

                    // Copy the cropped region
                    m_d3dContext->CopySubresourceRegion(
                        croppedTexture.get(),
                        0,
                        0, 0, 0,
                        frameTexture.get(),
                        0,
                        &region);

                    // Save this as the last frame for duplication
                    lastCroppedTexture = croppedTexture;
                }
                else if (lastCroppedTexture)
                {
                    // No new frame, duplicate the last one
                    duplicatedFrames++;
                    croppedTexture = lastCroppedTexture;
                }

                // Encode the frame (either new or duplicated)
                if (croppedTexture)
                {
                    HRESULT hr = EncodeFrame(croppedTexture.get());
                    if (FAILED(hr))
                    {
                        CloseInternal();
                        break;
                    }
                }

                // Wait for the next frame interval
                co_await winrt::resume_after(std::chrono::milliseconds(1000 / m_frameRate));
            }

            // Commit the GIF encoder
            if (m_gifEncoder)
            {
                auto frameEndTime = std::chrono::high_resolution_clock::now();
                auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(frameEndTime - frameStartTime).count();

                OutputDebugStringW(L"Recording stopped. Committing GIF encoder...\n");
                OutputDebugStringW((L"Total frames captured: " + std::to_wstring(m_frameCount) + L"\n").c_str());
                OutputDebugStringW((L"Capture attempts: " + std::to_wstring(captureAttempts) + L"\n").c_str());
                OutputDebugStringW((L"Successful captures: " + std::to_wstring(successfulCaptures) + L"\n").c_str());
                OutputDebugStringW((L"Duplicated frames: " + std::to_wstring(duplicatedFrames) + L"\n").c_str());
                OutputDebugStringW((L"Recording duration: " + std::to_wstring(duration) + L"ms\n").c_str());
                OutputDebugStringW((L"Actual FPS: " + std::to_wstring(m_frameCount * 1000.0 / duration) + L"\n").c_str());

                winrt::check_hresult(m_gifEncoder->Commit());
                OutputDebugStringW(L"GIF encoder committed successfully\n");
            }
        }
        catch (const winrt::hresult_error& error)
        {
            OutputDebugStringW(L"Error in GIF recording: ");
            OutputDebugStringW(error.message().c_str());
            OutputDebugStringW(L"\n");

            // Try to commit the encoder even on error
            if (m_gifEncoder)
            {
                try
                {
                    m_gifEncoder->Commit();
                }
                catch (...) {}
            }

            CloseInternal();
        }
    }
    co_return;
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::Close
//
//----------------------------------------------------------------------------
void GifRecordingSession::Close()
{
    auto expected = false;
    if (m_closed.compare_exchange_strong(expected, true))
    {
        expected = true;
        if (!m_isRecording.compare_exchange_strong(expected, false))
        {
            CloseInternal();
        }
        else
        {
            m_frameWait->StopCapture();
        }
    }
}

//----------------------------------------------------------------------------
//
// GifRecordingSession::CloseInternal
//
//----------------------------------------------------------------------------
void GifRecordingSession::CloseInternal()
{
    m_frameWait->StopCapture();
    m_itemClosed.revoke();
}
