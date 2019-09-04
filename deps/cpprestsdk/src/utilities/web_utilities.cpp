/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * Credential and proxy utilities.
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#include "pch.h"

#include <assert.h>

#if defined(_WIN32) && !defined(__cplusplus_winrt)
#include <Wincrypt.h>
#endif

#if defined(__cplusplus_winrt)
#include <robuffer.h>
#endif

namespace web
{
namespace details
{
#if defined(_WIN32) && !defined(CPPREST_TARGET_XP)
#if defined(__cplusplus_winrt)

// Helper function to zero out memory of an IBuffer.
void winrt_secure_zero_buffer(Windows::Storage::Streams::IBuffer ^ buffer)
{
    Microsoft::WRL::ComPtr<IInspectable> bufferInspectable(reinterpret_cast<IInspectable*>(buffer));
    Microsoft::WRL::ComPtr<Windows::Storage::Streams::IBufferByteAccess> bufferByteAccess;
    bufferInspectable.As(&bufferByteAccess);

    // This shouldn't happen but if can't get access to the raw bytes for some reason
    // then we can't zero out.
    byte* rawBytes;
    if (bufferByteAccess->Buffer(&rawBytes) == S_OK)
    {
        SecureZeroMemory(rawBytes, buffer->Length);
    }
}

winrt_encryption::winrt_encryption(const std::wstring& data)
{
    auto provider = ref new Windows::Security::Cryptography::DataProtection::DataProtectionProvider(
        ref new Platform::String(L"Local=user"));

    // Create buffer containing plain text password.
    Platform::ArrayReference<unsigned char> arrayref(
        reinterpret_cast<unsigned char*>(const_cast<std::wstring::value_type*>(data.c_str())),
        static_cast<unsigned int>(data.size()) * sizeof(std::wstring::value_type));
    Windows::Storage::Streams::IBuffer ^ plaintext =
        Windows::Security::Cryptography::CryptographicBuffer::CreateFromByteArray(arrayref);
    m_buffer = pplx::create_task(provider->ProtectAsync(plaintext));
    m_buffer.then(
        [plaintext](pplx::task<Windows::Storage::Streams::IBuffer ^>) { winrt_secure_zero_buffer(plaintext); });
}

plaintext_string winrt_encryption::decrypt() const
{
    // To fully guarantee asynchrony would require significant impact on existing code. This code path
    // is never run on a user's thread and is only done once when setting up a connection.
    auto encrypted = m_buffer.get();
    auto provider = ref new Windows::Security::Cryptography::DataProtection::DataProtectionProvider();
    auto plaintext = pplx::create_task(provider->UnprotectAsync(encrypted)).get();

    // Get access to raw bytes in plain text buffer.
    Microsoft::WRL::ComPtr<IInspectable> bufferInspectable(reinterpret_cast<IInspectable*>(plaintext));
    Microsoft::WRL::ComPtr<Windows::Storage::Streams::IBufferByteAccess> bufferByteAccess;
    bufferInspectable.As(&bufferByteAccess);
    byte* rawPlaintext;
    const auto& result = bufferByteAccess->Buffer(&rawPlaintext);
    if (result != S_OK)
    {
        throw ::utility::details::create_system_error(result);
    }

    // Construct string and zero out memory from plain text buffer.
    auto data = plaintext_string(
        new std::wstring(reinterpret_cast<const std::wstring::value_type*>(rawPlaintext), plaintext->Length / 2));
    SecureZeroMemory(rawPlaintext, plaintext->Length);
    return std::move(data);
}

#else

win32_encryption::win32_encryption(const std::wstring& data) : m_numCharacters(data.size())
{
    // Early return because CryptProtectMemory crashes with empty string
    if (m_numCharacters == 0)
    {
        return;
    }

    if (data.size() > (std::numeric_limits<DWORD>::max)() / sizeof(wchar_t))
    {
        throw std::length_error("Encryption string too long");
    }

    const auto dataSizeDword = static_cast<DWORD>(data.size() * sizeof(wchar_t));

    // Round up dataSizeDword to be a multiple of CRYPTPROTECTMEMORY_BLOCK_SIZE
    static_assert(CRYPTPROTECTMEMORY_BLOCK_SIZE == 16, "Power of 2 assumptions in this bit masking violated");
    const auto mask = static_cast<DWORD>(CRYPTPROTECTMEMORY_BLOCK_SIZE - 1u);
    const auto dataNumBytes = (dataSizeDword & ~mask) + ((dataSizeDword & mask) != 0) * CRYPTPROTECTMEMORY_BLOCK_SIZE;
    assert((dataNumBytes % CRYPTPROTECTMEMORY_BLOCK_SIZE) == 0);
    assert(dataNumBytes >= dataSizeDword);
    m_buffer.resize(dataNumBytes);
    memcpy_s(m_buffer.data(), m_buffer.size(), data.c_str(), dataNumBytes);
    if (!CryptProtectMemory(m_buffer.data(), dataNumBytes, CRYPTPROTECTMEMORY_SAME_PROCESS))
    {
        throw ::utility::details::create_system_error(GetLastError());
    }
}

win32_encryption::~win32_encryption() { SecureZeroMemory(m_buffer.data(), m_buffer.size()); }

plaintext_string win32_encryption::decrypt() const
{
    // Copy the buffer and decrypt to avoid having to re-encrypt.
    auto result = plaintext_string(new std::wstring(reinterpret_cast<const std::wstring::value_type*>(m_buffer.data()),
                                                    m_buffer.size() / sizeof(wchar_t)));
    auto& data = *result;
    if (!m_buffer.empty())
    {
        if (!CryptUnprotectMemory(&data[0], static_cast<DWORD>(m_buffer.size()), CRYPTPROTECTMEMORY_SAME_PROCESS))
        {
            throw ::utility::details::create_system_error(GetLastError());
        }

        assert(m_numCharacters <= m_buffer.size());
        SecureZeroMemory(&data[m_numCharacters], data.size() - m_numCharacters);
        data.erase(m_numCharacters);
    }

    return result;
}
#endif
#endif

void zero_memory_deleter::operator()(::utility::string_t* data) const
{
    CASABLANCA_UNREFERENCED_PARAMETER(data);
#if defined(_WIN32)
    SecureZeroMemory(&(*data)[0], data->size() * sizeof(::utility::string_t::value_type));
    delete data;
#endif
}
} // namespace details

} // namespace web
