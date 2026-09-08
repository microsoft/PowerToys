// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>

namespace RegionMirror
{
    // All methods and destruction belong to the same UI thread, which must pump
    // messages. The manager never injects code or overrides a window procedure.
    class RegionWindowManager final
    {
    public:
        RegionWindowManager();
        ~RegionWindowManager();

        RegionWindowManager(const RegionWindowManager&) = delete;
        RegionWindowManager& operator=(const RegionWindowManager&) = delete;

        // region is the desired visible window rectangle in physical screen pixels.
        void Start(RECT region);
        void Stop() noexcept;

        size_t ManagedCount() const noexcept;
        uint64_t AdjustedCount() const noexcept;
        std::wstring LastError() const;

    private:
        struct State;
        std::unique_ptr<State> m_state;
    };
}
