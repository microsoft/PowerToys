#pragma once
#include "Common.h"

namespace PowerToys::ProtectedStorage
{
    constexpr bool MayUseIncomingMaintenanceProof(Command command, bool matchingTransaction, bool terminal)
    {
        return command == Command::MsiPrepare ||
               (!matchingTransaction && (command == Command::MsiQuery || command == Command::MsiRollback)) ||
               (matchingTransaction && terminal && command == Command::MsiQuery);
    }
}
