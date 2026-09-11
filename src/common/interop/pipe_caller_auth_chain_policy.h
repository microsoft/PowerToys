// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <wincrypt.h>

namespace interop_auth::details
{
    inline bool VerifyMachineSignerChainPolicy(PCCERT_CHAIN_CONTEXT chain)
    {
        const DWORD ignore = CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION;
        if (!chain || (chain->TrustStatus.dwErrorStatus & ~ignore) != 0)
        {
            return false;
        }

        CERT_CHAIN_POLICY_PARA policyPara{};
        policyPara.cbSize = sizeof(policyPara);
        // Match the status mask above without ignoring known revocation or other trust failures.
        policyPara.dwFlags = CERT_CHAIN_POLICY_IGNORE_ALL_REV_UNKNOWN_FLAGS;
        CERT_CHAIN_POLICY_STATUS policyStatus{};
        policyStatus.cbSize = sizeof(policyStatus);
        return CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_AUTHENTICODE, chain, &policyPara, &policyStatus) &&
               policyStatus.dwError == ERROR_SUCCESS;
    }
}
