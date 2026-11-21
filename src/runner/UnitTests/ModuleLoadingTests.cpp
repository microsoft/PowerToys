// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../centralized_hotkeys.h"
#include "../centralized_kb_hook.h"
#include "../powertoy_module.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    int factoryCalls = 0;

    class TestModule : public PowertoyModuleIface
    {
    public:
        const wchar_t* get_name() override { return L"DeferredTestModule"; }
        const wchar_t* get_key() override { return L"DeferredTestModule"; }
        bool get_config(wchar_t* buffer, int* size) override
        {
            constexpr wchar_t config[] = L"{\"properties\":{}}";
            if (!buffer || *size < ARRAYSIZE(config))
            {
                *size = ARRAYSIZE(config);
                return false;
            }
            wcscpy_s(buffer, *size, config);
            return true;
        }
        void set_config(const wchar_t*) override {}
        void enable() override { enabled = true; }
        void disable() override { enabled = false; }
        bool is_enabled() override { return enabled; }
        void destroy() override { delete this; }

    private:
        bool enabled = false;
    };
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    ++factoryCalls;
    return new TestModule();
}

// Link the real loader without installing keyboard hooks or registering OS hotkeys.
namespace CentralizedKeyboardHook
{
    void SetHotkeyAction(const std::wstring&, const Hotkey&, std::function<bool()>&&) noexcept {}
    void AddPressedKeyAction(const std::wstring&, DWORD, UINT, std::function<bool()>&&) noexcept {}
    void ClearPressedKeyActions(const std::wstring&) noexcept {}
    void ClearModuleHotkeys(const std::wstring&) noexcept {}
}

namespace CentralizedHotkeys
{
    bool AddHotkeyAction(Shortcut, Action)
    {
        return false;
    }
    void UnregisterHotkeysForModule(std::wstring) {}
}

// Link-time seam: Runner defines the real catalog in main.cpp; tests supply a one-entry catalog.
namespace
{
    std::wstring testModulePath;
    ModuleRegistration testCatalog[1];
}

std::span<const ModuleRegistration> known_modules()
{
    return testCatalog;
}

