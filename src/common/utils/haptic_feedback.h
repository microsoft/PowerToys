#pragma once

#include <cstdint>
#include <Windows.h>

#include <winrt/Windows.Devices.Haptics.h>

namespace HapticFeedback
{
    class Player
    {
    public:
        void InitializeForCurrentThread()
        {
            using namespace winrt::Windows::Devices::Haptics;

            const auto statics = winrt::try_get_activation_factory<InputHapticsManager, IInputHapticsManagerStatics>();
            if (!statics || !statics.IsSupported())
            {
                return;
            }

            m_manager = statics.GetForCurrentThread();
            m_isSupported = m_manager != nullptr;
            m_isDevicePresent = statics.IsHapticDevicePresent();
        }

        void Configure(bool moduleEnabled)
        {
            m_moduleEnabled = moduleEnabled;
        }

        bool IsSupported() const noexcept
        {
            return m_isSupported;
        }

        bool IsDevicePresent() const noexcept
        {
            return m_isDevicePresent;
        }

        bool IsPlaybackEnabled() const noexcept
        {
            return m_isSupported && m_moduleEnabled;
        }

        bool HasCurrentController() const
        {
            return m_manager && m_manager.CurrentHapticsController() != nullptr;
        }

        winrt::Windows::Devices::Haptics::HapticDeviceType CurrentControllerDeviceType() const
        {
            using namespace winrt::Windows::Devices::Haptics;
            return m_manager ? m_manager.CurrentHapticsControllerDeviceType() : HapticDeviceType::None;
        }

        bool TryPlay(std::uint16_t waveform)
        {
            if (!m_manager || !IsPlaybackEnabled())
            {
                return false;
            }

            const std::uint64_t now = GetTickCount64();
            if (waveform == m_lastWaveform && now - m_lastPlaybackTick < DuplicateSuppressionMs)
            {
                return false;
            }

            const bool sent = m_manager.TrySendHapticWaveform(waveform, 0, PlaybackIntensity);
            if (sent)
            {
                m_lastWaveform = waveform;
                m_lastPlaybackTick = now;
                m_isDevicePresent = true;
            }

            return sent;
        }

    private:
        static constexpr std::uint64_t DuplicateSuppressionMs = 50;
        static constexpr double PlaybackIntensity = 0.5;

        winrt::Windows::Devices::Haptics::InputHapticsManager m_manager{ nullptr };
        bool m_isSupported = false;
        bool m_isDevicePresent = false;
        bool m_moduleEnabled = true;
        std::uint16_t m_lastWaveform = 0;
        std::uint64_t m_lastPlaybackTick = 0;
    };
}
