#pragma once

#include <winrt/Windows.Foundation.Metadata.h>

// The following three helper functions determine if the user has a build version higher than or equal to 19h1 (aka 1903), as that is a requirement for xaml islands
// Source : Microsoft-ui-xaml github
// Link: https://github.com/microsoft/microsoft-ui-xaml/blob/c045cde57c5c754683d674634a0baccda34d58c4/dev/dll/SharedHelpers.cpp
template<uint16_t APIVersion>
inline bool IsAPIContractVxAvailable()
{
  static bool isAPIContractVxAvailable = winrt::Windows::Foundation::Metadata::ApiInformation::IsApiContractPresent(L"Windows.Foundation.UniversalApiContract", APIVersion);

  return isAPIContractVxAvailable;
}

inline bool IsAPIContractV8Available()
{
  return IsAPIContractVxAvailable<8>();
}

inline bool Is19H1OrHigher()
{
  return IsAPIContractV8Available();
}
