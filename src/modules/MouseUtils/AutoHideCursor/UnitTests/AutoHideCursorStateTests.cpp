#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "..\AutoHideCursorState.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace auto_hide_cursor;

namespace AutoHideCursorUnitTests
{
    TEST_CLASS (AutoHideCursorStateTests)
    {
    public:
        TEST_METHOD (NeitherTriggerSelectedNeverHides)
        {
            State state{ { false, false, defaultIdleDelayMs }, 0, { 10, 10 } };

            Assert::IsTrue(state.OnKeyboardInput(100, { 10, 10 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(10000, { 10, 10 }) == CursorAction::None);
            Assert::IsFalse(state.IsHidden());
        }

        TEST_METHOD (KeyboardInputHidesAndIntentionalMovementShows)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };

            Assert::IsTrue(state.OnKeyboardInput(100, { 10, 10 }) == CursorAction::Hide);
            Assert::IsTrue(state.OnMouseInput(101, { 11, 10 }, MouseInputKind::Move) == CursorAction::None);
            Assert::IsTrue(state.OnMouseInput(102, { 12, 10 }, MouseInputKind::Move) == CursorAction::Show);
            Assert::IsFalse(state.IsHidden());
        }

        TEST_METHOD (MouseButtonShowsWithoutMovement)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };
            state.OnKeyboardInput(100, { 10, 10 });

            Assert::IsTrue(state.OnMouseInput(101, { 10, 10 }, MouseInputKind::ButtonOrWheel) == CursorAction::Show);
        }

        TEST_METHOD (IdleTimeoutUsesPointerActivity)
        {
            State state{ { false, true, 5000 }, 0, { 10, 10 } };

            Assert::IsTrue(state.OnTimer(4999, { 10, 10 }) == CursorAction::None);
            state.OnMouseInput(4000, { 20, 20 }, MouseInputKind::Move);
            Assert::IsTrue(state.OnTimer(8999, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(9000, { 20, 20 }) == CursorAction::Hide);
        }

        TEST_METHOD (IdleDelayIsClamped)
        {
            const auto tooShort = State::NormalizeConfiguration({ false, true, 1 });
            const auto tooLong = State::NormalizeConfiguration({ false, true, UINT32_MAX });

            Assert::AreEqual(minimumIdleDelayMs, tooShort.idleDelayMs);
            Assert::AreEqual(maximumIdleDelayMs, tooLong.idleDelayMs);
        }

        TEST_METHOD (TimerRestoresTypingHiddenCursorWithoutMouseHook)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };
            state.OnKeyboardInput(100, { 10, 10 });

            Assert::IsTrue(state.OnTimer(200, { 11, 10 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(300, { 12, 10 }) == CursorAction::Show);
            Assert::IsFalse(state.IsHidden());
            Assert::IsTrue(state.OnTimer(400, { 12, 10 }) == CursorAction::None);
        }

        TEST_METHOD (TimerRestoresIdleHiddenCursorWithoutMouseHook)
        {
            State state{ { false, true, 1000 }, 0, { 10, 10 } };
            Assert::IsTrue(state.OnTimer(1000, { 10, 10 }) == CursorAction::Hide);
            Assert::IsTrue(state.OnTimer(1100, { 10, 12 }) == CursorAction::Show);
            Assert::IsTrue(state.OnTimer(2099, { 10, 12 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(2100, { 10, 12 }) == CursorAction::Hide);
        }

        TEST_METHOD (PolledMovementResetsIdleDeadlineWithoutMouseHook)
        {
            State state{ { false, true, 1000 }, 0, { 10, 10 } };
            Assert::IsTrue(state.OnTimer(900, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(1000, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(1899, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(1900, { 20, 20 }) == CursorAction::Hide);
        }

        TEST_METHOD (TypingUsesCurrentPositionForPolledMovement)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };
            state.OnKeyboardInput(100, { 20, 20 });
            Assert::IsTrue(state.OnTimer(200, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.IsHidden());
        }

        TEST_METHOD (PolledMovementUsesDistanceFromHiddenPosition)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };
            state.OnKeyboardInput(100, { 10, 10 });

            Assert::IsTrue(state.OnTimer(200, { 9, 9 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(300, { 8, 10 }) == CursorAction::Show);
        }

        TEST_METHOD (MouseHookMovementIsNotCountedAgainByTimer)
        {
            State state{ { false, true, 1000 }, 0, { 10, 10 } };
            state.OnMouseInput(100, { 20, 20 }, MouseInputKind::Move);
            Assert::IsTrue(state.OnTimer(1000, { 20, 20 }) == CursorAction::None);
            Assert::IsTrue(state.OnTimer(1100, { 20, 20 }) == CursorAction::Hide);
        }

        TEST_METHOD (ModifierKeysDoNotCountAsTyping)
        {
            Assert::IsTrue(State::IsModifierVirtualKey(0x10));
            Assert::IsTrue(State::IsModifierVirtualKey(0xA3));
            Assert::IsFalse(State::IsModifierVirtualKey('A'));
            Assert::IsFalse(State::IsModifierVirtualKey(0x08)); // VK_BACK
        }

        TEST_METHOD (StopRestoresOnlyWhenHidden)
        {
            State state{ { true, false, defaultIdleDelayMs }, 0, { 10, 10 } };

            Assert::IsTrue(state.Stop() == CursorAction::None);
            state.OnKeyboardInput(100, { 10, 10 });
            Assert::IsTrue(state.Stop() == CursorAction::Show);
            Assert::IsTrue(state.Stop() == CursorAction::None);
        }
    };
}
