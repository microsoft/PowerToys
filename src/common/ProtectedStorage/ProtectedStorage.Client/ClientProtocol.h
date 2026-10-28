#pragma once
#include "../ProtectedStorage.Common/Protocol.h"

namespace PowerToys::ProtectedStorage::ClientProtocol
{
    void ValidateResponse(const Frame& request, const Frame& response);
    void ThrowResponseError(const Frame& request, const Frame& response);
    StorageError TransportFailure(const Frame& request, bool writeAttempted, const std::exception& failure);
}
