#pragma once

#include <d3d11.h>

#include <memory>
#include <string_view>
#include <optional>

#pragma warning(push)
#pragma warning(disable : 4005)
#include <stdint.h>
#pragma warning(pop)

HRESULT CreateWICTextureFromMemory(_In_ ID3D11Device * d3dDevice,
  _In_opt_ ID3D11DeviceContext * d3dContext,
  _In_bytecount_(wicDataSize) const uint8_t * wicData,
  _In_ size_t wicDataSize,
  _Out_opt_ ID3D11Resource ** texture,
  _Out_opt_ ID3D11ShaderResourceView ** textureView,
  _In_ size_t maxsize = 0
);

HRESULT CreateWICTextureFromFile(_In_ ID3D11Device * d3dDevice,
  _In_opt_ ID3D11DeviceContext * d3dContext,
  _In_z_ const wchar_t * szFileName,
  _Out_opt_ ID3D11Resource ** texture,
  _Out_opt_ ID3D11ShaderResourceView ** textureView,
  _In_ size_t maxsize = 0
);

struct LoadedImage
{
  std::unique_ptr<const uint8_t[]> buffer;
  size_t pitch; 
  size_t bpp;
  size_t width;
  size_t height;
};

std::optional<LoadedImage> LoadImageFromFile(std::wstring_view fileName);

#define NUM_IMAGE_ROWS 320
#define NUM_IMAGE_COLS 240
#define BYTES_PER_PIXEL 4
#define IMAGE_BUFFER_SIZE_BYTES (NUM_IMAGE_ROWS * NUM_IMAGE_COLS * BYTES_PER_PIXEL)
#define IMAGE_ROW_SIZE_BYTES (NUM_IMAGE_COLS * BYTES_PER_PIXEL)