namespace RunnerUnitTests
{
    TEST_CLASS (ModuleLoadingTests)
    {
    public:
        TEST_METHOD_INITIALIZE(RegisterTestModule)
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
            factoryCalls = 0;
            HMODULE handle = nullptr;
            Assert::IsTrue(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                              reinterpret_cast<LPCWSTR>(&powertoy_create),
                                              &handle) != 0);
            wchar_t path[MAX_PATH]{};
            const auto length = GetModuleFileNameW(handle, path, ARRAYSIZE(path));
            Assert::IsTrue(length > 0 && length < ARRAYSIZE(path));
            testModulePath = path;
            testCatalog[0] = { L"DeferredTestModule", testModulePath, DefaultEnabled::No };
            set_module_loading_allowed(true);
        }

        TEST_METHOD_CLEANUP(UnloadTestModule)
        {
            set_module_loading_allowed(false);
            modules().clear();
            winrt::uninit_apartment();
        }

        TEST_METHOD (CatalogDoesNotCreateDisabledModule)
        {
            Assert::IsFalse(find_known_module(L"DeferredTestModule")->is_enabled(json::JsonObject{}));
            Assert::IsTrue(modules().empty());
            Assert::AreEqual(0, factoryCalls);
        }

        TEST_METHOD (OnDemandLoadIsCachedWithoutEnabling)
        {
            auto* first = load_known_module(L"DeferredTestModule");
            Assert::IsNotNull(first);
            Assert::IsFalse((*first)->is_enabled());
            Assert::IsTrue(first->json_config().HasKey(L"properties"));
            Assert::IsTrue(first == load_known_module(L"DeferredTestModule"));
            Assert::IsTrue(first == find_loaded_module(L"DeferredTestModule"));
            Assert::AreEqual(1, factoryCalls);
        }

        TEST_METHOD (DisablingDoesNotUnloadOrRecreateModule)
        {
            auto* module = load_known_module(L"DeferredTestModule");
            Assert::IsNotNull(module);
            (*module)->enable();
            Assert::IsTrue((*module)->is_enabled());
            (*module)->disable();
            Assert::IsFalse((*module)->is_enabled());
            Assert::IsTrue(module == load_known_module(L"DeferredTestModule"));
            Assert::AreEqual(1, factoryCalls);
        }

        TEST_METHOD (UnknownModuleAndPreInitializationDoNotLoad)
        {
            Assert::IsNull(load_known_module(L"UnknownModule"));
            set_module_loading_allowed(false);
            Assert::IsNull(load_known_module(L"DeferredTestModule"));
            bool enabled = true;
            Assert::IsNull(get_module_for_enabled_state(L"DeferredTestModule", enabled));
            Assert::AreEqual(0, factoryCalls);
        }

        TEST_METHOD (DisablingDeferredModuleDoesNotLoadIt)
        {
            bool enabled = false;
            Assert::IsNull(get_module_for_enabled_state(L"DeferredTestModule", enabled));
            Assert::IsFalse(enabled);
            Assert::IsTrue(modules().empty());
            Assert::AreEqual(0, factoryCalls);
        }

        TEST_METHOD (EnablingDeferredModuleLoadsItOnce)
        {
            bool enabled = true;
            auto* first = get_module_for_enabled_state(L"DeferredTestModule", enabled);
            Assert::IsNotNull(first);
            Assert::IsTrue(enabled);
            Assert::IsTrue(first == get_module_for_enabled_state(L"DeferredTestModule", enabled));
            Assert::AreEqual(1, factoryCalls);
        }

        TEST_METHOD (PolicyForcedDisabledDoesNotLoadOnEnableRequest)
        {
            testCatalog[0].gpoPolicy = [] { return powertoys_gpo::gpo_rule_configured_disabled; };
            bool enabled = true;
            Assert::IsNull(get_module_for_enabled_state(L"DeferredTestModule", enabled));
            Assert::IsFalse(enabled);
            Assert::AreEqual(0, factoryCalls);
        }

        TEST_METHOD (PolicyForcedEnabledLoadsOnDisableRequest)
        {
            testCatalog[0].gpoPolicy = [] { return powertoys_gpo::gpo_rule_configured_enabled; };
            bool enabled = false;
            Assert::IsNotNull(get_module_for_enabled_state(L"DeferredTestModule", enabled));
            Assert::IsTrue(enabled);
            Assert::AreEqual(1, factoryCalls);
        }
    };
    TEST_CLASS (ModuleRegistrationTests)
    {
    public:
        TEST_METHOD_INITIALIZE(InitializeApartment)
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
        }

        TEST_METHOD_CLEANUP(UninitializeApartment)
        {
            winrt::uninit_apartment();
        }

        TEST_METHOD (MissingPreferencesUseNativeDefaults)
        {
            const ModuleRegistration enabled{ L"Module", L"unused.dll", DefaultEnabled::Yes };
            const ModuleRegistration disabled{ L"Module", L"unused.dll", DefaultEnabled::No };
            for (const auto* settings : { L"{}", L"{\"enabled\":{}}", L"{\"enabled\":{\"Other\":true}}" })
            {
                const auto json = json::JsonObject::Parse(settings);
                Assert::IsTrue(enabled.is_enabled(json));
                Assert::IsFalse(disabled.is_enabled(json));
            }
        }

        TEST_METHOD (SavedPreferencesOverrideDefaults)
        {
            const ModuleRegistration enabled{ L"Module", L"unused.dll", DefaultEnabled::Yes };
            const ModuleRegistration disabled{ L"Module", L"unused.dll", DefaultEnabled::No };
            Assert::IsFalse(enabled.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Module\":false}}")));
            Assert::IsTrue(disabled.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Module\":true}}")));
        }

        TEST_METHOD (PolicyOverridesSavedAndDefaultStates)
        {
            const ModuleRegistration forcedOn{ L"Module", L"unused.dll", DefaultEnabled::No, [] { return powertoys_gpo::gpo_rule_configured_enabled; } };
            const ModuleRegistration forcedOff{ L"Module", L"unused.dll", DefaultEnabled::Yes, [] { return powertoys_gpo::gpo_rule_configured_disabled; } };
            Assert::IsTrue(forcedOn.is_enabled(json::JsonObject{}));
            Assert::IsTrue(forcedOn.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Module\":false}}")));
            Assert::IsFalse(forcedOff.is_enabled(json::JsonObject{}));
            Assert::IsFalse(forcedOff.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Module\":true}}")));
        }

        TEST_METHOD (UnavailableInvalidAndUnconfiguredPolicyUsePreferences)
        {
            for (auto policy : { UnavailablePolicy, InvalidPolicy, UnconfiguredPolicy })
            {
                const ModuleRegistration registration{ L"Module", L"unused.dll", DefaultEnabled::No, policy };
                Assert::IsFalse(registration.is_enabled(json::JsonObject{}));
                Assert::IsTrue(registration.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Module\":true}}")));
            }
        }

        TEST_METHOD (MalformedPreferencesDoNotDisableOtherModules)
        {
            for (const auto* settings : { L"{\"enabled\":null}", L"{\"enabled\":false}", L"{\"enabled\":{\"Module\":\"false\"}}" })
            {
                const ModuleRegistration enabled{ L"Module", L"unused.dll", DefaultEnabled::Yes };
                const ModuleRegistration disabled{ L"Module", L"unused.dll", DefaultEnabled::No };
                const auto json = json::JsonObject::Parse(settings);
                Assert::IsTrue(enabled.is_enabled(json));
                Assert::IsFalse(disabled.is_enabled(json));
            }
            const ModuleRegistration registration{ L"Module", L"unused.dll", DefaultEnabled::Yes };
            Assert::IsFalse(registration.is_enabled(json::JsonObject::Parse(L"{\"enabled\":{\"Other\":null,\"Module\":false}}")));
        }

        TEST_METHOD (UnloadedModuleWithoutSavedStateIsReportedDisabled)
        {
            // Settings can drop native keys it does not know (e.g. "File Explorer"); a default-on
            // module that was not loaded must not be reported, and then saved, as enabled.
            const ModuleRegistration defaultOn{ L"Module", L"unused.dll", DefaultEnabled::Yes };
            Assert::IsFalse(defaultOn.unloaded_state(std::nullopt));
            Assert::IsFalse(defaultOn.unloaded_state(false));
        }

        TEST_METHOD (UnloadedModuleKeepsSavedEnabledUnlessPolicyDisablesIt)
        {
            const ModuleRegistration notConfigured{ L"Module", L"unused.dll", DefaultEnabled::No };
            const ModuleRegistration forcedOff{ L"Module", L"unused.dll", DefaultEnabled::No, [] { return powertoys_gpo::gpo_rule_configured_disabled; } };
            Assert::IsTrue(notConfigured.unloaded_state(true));
            Assert::IsFalse(forcedOff.unloaded_state(true));
        }

    private:
        static powertoys_gpo::gpo_rule_configured_t UnavailablePolicy()
        {
            return powertoys_gpo::gpo_rule_configured_unavailable;
        }
        static powertoys_gpo::gpo_rule_configured_t InvalidPolicy()
        {
            return powertoys_gpo::gpo_rule_configured_wrong_value;
        }
        static powertoys_gpo::gpo_rule_configured_t UnconfiguredPolicy()
        {
            return powertoys_gpo::gpo_rule_configured_not_configured;
        }
    };
}
