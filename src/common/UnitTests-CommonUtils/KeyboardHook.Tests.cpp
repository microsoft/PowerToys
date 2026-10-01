#include "pch.h"
#include <interop/KeyboardHook.h>
#include <wil/resource.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    using NativeKeyboardHook = winrt::PowerToys::Interop::implementation::KeyboardHook;
    using KeyboardEvent = winrt::PowerToys::Interop::KeyboardEvent;

    TEST_CLASS(KeyboardHookTests)
    {
    public:
        TEST_METHOD(SnapshotRetainsHookClosedDuringItsOwnCallback)
        {
            winrt::com_ptr<NativeKeyboardHook> hook;
            winrt::weak_ref<NativeKeyboardHook> weak;
            int calls = 0;
            hook = winrt::make_self<NativeKeyboardHook>(
                [&](KeyboardEvent const&)
                {
                    hook->Close();
                    hook = nullptr;
                    Assert::IsTrue(static_cast<bool>(weak.get()));
                    ++calls;
                },
                [] { return true; },
                [](KeyboardEvent const&) { return true; });
            weak = hook->get_weak();
            auto cleanup = wil::scope_exit([&]
            {
                if (hook)
                {
                    hook->Close();
                }
            });
            RegisterWithoutStarting(hook.get());

            Assert::AreEqual<LRESULT>(1, Dispatch());

            Assert::AreEqual(1, calls);
            Assert::IsFalse(static_cast<bool>(weak.get()));
            AssertRegistryEmpty();
        }

        TEST_METHOD(SnapshotRetainsAllHooksClosedByAnotherCallback)
        {
            winrt::com_ptr<NativeKeyboardHook> first;
            winrt::com_ptr<NativeKeyboardHook> second;
            winrt::weak_ref<NativeKeyboardHook> firstWeak;
            winrt::weak_ref<NativeKeyboardHook> secondWeak;
            int activeCalls = 0;
            int keyCalls = 0;
            auto isActive = [&]
            {
                if (++activeCalls == 1)
                {
                    first->Close();
                    second->Close();
                    first = nullptr;
                    second = nullptr;
                    AssertRegistryEmpty();
                    Assert::IsTrue(static_cast<bool>(firstWeak.get()));
                    Assert::IsTrue(static_cast<bool>(secondWeak.get()));
                    return false;
                }

                return true;
            };
            auto keyEvent = [&](KeyboardEvent const&)
            {
                Assert::IsTrue(static_cast<bool>(firstWeak.get()));
                Assert::IsTrue(static_cast<bool>(secondWeak.get()));
                ++keyCalls;
            };
            first = winrt::make_self<NativeKeyboardHook>(keyEvent, isActive, [](KeyboardEvent const&) { return true; });
            second = winrt::make_self<NativeKeyboardHook>(keyEvent, isActive, [](KeyboardEvent const&) { return true; });
            firstWeak = first->get_weak();
            secondWeak = second->get_weak();
            auto cleanup = wil::scope_exit([&]
            {
                if (first)
                {
                    first->Close();
                }
                if (second)
                {
                    second->Close();
                }
            });
            RegisterWithoutStarting(first.get());
            RegisterWithoutStarting(second.get());

            Assert::AreEqual<LRESULT>(1, Dispatch());

            Assert::AreEqual(2, activeCalls);
            Assert::AreEqual(1, keyCalls);
            Assert::IsFalse(static_cast<bool>(firstWeak.get()));
            Assert::IsFalse(static_cast<bool>(secondWeak.get()));
            AssertRegistryEmpty();
        }

    private:
        static void RegisterWithoutStarting(NativeKeyboardHook* hook)
        {
            // Exercise the real registry and HookProc without ever installing a Windows hook.
            std::unique_lock lock{ NativeKeyboardHook::instancesMutex };
            Assert::IsTrue(NativeKeyboardHook::hookHandle == nullptr);
            NativeKeyboardHook::instances.insert(hook);
        }

        static LRESULT Dispatch()
        {
            KBDLLHOOKSTRUCT data{};
            data.vkCode = 'A';
            return NativeKeyboardHook::HookProc(HC_ACTION, WM_KEYDOWN, reinterpret_cast<LPARAM>(&data));
        }

        static void AssertRegistryEmpty()
        {
            std::unique_lock lock{ NativeKeyboardHook::instancesMutex };
            Assert::IsTrue(NativeKeyboardHook::instances.empty());
            Assert::IsTrue(NativeKeyboardHook::hookHandle == nullptr);
        }
    };
}
