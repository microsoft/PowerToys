#include "pch.h"
#include "Tsf4TextReplacer.h"

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.UI.Input.Preview.Text.h>
#include <winrt/Windows.UI.Text.Core.h>

using namespace winrt::Windows::UI::Input::Preview::Text;
using namespace winrt::Windows::UI::Text::Core;

namespace
{
    TextInputProvider s_provider{ nullptr };
    bool s_initialized = false;
    bool s_available = false;

    // LAF token computed for PFN: Microsoft.PowerToys.SparseApp_8wekyb3d8bbwe
    constexpr wchar_t LafFeatureId[] = L"com.microsoft.windows.textinputmethod";
    constexpr wchar_t LafToken[] = L"fs0FTtO4rVEbMtnhuNmCNA==";
    constexpr wchar_t LafPublisherId[] = L"8wekyb3d8bbwe";

    void UnlockTextInputMethodFeature() noexcept
    {
        try
        {
            std::wstring attestation = std::wstring(LafPublisherId) + L" has registered their use of " + LafFeatureId + L" with Microsoft and agrees to the terms of use.";

            auto result = winrt::Windows::ApplicationModel::LimitedAccessFeatures::TryUnlockFeature(
                LafFeatureId, LafToken, attestation);

            Logger::info(L"TSF4 LAF unlock status: {}", static_cast<int>(result.Status()));
        }
        catch (const winrt::hresult_error& ex)
        {
            Logger::warn(L"TSF4 LAF unlock failed: {}", ex.message().c_str());
        }
        catch (...)
        {
            Logger::warn(L"TSF4 LAF unlock failed (no package identity?)");
        }
    }
}

namespace Tsf4TextReplacer
{
    void Initialize() noexcept
    {
        if (s_initialized)
        {
            return;
        }

        s_initialized = true;

        UnlockTextInputMethodFeature();

        try
        {
            auto service = TextInputService::GetForCurrentThread();
            if (!service)
            {
                Logger::warn(L"TSF4: TextInputService not available on this thread");
                return;
            }

            s_provider = service.CreateTextInputProvider(L"");
            if (!s_provider)
            {
                Logger::warn(L"TSF4: Failed to create TextInputProvider");
                return;
            }

            TextInputServiceSubscription subscription{};
            subscription.requiredEnabledFeatures = TextBoxFeatures::None;
            s_provider.SetSubscription(subscription);

            s_available = true;
            Logger::info(L"TSF4: Text input provider initialized successfully");
        }
        catch (const winrt::hresult_error& ex)
        {
            Logger::warn(L"TSF4: Initialization failed: {}", ex.message().c_str());
        }
        catch (...)
        {
            Logger::warn(L"TSF4: Initialization failed with unknown exception");
        }
    }

    bool IsAvailable() noexcept
    {
        return s_available && s_provider;
    }

    bool TryExpand(const std::wstring& abbreviation, const std::wstring& expandedText) noexcept
    {
        if (!s_available || !s_provider)
        {
            return false;
        }

        try
        {
            if (!s_provider.HasFocusedTextBox())
            {
                return false;
            }

            auto session = s_provider.CreateEditSession();
            if (!session)
            {
                return false;
            }

            int textLength = session.TextLength();
            int abbrevLen = static_cast<int>(abbreviation.length());

            if (textLength < abbrevLen)
            {
                session.SubmitPayload();
                return false;
            }

            // Read the last N characters (where N = abbreviation length)
            CoreTextRange range{};
            range.StartCaretPosition = textLength - abbrevLen;
            range.EndCaretPosition = textLength;

            winrt::hstring tail = session.GetText(range);

            // Case-insensitive comparison
            if (_wcsicmp(tail.c_str(), abbreviation.c_str()) == 0)
            {
                session.ReplaceText(range, expandedText);
                session.SubmitPayload();
                return true;
            }

            session.SubmitPayload();
            return false;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
        catch (...)
        {
            return false;
        }
    }

    void Shutdown() noexcept
    {
        try
        {
            s_provider = nullptr;
            s_available = false;
            s_initialized = false;
        }
        catch (...)
        {
        }
    }
}
