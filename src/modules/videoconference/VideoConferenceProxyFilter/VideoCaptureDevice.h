#pragma once
#include <initguid.h>
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <dshow.h>

#include <wil/com.h>

#include <functional>
#include <vector>
#include <string>
#include <optional>

#include "DirectShowUtils.h"

struct VideoStreamFormat
{
    long width = 0;
    long height = 0;
    REFERENCE_TIME avgFrameTime = std::numeric_limits<REFERENCE_TIME>::max();
    unique_media_type_ptr mediaType;

    VideoStreamFormat() = default;

    VideoStreamFormat(const VideoStreamFormat&) = delete;
    VideoStreamFormat& operator=(const VideoStreamFormat&) = delete;

    VideoStreamFormat(VideoStreamFormat&&) = default;
    VideoStreamFormat& operator=(VideoStreamFormat&&) = default;
};

struct VideoCaptureDeviceInfo
{
    std::wstring friendlyName;
    std::wstring devicePath;
    wil::com_ptr_nothrow<IPin> captureOutputPin;
    wil::com_ptr_nothrow<IBaseFilter> captureOutputFilter;
    VideoStreamFormat bestFormat;
};

class VideoCaptureDevice final
{
public:
    wil::com_ptr_nothrow<IMemAllocator> _allocator;

    using callback_t = std::function<void(IMediaSample*)>;

    static std::vector<VideoCaptureDeviceInfo> ListAll();
    static std::optional<VideoCaptureDevice> Create(VideoCaptureDeviceInfo&& vdi, callback_t callback);

    bool StartCapture();
    bool StopCapture();

    ~VideoCaptureDevice();

private:
    wil::com_ptr_nothrow<IGraphBuilder> _graph;
    wil::com_ptr_nothrow<ICaptureGraphBuilder2> _builder;
    wil::com_ptr_nothrow<IMediaControl> _control;
    callback_t _callback;
};
