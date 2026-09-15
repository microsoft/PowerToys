// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <WorkspacesLib/PackageVerificationAsync.h>

#include <atomic>
#include <future>
#include <mutex>
#include <wil/resource.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        using Operation = winrt::Windows::Foundation::IAsyncOperation<bool>;
        using AsyncStatus = winrt::Windows::Foundation::AsyncStatus;
        using CompletedHandler = winrt::Windows::Foundation::AsyncOperationCompletedHandler<bool>;

        struct ControlledOperation : winrt::implements<ControlledOperation, Operation, winrt::Windows::Foundation::IAsyncInfo>
        {
            wil::unique_event registered{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            std::atomic<unsigned int> registrations{};
            std::atomic<unsigned int> cancellations{};
            bool finishDuringRegistration{};

            constexpr uint32_t Id() const noexcept
            {
                return 1;
            }

            AsyncStatus Status()
            {
                std::lock_guard lock(m_mutex);
                return m_status;
            }

            winrt::hresult ErrorCode()
            {
                std::lock_guard lock(m_mutex);
                return m_error;
            }

            void Cancel() noexcept
            {
                ++cancellations;
            }

            void Close() const noexcept
            {
            }

            CompletedHandler Completed()
            {
                std::lock_guard lock(m_mutex);
                return m_handler;
            }

            void Completed(const CompletedHandler& handler)
            {
                AsyncStatus status;
                {
                    std::lock_guard lock(m_mutex);
                    if (++registrations != 1)
                    {
                        winrt::throw_hresult(E_ILLEGAL_DELEGATE_ASSIGNMENT);
                    }
                    m_handler = handler;
                    if (finishDuringRegistration)
                    {
                        m_status = AsyncStatus::Completed;
                    }
                    status = m_status;
                }
                SetEvent(registered.get());
                if (status != AsyncStatus::Started)
                {
                    handler(get_strong().as<Operation>(), status);
                }
            }

            bool GetResults()
            {
                std::lock_guard lock(m_mutex);
                if (m_status == AsyncStatus::Error || m_status == AsyncStatus::Canceled)
                {
                    winrt::throw_hresult(m_error);
                }
                if (m_status != AsyncStatus::Completed)
                {
                    winrt::throw_hresult(E_ILLEGAL_METHOD_CALL);
                }
                return m_result;
            }

            void Finish(bool result = true, HRESULT error = S_OK)
            {
                CompletedHandler handler{ nullptr };
                AsyncStatus status;
                {
                    std::lock_guard lock(m_mutex);
                    if (m_status != AsyncStatus::Started)
                    {
                        return;
                    }
                    m_result = result;
                    m_error = error;
                    m_status = SUCCEEDED(error) ? AsyncStatus::Completed : AsyncStatus::Error;
                    status = m_status;
                    handler = m_handler;
                }
                if (handler)
                {
                    handler(get_strong().as<Operation>(), status);
                }
            }

        private:
            std::mutex m_mutex;
            AsyncStatus m_status{ AsyncStatus::Started };
            HRESULT m_error{ S_OK };
            bool m_result{ true };
            CompletedHandler m_handler{ nullptr };
        };

        std::future<bool> StartWait(Operation operation, std::chrono::milliseconds timeout = std::chrono::seconds(5), std::function<bool()> isCanceled = {})
        {
            return std::async(std::launch::async, [operation, timeout, isCanceled] {
                winrt::init_apartment(winrt::apartment_type::multi_threaded);
                auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
                return PackageVerification::details::Await(operation, timeout, isCanceled);
            });
        }

        HRESULT WaitError(std::future<bool>& result)
        {
            try
            {
                result.get();
                return S_OK;
            }
            catch (const winrt::hresult_error& error)
            {
                return error.code();
            }
        }
    }

    TEST_CLASS (PackageVerificationAsyncTests)
    {
    public:
        TEST_METHOD (SlowCompletionRegistersOnlyOneHandler)
        {
            auto state = winrt::make_self<ControlledOperation>();
            auto result = StartWait(state.as<Operation>());
            auto finish = wil::scope_exit([&] { state->Finish(); });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(state->registered.get(), 2000));

            const auto waiting = result.wait_for(std::chrono::milliseconds(250));
            state->Finish();
            Assert::AreEqual(1u, state->registrations.load());
            Assert::IsTrue(result.get());
            Assert::IsTrue(waiting == std::future_status::timeout);
            Assert::AreEqual(0u, state->cancellations.load());
        }

        TEST_METHOD (AlreadyCompletedOperationReturnsItsValue)
        {
            auto state = winrt::make_self<ControlledOperation>();
            state->Finish(false);
            auto result = StartWait(state.as<Operation>());
            Assert::IsFalse(result.get());
            Assert::AreEqual(0u, state->registrations.load());
        }

        TEST_METHOD (CompletionDuringRegistrationIsNotLost)
        {
            auto state = winrt::make_self<ControlledOperation>();
            state->finishDuringRegistration = true;
            auto result = StartWait(state.as<Operation>());
            Assert::IsTrue(result.get());
            Assert::AreEqual(1u, state->registrations.load());
            Assert::AreEqual(0u, state->cancellations.load());
        }

        TEST_METHOD (SlowVerificationFailurePreservesItsError)
        {
            auto state = winrt::make_self<ControlledOperation>();
            auto result = StartWait(state.as<Operation>());
            auto finish = wil::scope_exit([&] { state->Finish(); });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(state->registered.get(), 2000));
            const auto waiting = result.wait_for(std::chrono::milliseconds(250));
            state->Finish(false, E_ACCESSDENIED);
            Assert::AreEqual(static_cast<HRESULT>(E_ACCESSDENIED), WaitError(result));
            Assert::IsTrue(waiting == std::future_status::timeout);
            Assert::AreEqual(1u, state->registrations.load());
        }

        TEST_METHOD (FalseIntegrityResultIsNotChangedToSuccess)
        {
            auto state = winrt::make_self<ControlledOperation>();
            auto result = StartWait(state.as<Operation>());
            auto finish = wil::scope_exit([&] { state->Finish(); });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(state->registered.get(), 2000));
            state->Finish(false);
            Assert::IsFalse(result.get());
            Assert::AreEqual(1u, state->registrations.load());
        }

        TEST_METHOD (CancellationBeforeWaitingDoesNotRegisterAHandler)
        {
            auto state = winrt::make_self<ControlledOperation>();
            auto result = StartWait(state.as<Operation>(), std::chrono::seconds(5), [] { return true; });
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_CANCELLED), WaitError(result));
            Assert::AreEqual(1u, state->cancellations.load());
            Assert::AreEqual(0u, state->registrations.load());
        }

        TEST_METHOD (CancellationReturnsPromptlyAndLateCompletionIsSafe)
        {
            auto state = winrt::make_self<ControlledOperation>();
            std::atomic<bool> canceled{};
            auto result = StartWait(state.as<Operation>(), std::chrono::seconds(5), [&] { return canceled.load(); });
            auto finish = wil::scope_exit([&] { state->Finish(); });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(state->registered.get(), 2000));
            canceled = true;
            Assert::IsTrue(result.wait_for(std::chrono::seconds(1)) == std::future_status::ready);
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_CANCELLED), WaitError(result));
            Assert::AreEqual(1u, state->cancellations.load());
            state->Finish();
            Assert::AreEqual(1u, state->registrations.load());
        }

        TEST_METHOD (TimeoutReturnsPromptlyAndLateCompletionIsSafe)
        {
            auto state = winrt::make_self<ControlledOperation>();
            const auto started = std::chrono::steady_clock::now();
            auto result = StartWait(state.as<Operation>(), std::chrono::milliseconds(150));
            auto finish = wil::scope_exit([&] { state->Finish(); });
            Assert::IsTrue(result.wait_for(std::chrono::seconds(2)) == std::future_status::ready);
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_TIMEOUT), WaitError(result));
            Assert::IsTrue(std::chrono::steady_clock::now() - started >= std::chrono::milliseconds(150));
            Assert::AreEqual(1u, state->cancellations.load());
            state->Finish();
            Assert::AreEqual(1u, state->registrations.load());
        }

        TEST_METHOD (CancellationOverridesAnAlreadyCompletedResult)
        {
            auto state = winrt::make_self<ControlledOperation>();
            state->Finish();
            auto result = StartWait(state.as<Operation>(), std::chrono::seconds(5), [] { return true; });
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_CANCELLED), WaitError(result));
            Assert::AreEqual(0u, state->registrations.load());
        }

        TEST_METHOD (AlreadyFailedOperationPreservesItsError)
        {
            auto state = winrt::make_self<ControlledOperation>();
            state->Finish(false, E_ACCESSDENIED);
            auto result = StartWait(state.as<Operation>());
            Assert::AreEqual(static_cast<HRESULT>(E_ACCESSDENIED), WaitError(result));
            Assert::AreEqual(0u, state->registrations.load());
        }
    };
}
