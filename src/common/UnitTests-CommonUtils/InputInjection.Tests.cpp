#include "pch.h"
#include "TestHelpers.h"
#include <utils/input_injection.h>

#include <set>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        constexpr ULONG_PTR test_extra_info = 0x110;
        constexpr WORD vk_v = 'V';

        // Records SendInput batches and serves a fake keyboard state.
        struct fake_input
        {
            std::vector<std::vector<INPUT>> batches;
            std::vector<UINT> send_limits; // How many events each SendInput call accepts; all when not set.
            std::set<WORD> held;
            int sleeps = 0;
            int release_after_sleeps = -1; // Clears `held` after this many sleeps; never when negative.

            input_injection::system_api api()
            {
                input_injection::system_api result;
                result.send_input = [this](UINT count, INPUT* inputs) {
                    const UINT accepted = batches.size() < send_limits.size() ? (std::min)(count, send_limits[batches.size()]) : count;
                    batches.emplace_back(inputs, inputs + count);
                    return accepted;
                };
                result.is_key_down = [this](WORD vk) { return held.contains(vk); };
                result.get_last_error = [] { return static_cast<DWORD>(ERROR_ACCESS_DENIED); };
                result.sleep = [this](std::chrono::milliseconds) {
                    if (++sleeps == release_after_sleeps)
                    {
                        held.clear();
                    }
                };
                return result;
            }
        };

        constexpr input_injection::chord_options options_without_wait()
        {
            input_injection::chord_options options;
            options.extra_info = test_extra_info;
            return options;
        }

        void assert_key(const INPUT& input, WORD vk, bool key_up)
        {
            Assert::AreEqual(static_cast<DWORD>(INPUT_KEYBOARD), input.type);
            Assert::AreEqual(vk, input.ki.wVk);
            Assert::AreEqual(key_up ? static_cast<DWORD>(KEYEVENTF_KEYUP) : 0ul, input.ki.dwFlags);
            Assert::AreEqual(static_cast<unsigned long long>(test_extra_info), static_cast<unsigned long long>(input.ki.dwExtraInfo));
        }

        bool is_modifier(WORD vk)
        {
            return vk == VK_CONTROL || vk == VK_SHIFT || vk == VK_MENU ||
                   std::find(input_injection::modifier_keys.begin(), input_injection::modifier_keys.end(), vk) != input_injection::modifier_keys.end();
        }
    }

    TEST_CLASS(InputInjectionTests)
    {
    public:
        TEST_METHOD(SendChord_SendsWholeChordInOneTaggedBatch)
        {
            fake_input fake;

            const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options_without_wait(), fake.api());

            Assert::IsTrue(result.succeeded());
            Assert::AreEqual(size_t{ 1 }, fake.batches.size());
            const auto& batch = fake.batches[0];
            Assert::AreEqual(size_t{ 5 }, batch.size());
            assert_key(batch[0], input_injection::start_menu_suppression_key, true);
            assert_key(batch[1], VK_CONTROL, false);
            assert_key(batch[2], vk_v, false);
            assert_key(batch[3], vk_v, true);
            assert_key(batch[4], VK_CONTROL, true);
        }

        TEST_METHOD(SendChord_PartialSend_ReleasesKeysThatWentDown)
        {
            fake_input fake;
            fake.send_limits = { 3 }; // Start menu suppression, Ctrl down, V down.

            const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options_without_wait(), fake.api());

            Assert::IsFalse(result.succeeded());
            Assert::AreEqual(3u, result.sent);
            Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), result.error);
            Assert::AreEqual(size_t{ 2 }, fake.batches.size());
            const auto& cleanup = fake.batches[1];
            Assert::AreEqual(size_t{ 2 }, cleanup.size());
            assert_key(cleanup[0], vk_v, true);
            assert_key(cleanup[1], VK_CONTROL, true);
            Assert::AreEqual(2u, result.cleanup_requested);
            Assert::AreEqual(2u, result.cleanup_sent);
        }

        TEST_METHOD(SendChord_PartialSend_OnlyReleasesKeysWhoseKeyUpWasNotSent)
        {
            fake_input fake;
            fake.send_limits = { 4 }; // ... V up went out, Ctrl up didn't.

            const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options_without_wait(), fake.api());

            Assert::IsFalse(result.succeeded());
            Assert::AreEqual(size_t{ 2 }, fake.batches.size());
            Assert::AreEqual(size_t{ 1 }, fake.batches[1].size());
            assert_key(fake.batches[1][0], VK_CONTROL, true);
        }

        TEST_METHOD(SendChord_SendInputStopsBeforeAnyKeyDown_SendsNoCleanup)
        {
            for (const UINT limit : { 0u, 1u })
            {
                fake_input fake;
                fake.send_limits = { limit };

                const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options_without_wait(), fake.api());

                Assert::IsFalse(result.succeeded());
                Assert::AreEqual(size_t{ 1 }, fake.batches.size());
                Assert::AreEqual(0u, result.cleanup_requested);
            }
        }

        TEST_METHOD(SendChord_NoKeysAndNothingHeld_DoesNotCallSendInput)
        {
            fake_input fake;
            auto options = options_without_wait();
            options.suppress_start_menu = false;

            const auto result = input_injection::send_chord({}, options, fake.api());

            Assert::IsTrue(result.succeeded());
            Assert::AreEqual(0u, result.requested);
            Assert::IsTrue(fake.batches.empty());
        }

        TEST_METHOD(SendChord_NoModifiersHeld_InjectsNoModifierEvents)
        {
            fake_input fake;
            auto options = options_without_wait();
            options.suppress_start_menu = false;

            const auto result = input_injection::send_chord({ VK_RETURN }, options, fake.api());

            Assert::IsTrue(result.succeeded());
            Assert::AreEqual(size_t{ 1 }, fake.batches.size());
            Assert::AreEqual(size_t{ 2 }, fake.batches[0].size());
            assert_key(fake.batches[0][0], VK_RETURN, false);
            assert_key(fake.batches[0][1], VK_RETURN, true);
        }

        TEST_METHOD(SendChord_HeldModifiers_AreReleasedAndNeverPressedAgain)
        {
            fake_input fake;
            fake.held = { VK_LWIN, VK_LSHIFT, VK_RMENU };
            fake.send_limits = { 5 }; // Fail partway so the cleanup path runs too.

            input_injection::send_chord({ VK_CONTROL, vk_v }, options_without_wait(), fake.api());

            const auto& batch = fake.batches[0];
            assert_key(batch[0], input_injection::start_menu_suppression_key, true);
            assert_key(batch[1], VK_LWIN, true);
            assert_key(batch[2], VK_LSHIFT, true);
            assert_key(batch[3], VK_RMENU, true);
            assert_key(batch[4], VK_CONTROL, false);

            for (const auto& sent : fake.batches)
            {
                for (const auto& input : sent)
                {
                    const bool key_down = (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0;
                    if (key_down && is_modifier(input.ki.wVk))
                    {
                        Assert::AreEqual(static_cast<WORD>(VK_CONTROL), input.ki.wVk, L"Only the chord's own modifier may be pressed.");
                    }
                }
            }
        }

        TEST_METHOD(SendChord_DoesNotReleaseHeldModifiersWhenDisabled)
        {
            fake_input fake;
            fake.held = { VK_LCONTROL };
            auto options = options_without_wait();
            options.suppress_start_menu = false;
            options.release_held_modifiers = false;

            input_injection::send_chord({ vk_v }, options, fake.api());

            Assert::AreEqual(size_t{ 2 }, fake.batches[0].size());
            assert_key(fake.batches[0][0], vk_v, false);
        }

        TEST_METHOD(SendChord_WaitsForModifiersToBeReleased)
        {
            fake_input fake;
            fake.held = { VK_LCONTROL, VK_LSHIFT };
            fake.release_after_sleeps = 3;
            auto options = options_without_wait();
            options.modifier_release_timeout = std::chrono::milliseconds{ 1000 };

            const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options, fake.api());

            Assert::IsFalse(result.modifier_wait_timed_out);
            Assert::AreEqual(3, fake.sleeps);
            Assert::AreEqual(size_t{ 5 }, fake.batches[0].size(), L"No key-ups needed once the user let go.");
        }

        TEST_METHOD(SendChord_WaitTimesOut_ReleasesModifiersThatAreStillHeld)
        {
            fake_input fake;
            fake.held = { VK_RCONTROL };
            auto options = options_without_wait();
            options.modifier_release_timeout = std::chrono::milliseconds{ 50 };
            options.poll_interval = std::chrono::milliseconds{ 10 };

            const auto result = input_injection::send_chord({ VK_CONTROL, vk_v }, options, fake.api());

            Assert::IsTrue(result.modifier_wait_timed_out);
            Assert::AreEqual(5, fake.sleeps);
            assert_key(fake.batches[0][1], VK_RCONTROL, true);
            assert_key(fake.batches[0][2], VK_CONTROL, false);
        }
    };
}
