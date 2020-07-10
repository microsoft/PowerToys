#pragma once

#include <memory>
#include <string_view>
#include <optional>
#include <vector>

#include <wrl/client.h>
#include <Mfidl.h>

#pragma warning(push)
#pragma warning(disable : 4005)
#include <stdint.h>
#pragma warning(pop)

Microsoft::WRL::ComPtr<IMFSample> LoadImageAsSample(std::wstring_view fileName, IMFMediaType* outputSampleMediaType);
