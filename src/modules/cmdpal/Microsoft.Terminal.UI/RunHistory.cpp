#include "pch.h"
#include "RunHistory.h"
#include "RunHistory.g.cpp"


using namespace winrt::Windows;

namespace winrt::Microsoft::Terminal::UI::implementation
{
    // Run history
    // Largely copied from the Run work circa 2022.

    winrt::Windows::Foundation::Collections::IVector<hstring> RunHistory::CreateRunHistory()
    {
        // Load MRU history
        std::vector<hstring> history;

        wil::unique_hmodule _comctl;
        HANDLE(WINAPI* _createMRUList)(MRUINFO* lpmi);
        int(WINAPI* _enumMRUList)(HANDLE hMRU,int nItem,void* lpData,UINT uLen);
        void(WINAPI *_freeMRUList)(HANDLE hMRU);
        int(WINAPI *_addMRUString)(HANDLE hMRU, LPCWSTR szString);

        // Lazy load comctl32.dll
        // Theoretically, we could cache this into a magic static, but we shouldn't need to actually do this more than once in CmdPal
        _comctl.reset(LoadLibraryExW(L"comctl32.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32));

        _createMRUList = reinterpret_cast<decltype(_createMRUList)>(GetProcAddress(_comctl.get(), "CreateMRUListW"));
        FAIL_FAST_LAST_ERROR_IF(!_createMRUList);

        _enumMRUList = reinterpret_cast<decltype(_enumMRUList)>(GetProcAddress(_comctl.get(), "EnumMRUListW"));
        FAIL_FAST_LAST_ERROR_IF(!_enumMRUList);

        _freeMRUList = reinterpret_cast<decltype(_freeMRUList)>(GetProcAddress(_comctl.get(), "FreeMRUList"));
        FAIL_FAST_LAST_ERROR_IF(!_freeMRUList);

        _addMRUString = reinterpret_cast<decltype(_addMRUString)>(GetProcAddress(_comctl.get(), "AddMRUStringW"));
        FAIL_FAST_LAST_ERROR_IF(!_addMRUString);

        static const WCHAR c_szRunMRU[] = REGSTR_PATH_EXPLORER L"\\RunMRU";
        MRUINFO mi = {
            sizeof(mi),
            26,
            MRU_CACHEWRITE,
            HKEY_CURRENT_USER,
            c_szRunMRU,
            NULL // NOTE: use default string compare
            // since this is a GLOBAL MRU
        };

        if (const auto hmru = _createMRUList(&mi))
        {
            auto freeMRUList = wil::scope_exit([=]() {
                _freeMRUList(hmru);
            });

            for (int nMax = _enumMRUList(hmru, -1, NULL, 0), i = 0; i < nMax; ++i)
            {
                WCHAR szCommand[MAX_PATH + 2];

                const auto length = _enumMRUList(hmru, i, szCommand, ARRAYSIZE(szCommand));
                if (length > 1)
                {
                    // clip off the null-terminator
                    std::wstring_view text{ szCommand, wil::safe_cast<size_t>(length - 1) };
//#pragma disable warning(C26493)
#pragma warning( push )
#pragma warning( disable : 26493 )
                    if (text.back() == L'\\')
                    {
                        // old MRU format has a slash at the end with the show cmd
                        text = { szCommand, wil::safe_cast<size_t>(length - 2) };
#pragma warning( pop )
                        if (text.empty())
                        {
                            continue;
                        }
                    }
                    history.emplace_back(text);
                }
            }
        }

        // Update dropdown & initial value
        return winrt::single_threaded_observable_vector<winrt::hstring>(std::move(history));
    }
}
