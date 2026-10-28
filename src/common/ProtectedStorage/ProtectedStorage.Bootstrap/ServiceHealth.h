#pragma once

namespace PowerToys::ProtectedStorage
{
    struct ServiceHealth
    {
        bool workerReady = false;
        bool dataRecoveryRequired = false;
        bool hasUnresolvedTransaction = false;
        bool maintenance = false;
        constexpr bool Healthy() const
        {
            return workerReady && !dataRecoveryRequired && !hasUnresolvedTransaction && !maintenance;
        }
    };
}
