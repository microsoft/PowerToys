#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <atlstr.h>

#include <common/utils/language_helper.h>


inline std::wstring get_english_fallback_string(UINT resource_id, HINSTANCE instance)
{
    // Try to load en-us string as the first fallback.
    WORD english_language = MAKELANGID(LANG_ENGLISH, SUBLANG_ENGLISH_US);

    ATL::CStringW english_string;
    try
    {
        if (!english_string.LoadStringW(instance, resource_id, english_language))
        {
            return {};
        }
    }
    catch (...)
    {
        return {};
    }

    return std::wstring(english_string);
}

inline std::wstring get_resource_string_language_override(UINT resource_id, HINSTANCE instance)
{
    static std::wstring language = LanguageHelpers::load_language();
    unsigned lang = LANG_ENGLISH;
    unsigned sublang = SUBLANG_ENGLISH_US;

    if (!language.empty())
    {
        // Language list taken from Resources.wxs
        if (language == L"ar-SA")
        {
            lang = LANG_ARABIC;
            sublang = SUBLANG_ARABIC_SAUDI_ARABIA;
        }
        else if (language == L"cs-CZ")
        {
            lang = LANG_CZECH;
            sublang = SUBLANG_CZECH_CZECH_REPUBLIC;
        }
        else if (language == L"de-DE")
        {
            lang = LANG_GERMAN;
            sublang = SUBLANG_GERMAN;
        }
        else if (language == L"en-US")
        {
            lang = LANG_ENGLISH;
            sublang = SUBLANG_ENGLISH_US;
        }
        else if (language == L"es-ES")
        {
            lang = LANG_SPANISH;
            sublang = SUBLANG_SPANISH;
        }
        else if (language == L"fa-IR")
        {
            lang = LANG_PERSIAN;
            sublang = SUBLANG_PERSIAN_IRAN;
        }
        else if (language == L"fr-FR")
        {
            lang = LANG_FRENCH;
            sublang = SUBLANG_FRENCH;
        }
        else if (language == L"he-IL")
        {
            lang = LANG_HEBREW;
            sublang = SUBLANG_HEBREW_ISRAEL;
        }
        else if (language == L"hu-HU")
        {
            lang = LANG_HUNGARIAN;
            sublang = SUBLANG_HUNGARIAN_HUNGARY;
        }
        else if (language == L"it-IT")
        {
            lang = LANG_ITALIAN;
            sublang = SUBLANG_ITALIAN;
        }
        else if (language == L"ja-JP")
        {
            lang = LANG_JAPANESE;
            sublang = SUBLANG_JAPANESE_JAPAN;
        }
        else if (language == L"ko-KR")
        {
            lang = LANG_KOREAN;
            sublang = SUBLANG_KOREAN;
        }
        else if (language == L"nl-NL")
        {
            lang = LANG_DUTCH;
            sublang = SUBLANG_DUTCH;
        }
        else if (language == L"pl-PL")
        {
            lang = LANG_POLISH;
            sublang = SUBLANG_POLISH_POLAND;
        }
        else if (language == L"pt-BR")
        {
            lang = LANG_PORTUGUESE;
            sublang = SUBLANG_PORTUGUESE_BRAZILIAN;
        }
        else if (language == L"pt-PT")
        {
            lang = LANG_PORTUGUESE;
            sublang = SUBLANG_PORTUGUESE;
        }
        else if (language == L"ru-RU")
        {
            lang = LANG_RUSSIAN;
            sublang = SUBLANG_RUSSIAN_RUSSIA;
        }
        else if (language == L"sv-SE")
        {
            lang = LANG_SWEDISH;
            sublang = SUBLANG_SWEDISH;
        }
        else if (language == L"tr-TR")
        {
            lang = LANG_TURKISH;
            sublang = SUBLANG_TURKISH_TURKEY;
        }
        else if (language == L"uk-UA")
        {
            lang = LANG_UKRAINIAN;
            sublang = SUBLANG_UKRAINIAN_UKRAINE;
        }
        else if (language == L"zh-CN")
        {
            lang = LANG_CHINESE_SIMPLIFIED;
            sublang = SUBLANG_CHINESE_SIMPLIFIED;
        }
        else if (language == L"zh-TW")
        {
            lang = LANG_CHINESE_TRADITIONAL;
            sublang = SUBLANG_CHINESE_TRADITIONAL;
        }

        WORD languageID = MAKELANGID(lang, sublang);
        ATL::CStringW result;
        try
        {
            if (!result.LoadStringW(instance, resource_id, languageID))
            {
                return {};
            }
        }
        catch (...)
        {
            return {};
        }

        if (!result.IsEmpty())
        {
            return std::wstring(result);
        }
    }

    return {};
}

// Get a string from the resource file
inline std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback)
{
    // Try to load en-us string as the first fallback.
    std::wstring english_string = get_english_fallback_string(resource_id, instance);

    std::wstring language_override_resource = get_resource_string_language_override(resource_id, instance);

    if (!language_override_resource.empty())
    {
        return language_override_resource;
    }
    else
    {
        wchar_t* text_ptr;
        auto length = LoadStringW(instance, resource_id, reinterpret_cast<wchar_t*>(&text_ptr), 0);
        if (length == 0)
        {
            if (!english_string.empty())
            {
                return std::wstring(english_string);
            }
            else
            {
                return fallback;
            }
        }
        else
        {
            return { text_ptr, static_cast<std::size_t>(length) };
        }
    }
}

extern "C" IMAGE_DOS_HEADER __ImageBase;
// Wrapper for getting a string from the resource file. Returns the resource id text when fails.
#define GET_RESOURCE_STRING(resource_id) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), L#resource_id)
#define GET_RESOURCE_STRING_FALLBACK(resource_id, fallback) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), fallback)
