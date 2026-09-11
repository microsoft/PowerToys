// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <algorithm>
#include <chrono>
#include <functional>
#include <memory>
#include <wil/resource.h>
#include <winrt/Windows.Foundation.h>

namespace PackageVerification::details
{
    inline void ThrowIfCanceled(const std::function<bool()>& isCanceled)
    {
        if (isCanceled && isCanceled())
        {
            winrt::throw_hresult(HRESULT_FROM_WIN32(ERROR_CANCELLED));
        }
    }

    template<typename T>
    T Await(const winrt::Windows::Foundation::IAsyncOperation<T>& operation,
            std::chrono::milliseconds timeout,
            const std::function<bool()>& isCanceled)
    {
        const auto deadline = std::chrono::steady_clock::now() + timeout;
        if (operation.Status() != winrt::Windows::Foundation::AsyncStatus::Started)
        {
            ThrowIfCanceled(isCanceled);
            return operation.GetResults();
        }
        if (isCanceled && isCanceled())
        {
            operation.Cancel();
            winrt::throw_hresult(HRESULT_FROM_WIN32(ERROR_CANCELLED));
        }

        wil::unique_event event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            winrt::throw_last_error();
        }
        // WinRT permits only one completion handler. Keep its event alive even if
        // cancellation or timeout returns before the operation invokes the handler.
        const auto completed = std::make_shared<wil::unique_event>(std::move(event));
        operation.Completed([completed](const auto&, auto) noexcept {
            WINRT_VERIFY(SetEvent(completed->get()));
        });

        for (;;)
        {
            if (isCanceled && isCanceled())
            {
                operation.Cancel();
                winrt::throw_hresult(HRESULT_FROM_WIN32(ERROR_CANCELLED));
            }
            const auto now = std::chrono::steady_clock::now();
            const auto remaining = now < deadline ? std::chrono::ceil<std::chrono::milliseconds>(deadline - now) : std::chrono::milliseconds::zero();
            const auto interval = (std::min)(remaining, std::chrono::milliseconds(100));
            const auto result = WaitForSingleObject(completed->get(), static_cast<DWORD>(interval.count()));
            if (result == WAIT_OBJECT_0)
            {
                break;
            }
            if (result == WAIT_FAILED)
            {
                winrt::throw_last_error();
            }
            if (result != WAIT_TIMEOUT)
            {
                winrt::throw_hresult(E_UNEXPECTED);
            }
            if (std::chrono::steady_clock::now() >= deadline)
            {
                operation.Cancel();
                ThrowIfCanceled(isCanceled);
                winrt::throw_hresult(HRESULT_FROM_WIN32(ERROR_TIMEOUT));
            }
        }
        ThrowIfCanceled(isCanceled);
        return operation.GetResults();
    }
}
