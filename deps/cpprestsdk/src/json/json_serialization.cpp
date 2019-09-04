/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * HTTP Library: JSON parser and writer
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#include "pch.h"

#include <stdio.h>

#ifndef _WIN32
#define __STDC_FORMAT_MACROS
#include <inttypes.h>
#endif

using namespace web;
using namespace web::json;
using namespace utility;
using namespace utility::conversions;

//
// JSON Serialization
//

#ifdef _WIN32
void web::json::value::serialize(std::ostream& stream) const
{
    // This has better performance than writing directly to stream.
    std::string str;
    m_value->serialize_impl(str);
    stream << str;
}
void web::json::value::format(std::basic_string<wchar_t>& string) const { m_value->format(string); }
#endif

void web::json::value::serialize(utility::ostream_t& stream) const
{
#ifndef _WIN32
    utility::details::scoped_c_thread_locale locale;
#endif

    // This has better performance than writing directly to stream.
    utility::string_t str;
    m_value->serialize_impl(str);
    stream << str;
}

void web::json::value::format(std::basic_string<char>& string) const { m_value->format(string); }

template<typename CharType>
void web::json::details::append_escape_string(std::basic_string<CharType>& str,
                                              const std::basic_string<CharType>& escaped)
{
    for (const auto& ch : escaped)
    {
        switch (ch)
        {
            case '\"':
                str += '\\';
                str += '\"';
                break;
            case '\\':
                str += '\\';
                str += '\\';
                break;
            case '\b':
                str += '\\';
                str += 'b';
                break;
            case '\f':
                str += '\\';
                str += 'f';
                break;
            case '\r':
                str += '\\';
                str += 'r';
                break;
            case '\n':
                str += '\\';
                str += 'n';
                break;
            case '\t':
                str += '\\';
                str += 't';
                break;
            default:

                // If a control character then must unicode escaped.
                if (ch >= 0 && ch <= 0x1F)
                {
                    static const std::array<CharType, 16> intToHex = {
                        {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'}};
                    str += '\\';
                    str += 'u';
                    str += '0';
                    str += '0';
                    str += intToHex[(ch & 0xF0) >> 4];
                    str += intToHex[ch & 0x0F];
                }
                else
                {
                    str += ch;
                }
        }
    }
}

void web::json::details::format_string(const utility::string_t& key, utility::string_t& str)
{
    str.push_back('"');
    append_escape_string(str, key);
    str.push_back('"');
}

#ifdef _WIN32
void web::json::details::format_string(const utility::string_t& key, std::string& str)
{
    str.push_back('"');
    append_escape_string(str, utility::conversions::to_utf8string(key));
    str.push_back('"');
}
#endif

void web::json::details::_String::format(std::basic_string<char>& str) const
{
    str.push_back('"');

    if (m_has_escape_char)
    {
        append_escape_string(str, utility::conversions::to_utf8string(m_string));
    }
    else
    {
        str.append(utility::conversions::to_utf8string(m_string));
    }

    str.push_back('"');
}

void web::json::details::_Number::format(std::basic_string<char>& stream) const
{
    if (m_number.m_type != number::type::double_type)
    {
        // #digits + 1 to avoid loss + 1 for the sign + 1 for null terminator.
        const size_t tempSize = std::numeric_limits<uint64_t>::digits10 + 3;
        char tempBuffer[tempSize];

#ifdef _WIN32
        // This can be improved performance-wise if we implement our own routine
        if (m_number.m_type == number::type::signed_type)
            _i64toa_s(m_number.m_intval, tempBuffer, tempSize, 10);
        else
            _ui64toa_s(m_number.m_uintval, tempBuffer, tempSize, 10);

        const auto numChars = strnlen_s(tempBuffer, tempSize);
#else
        int numChars;
        if (m_number.m_type == number::type::signed_type)
            numChars = snprintf(tempBuffer, tempSize, "%" PRId64, m_number.m_intval);
        else
            numChars = snprintf(tempBuffer, tempSize, "%" PRIu64, m_number.m_uintval);
#endif
        stream.append(tempBuffer, numChars);
    }
    else
    {
        // #digits + 2 to avoid loss + 1 for the sign + 1 for decimal point + 5 for exponent (e+xxx) + 1 for null
        // terminator
        const size_t tempSize = std::numeric_limits<double>::digits10 + 10;
        char tempBuffer[tempSize];
#ifdef _WIN32
        const auto numChars = _sprintf_s_l(tempBuffer,
                                           tempSize,
                                           "%.*g",
                                           utility::details::scoped_c_thread_locale::c_locale(),
                                           std::numeric_limits<double>::digits10 + 2,
                                           m_number.m_value);
#else
        const auto numChars =
            snprintf(tempBuffer, tempSize, "%.*g", std::numeric_limits<double>::digits10 + 2, m_number.m_value);
#endif
        stream.append(tempBuffer, numChars);
    }
}

#ifdef _WIN32

void web::json::details::_String::format(std::basic_string<wchar_t>& str) const
{
    str.push_back(L'"');

    if (m_has_escape_char)
    {
        append_escape_string(str, m_string);
    }
    else
    {
        str.append(m_string);
    }

    str.push_back(L'"');
}

void web::json::details::_Number::format(std::basic_string<wchar_t>& stream) const
{
    if (m_number.m_type != number::type::double_type)
    {
        // #digits + 1 to avoid loss + 1 for the sign + 1 for null terminator.
        const size_t tempSize = std::numeric_limits<uint64_t>::digits10 + 3;
        wchar_t tempBuffer[tempSize];

        if (m_number.m_type == number::type::signed_type)
            _i64tow_s(m_number.m_intval, tempBuffer, tempSize, 10);
        else
            _ui64tow_s(m_number.m_uintval, tempBuffer, tempSize, 10);

        stream.append(tempBuffer, wcsnlen_s(tempBuffer, tempSize));
    }
    else
    {
        // #digits + 2 to avoid loss + 1 for the sign + 1 for decimal point + 5 for exponent (e+xxx) + 1 for null
        // terminator
        const size_t tempSize = std::numeric_limits<double>::digits10 + 10;
        wchar_t tempBuffer[tempSize];
        const int numChars = _swprintf_s_l(tempBuffer,
                                           tempSize,
                                           L"%.*g",
                                           utility::details::scoped_c_thread_locale::c_locale(),
                                           std::numeric_limits<double>::digits10 + 2,
                                           m_number.m_value);
        stream.append(tempBuffer, numChars);
    }
}

#endif

const utility::string_t& web::json::details::_String::as_string() const { return m_string; }

const utility::string_t& web::json::value::as_string() const { return m_value->as_string(); }

utility::string_t json::value::serialize() const
{
#ifndef _WIN32
    utility::details::scoped_c_thread_locale locale;
#endif
    return m_value->to_string();
}
