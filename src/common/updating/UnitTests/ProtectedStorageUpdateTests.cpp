// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <common/updating/protectedStorageUpdate.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UpdatingUnitTests
{
    TEST_CLASS(ProtectedStorageUpdateTests)
    {
    public:
        TEST_METHOD(OwnerRemovalOnlyFollowsSuccessfulExplicitPerUserUninstall)
        {
            Assert::IsTrue(updating::ShouldRemoveOwnerCarrier(true, true, false, true));
            Assert::IsFalse(updating::ShouldRemoveOwnerCarrier(false, true, false, true));
            Assert::IsFalse(updating::ShouldRemoveOwnerCarrier(true, false, false, true));
            Assert::IsFalse(updating::ShouldRemoveOwnerCarrier(true, true, true, true));
            Assert::IsFalse(updating::ShouldRemoveOwnerCarrier(true, true, false, false));
        }

        TEST_METHOD(RecognizesUserOwnersButNotMachineOrServiceAccounts)
        {
            Assert::IsTrue(updating::IsProtectedStorageOwnerSid(L"S-1-5-21-111-222-333-1001"));
            Assert::IsTrue(updating::IsProtectedStorageOwnerSid(L"S-1-12-1-111-222-333-444"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-5-18"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-5-80-111-222-333-444-555"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-5-21-"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-5-21-1-2-3-4294967296"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-5-21-1-2-3-4-5"));
            Assert::IsFalse(updating::IsProtectedStorageOwnerSid(L"S-1-12-1-1-2-3-invalid"));
        }

        TEST_METHOD(CompletedIsDistinctFromRestartRequired)
        {
            Assert::IsTrue(updating::ClassifyProtectedStorageSyncExit(0).state == updating::ProtectedStorageSyncState::Completed);
            for (const DWORD code : { ERROR_SUCCESS_REBOOT_REQUIRED, ERROR_SUCCESS_REBOOT_INITIATED })
            {
                const auto result = updating::ClassifyProtectedStorageSyncExit(code);
                Assert::IsTrue(result.state == updating::ProtectedStorageSyncState::RestartRequired);
                Assert::AreEqual(code, result.nativeCode);
            }
        }

        TEST_METHOD(TimeoutIsUnknownRatherThanSuccessOrSafeRollback)
        {
            const auto result = updating::ClassifyProtectedStorageSyncExit(WAIT_TIMEOUT);
            Assert::IsTrue(result.state == updating::ProtectedStorageSyncState::OutcomeUnknown);
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), result.nativeCode);
        }

        TEST_METHOD(BusyAndInvalidPayloadRemainVisibleFailures)
        {
            for (const DWORD code : { ERROR_INSTALL_ALREADY_RUNNING, ERROR_INSTALL_FAILURE, ERROR_ACCESS_DENIED, ERROR_INVALID_IMAGE_HASH })
            {
                const auto result = updating::ClassifyProtectedStorageSyncExit(code);
                Assert::IsTrue(result.state == updating::ProtectedStorageSyncState::RetryRequired);
                Assert::AreEqual(code, result.nativeCode);
            }
        }
    };
}
