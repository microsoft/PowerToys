// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <common/utils/owner_process.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(OwnerProcessTests)
    {
    public:
        TEST_METHOD(OnlySameOwnerSameSessionNormalShellIsAccepted)
        {
            owner_process::Identity caller{ L"S-1-5-21-1-2-3-1001", 2, SECURITY_MANDATORY_HIGH_RID, true };
            owner_process::Identity shell{ caller.sid, 2, SECURITY_MANDATORY_MEDIUM_RID, false };
            Assert::IsTrue(owner_process::CanUseOwnerShell(caller, shell));
            shell.sid = L"S-1-5-21-1-2-3-1002";
            Assert::IsFalse(owner_process::CanUseOwnerShell(caller, shell));
            shell.sid = caller.sid;
            shell.session = 3;
            Assert::IsFalse(owner_process::CanUseOwnerShell(caller, shell));
            shell.session = caller.session;
            caller.elevated = false;
            caller.integrity = SECURITY_MANDATORY_LOW_RID;
            Assert::IsFalse(owner_process::CanUseOwnerShell(caller, shell));
        }

        TEST_METHOD(ElevatedAndServiceTokensAreNeverNormalLaunchAuthority)
        {
            Assert::IsFalse(owner_process::IsNormalIdentity({ L"S-1-5-18", 0, SECURITY_MANDATORY_SYSTEM_RID, true }));
            Assert::IsFalse(owner_process::IsNormalIdentity({ L"S-1-5-21-1-2-3-1001", 2, SECURITY_MANDATORY_HIGH_RID, true }));
            Assert::IsFalse(owner_process::IsNormalIdentity({ L"S-1-5-21-1-2-3-1001", 0, SECURITY_MANDATORY_MEDIUM_RID, false }));
            Assert::IsTrue(owner_process::IsNormalIdentity({ L"S-1-12-1-1-2-3-4", 2, SECURITY_MANDATORY_MEDIUM_RID, false }));
        }
    };
}
