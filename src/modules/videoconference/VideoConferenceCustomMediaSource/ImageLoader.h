#pragma once

#include <memory>
#include <optional>

#include <wrl/client.h>
#include <Mfidl.h>

#pragma warning(push)
#pragma warning(disable : 4005)
#include <stdint.h>
#pragma warning(pop)

Microsoft::WRL::ComPtr<IMFSample> LoadImageAsSample(Microsoft::WRL::ComPtr<IStream> imageStream, IMFMediaType* outputSampleMediaType) noexcept;
