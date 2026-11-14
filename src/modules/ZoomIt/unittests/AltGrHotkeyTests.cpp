#include "pch.h"
#include <CppUnitTest.h>

#include <algorithm>
#include <vector>

#include "Utility.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AltGrHotkeyTests
{
    // Loads a keyboard layout for the test and unloads it afterwards unless the
    // user already had it installed.
    class ScopedLayout
    {
    public:
        explicit ScopedLayout( const wchar_t* layoutId )
        {
            std::vector<HKL> installed( GetKeyboardLayoutList( 0, nullptr ) );
            GetKeyboardLayoutList( static_cast<int>( installed.size() ), installed.data() );
            m_layout = LoadKeyboardLayoutW( layoutId, KLF_NOTELLSHELL );
            Assert::IsNotNull( m_layout );
            m_unload = std::find( installed.begin(), installed.end(), m_layout ) == installed.end();
        }

        ~ScopedLayout()
        {
            if( m_unload )
            {
                UnloadKeyboardLayout( m_layout );
            }
        }

        operator HKL() const { return m_layout; }

    private:
        HKL m_layout{};
        bool m_unload{};
    };

    constexpr UINT CtrlAlt = MOD_CONTROL | MOD_ALT;

    TEST_CLASS( IsAltGrCharacterHotkeyTests )
    {
    public:
        TEST_METHOD( GermanCtrlAltDigitsThatTypeBracketsCollide )
        {
            ScopedLayout german( L"00000407" );

            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, '9', german ) ); // ]  (Mirror window default)
            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, '8', german ) ); // [
            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, 'Q', german ) ); // @
            Assert::IsFalse( IsAltGrCharacterHotkey( CtrlAlt, '5', german ) ); // AltGr+5 types nothing
        }

        TEST_METHOD( FrenchRecordAndOcrDefaultsCollide )
        {
            ScopedLayout french( L"0000040C" );

            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, '5', french ) ); // [  (Record window default)
            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, '6', french ) ); // |  (Snip OCR default)
            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt, '2', french ) ); // ~  dead key
        }

        TEST_METHOD( UsLayoutHasNoAltGrCharacters )
        {
            ScopedLayout us( L"00000409" );

            Assert::IsFalse( IsAltGrCharacterHotkey( CtrlAlt, '9', us ) );
            Assert::IsFalse( IsAltGrCharacterHotkey( CtrlAlt, '5', us ) );
            Assert::IsFalse( IsAltGrCharacterHotkey( CtrlAlt, '6', us ) );
        }

        TEST_METHOD( HotkeysWithoutBothCtrlAndAltOrWithWinNeverCollide )
        {
            ScopedLayout german( L"00000407" );

            Assert::IsFalse( IsAltGrCharacterHotkey( MOD_CONTROL, '9', german ) );
            Assert::IsFalse( IsAltGrCharacterHotkey( MOD_ALT, '9', german ) );
            Assert::IsFalse( IsAltGrCharacterHotkey( CtrlAlt | MOD_WIN, '9', german ) );
        }

        TEST_METHOD( NoRepeatFlagIsIgnored )
        {
            ScopedLayout german( L"00000407" );

            Assert::IsTrue( IsAltGrCharacterHotkey( CtrlAlt | MOD_NOREPEAT, '9', german ) );
        }
    };
}
