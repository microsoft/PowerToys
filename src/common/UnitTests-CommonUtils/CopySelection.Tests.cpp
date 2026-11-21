#include "pch.h"
#include "TestHelpers.h"
#include <utils/copy_selection.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        constexpr ULONG_PTR test_extra_info = 0x110;
        const HWND focused_window = reinterpret_cast<HWND>(static_cast<INT_PTR>(0x1234));
        const HWND active_window = reinterpret_cast<HWND>(static_cast<INT_PTR>(0x5678));

        // Serves a fake clipboard sequence number, foreground GUI state and SendInput.
        struct fake_system
        {
            DWORD sequence = 100;
            int sleeps = 0;

            // The sequence number changes on the next check after WM_COPY / Ctrl+C when set.
            bool wm_copy_changes_clipboard = false;
            bool ctrl_c_changes_clipboard = false;
            int change_after_sleeps = 0; // Extra polling sleeps before the change becomes visible.

            bool gui_thread_info_succeeds = true;
            HWND focus = focused_window;
            HWND active = active_window;
            bool wm_copy_delivered = true;
            std::vector<HWND> wm_copy_targets;

            std::vector<std::vector<INPUT>> batches;
            std::vector<UINT> send_limits; // How many events each SendInput call accepts; all when not set.

            int pending_change_at = -1;

            void schedule_change()
            {
                pending_change_at = sleeps + change_after_sleeps;
            }

            copy_selection::system_api api()
            {
                copy_selection::system_api result;
                result.get_clipboard_sequence_number = [this] {
                    if (pending_change_at >= 0 && sleeps >= pending_change_at)
                    {
                        ++sequence;
                        pending_change_at = -1;
                    }

                    return sequence;
                };
                result.get_gui_thread_info = [this](GUITHREADINFO& info) {
                    if (!gui_thread_info_succeeds)
                    {
                        return false;
                    }

                    info.hwndFocus = focus;
                    info.hwndActive = active;
                    return true;
                };
                result.send_copy_message = [this](HWND target, std::chrono::milliseconds) {
                    wm_copy_targets.push_back(target);
                    if (wm_copy_delivered && wm_copy_changes_clipboard)
                    {
                        schedule_change();
                    }

                    return wm_copy_delivered;
                };
                result.input.send_input = [this](UINT count, INPUT* inputs) {
                    const UINT accepted = batches.size() < send_limits.size() ? (std::min)(count, send_limits[batches.size()]) : count;
                    batches.emplace_back(inputs, inputs + count);
                    if (accepted == count && ctrl_c_changes_clipboard)
                    {
                        schedule_change();
                    }

                    return accepted;
                };
                result.input.is_key_down = [](WORD) { return false; };
                result.input.get_last_error = [] { return static_cast<DWORD>(ERROR_ACCESS_DENIED); };
                result.input.sleep = [this](std::chrono::milliseconds) { ++sleeps; };
                return result;
            }
        };

        constexpr copy_selection::copy_options test_options()
        {
            copy_selection::copy_options options;
            options.clipboard_timeout = std::chrono::milliseconds{ 150 };
            options.clipboard_poll_interval = std::chrono::milliseconds{ 30 };
            options.chord.extra_info = test_extra_info;
            return options;
        }

        bool is_ctrl_c_batch(const std::vector<INPUT>& batch)
        {
            return std::any_of(batch.begin(), batch.end(), [](const INPUT& input) {
                return input.ki.wVk == 'C' && (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0;
            });
        }
    }

    TEST_CLASS(CopySelectionTests)
    {
    public:
        TEST_METHOD(Copy_WmCopyChangesClipboard_DoesNotSendCtrlC)
        {
            fake_system fake;
            fake.wm_copy_changes_clipboard = true;
            fake.change_after_sleeps = 2;

            const auto result = copy_selection::copy(test_options(), fake.api());

            Assert::IsTrue(result.copied());
            Assert::IsTrue(result.method == copy_selection::copy_method::wm_copy);
            Assert::AreEqual(size_t{ 1 }, result.attempts.size());
            Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::sent);
            Assert::IsFalse(result.attempts[0].ctrl_c_attempted);
            Assert::IsTrue(fake.batches.empty());
            Assert::AreEqual(size_t{ 1 }, fake.wm_copy_targets.size());
            Assert::IsTrue(fake.wm_copy_targets[0] == focused_window);
            Assert::AreEqual(2, fake.sleeps);
        }

        TEST_METHOD(Copy_NoFocusedWindow_SendsWmCopyToActiveWindow)
        {
            fake_system fake;
            fake.focus = nullptr;
            fake.wm_copy_changes_clipboard = true;

            const auto result = copy_selection::copy(test_options(), fake.api());

            Assert::IsTrue(result.copied());
            Assert::IsTrue(fake.wm_copy_targets[0] == active_window);
        }

        TEST_METHOD(Copy_WmCopyDoesNotChangeClipboard_FallsBackToTaggedCtrlC)
        {
            fake_system fake;
            fake.ctrl_c_changes_clipboard = true;
            fake.change_after_sleeps = 1;

            const auto result = copy_selection::copy(test_options(), fake.api());

            Assert::IsTrue(result.copied());
            Assert::IsTrue(result.method == copy_selection::copy_method::ctrl_c);
            Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::sent);
            Assert::IsTrue(result.attempts[0].ctrl_c_attempted);
            Assert::IsTrue(result.attempts[0].ctrl_c.succeeded());
            Assert::AreEqual(size_t{ 1 }, fake.batches.size());
            Assert::IsTrue(is_ctrl_c_batch(fake.batches[0]));
            for (const auto& input : fake.batches[0])
            {
                Assert::AreEqual(static_cast<unsigned long long>(test_extra_info), static_cast<unsigned long long>(input.ki.dwExtraInfo));
            }

            // 150ms / 30ms after WM_COPY, then one more poll after Ctrl+C.
            Assert::AreEqual(6, fake.sleeps);
        }

        TEST_METHOD(Copy_WmCopyDisabled_OnlySendsCtrlC)
        {
            fake_system fake;
            fake.ctrl_c_changes_clipboard = true;
            auto options = test_options();
            options.try_wm_copy = false;

            const auto result = copy_selection::copy(options, fake.api());

            Assert::IsTrue(result.copied());
            Assert::IsTrue(result.method == copy_selection::copy_method::ctrl_c);
            Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::not_attempted);
            Assert::IsTrue(fake.wm_copy_targets.empty());
        }

        TEST_METHOD(Copy_WmCopyUnavailable_ReportsWhyAndFallsBackToCtrlC)
        {
            {
                fake_system fake;
                fake.gui_thread_info_succeeds = false;
                fake.ctrl_c_changes_clipboard = true;

                const auto result = copy_selection::copy(test_options(), fake.api());

                Assert::IsTrue(result.copied());
                Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::gui_thread_info_failed);
                Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), result.attempts[0].wm_copy_error);
                Assert::AreEqual(0, fake.sleeps, L"No clipboard polling when WM_COPY wasn't sent.");
            }
            {
                fake_system fake;
                fake.focus = nullptr;
                fake.active = nullptr;
                fake.ctrl_c_changes_clipboard = true;

                const auto result = copy_selection::copy(test_options(), fake.api());

                Assert::IsTrue(result.copied());
                Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::no_target_window);
                Assert::IsTrue(fake.wm_copy_targets.empty());
            }
            {
                fake_system fake;
                fake.wm_copy_delivered = false;
                fake.ctrl_c_changes_clipboard = true;

                const auto result = copy_selection::copy(test_options(), fake.api());

                Assert::IsTrue(result.copied());
                Assert::IsTrue(result.attempts[0].wm_copy == copy_selection::wm_copy_outcome::send_failed);
                Assert::AreEqual(0, fake.sleeps);
            }
        }

        TEST_METHOD(Copy_ClipboardNeverChanges_TimesOutAfterEveryAttempt)
        {
            fake_system fake;
            auto options = test_options();
            options.attempts = 2;

            const auto result = copy_selection::copy(options, fake.api());

            Assert::IsFalse(result.copied());
            Assert::IsTrue(result.status == copy_selection::copy_status::timed_out);
            Assert::IsTrue(result.method == copy_selection::copy_method::none);
            Assert::AreEqual(size_t{ 2 }, result.attempts.size());
            Assert::AreEqual(size_t{ 2 }, fake.wm_copy_targets.size());
            Assert::AreEqual(size_t{ 2 }, fake.batches.size());

            // Per attempt: 5 polls after WM_COPY and 5 after Ctrl+C; plus one retry delay.
            Assert::AreEqual(21, fake.sleeps);
        }

        TEST_METHOD(Copy_SecondAttemptSucceeds_StopsThere)
        {
            fake_system fake;
            fake.ctrl_c_changes_clipboard = true;
            fake.send_limits = { 0 }; // The first Ctrl+C is blocked.
            auto options = test_options();
            options.attempts = 3;
            options.try_wm_copy = false;

            const auto result = copy_selection::copy(options, fake.api());

            Assert::IsTrue(result.copied());
            Assert::AreEqual(size_t{ 2 }, result.attempts.size());
            Assert::IsFalse(result.attempts[0].ctrl_c.succeeded());
            Assert::IsTrue(result.attempts[1].ctrl_c.succeeded());
        }

        TEST_METHOD(Copy_CtrlCPartiallySent_ReportsInjectionFailureWithoutPolling)
        {
            fake_system fake;
            fake.send_limits = { 2 }; // Start menu suppression and Ctrl down; C never goes out.
            auto options = test_options();
            options.try_wm_copy = false;

            const auto result = copy_selection::copy(options, fake.api());

            Assert::IsTrue(result.status == copy_selection::copy_status::injection_failed);
            Assert::IsFalse(result.copied());
            const auto& ctrl_c = result.attempts[0].ctrl_c;
            Assert::AreEqual(2u, ctrl_c.sent);
            Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), ctrl_c.error);
            Assert::AreEqual(1u, ctrl_c.cleanup_requested, L"Ctrl went down without its key-up.");
            Assert::AreEqual(0, fake.sleeps);
        }

        TEST_METHOD(WaitForClipboardChange_ChecksAgainAtTimeout)
        {
            fake_system fake;
            fake.pending_change_at = 5;

            Assert::IsTrue(copy_selection::wait_for_clipboard_change(fake.sequence, std::chrono::milliseconds{ 150 }, std::chrono::milliseconds{ 30 }, fake.api()));
            Assert::AreEqual(5, fake.sleeps);
        }
    };
}
