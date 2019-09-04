/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * HTTP Library: JSON parser
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#include "pch.h"

#include <cstdlib>

#if defined(_MSC_VER)
#pragma warning(disable : 4127) // allow expressions like while(true) pass
#endif
using namespace web;
using namespace web::json;
using namespace utility;
using namespace utility::conversions;

std::array<signed char, 128> _hexval = {
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
     -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0,  1,  2,  3,
     4,  5,  6,  7,  8,  9,  -1, -1, -1, -1, -1, -1, -1, 10, 11, 12, 13, 14, 15, -1, -1, -1, -1, -1, -1, -1,
     -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 10, 11, 12, 13, 14, 15, -1,
     -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}};

namespace web
{
namespace json
{
namespace details
{
//
// JSON Parsing
//

template<typename Token>
#if defined(_WIN32)
__declspec(noreturn)
#else
    __attribute__((noreturn))
#endif
    void CreateException(const Token& tk, const utility::string_t& message)
{
    std::string str("* Line ");
    str += std::to_string(tk.start.m_line);
    str += ", Column ";
    str += std::to_string(tk.start.m_column);
    str += " Syntax error: ";
    str += utility::conversions::to_utf8string(message);
    throw web::json::json_exception(std::move(str));
}

template<typename Token>
void SetErrorCode(Token& tk, json_error jsonErrorCode)
{
    tk.m_error = std::error_code(jsonErrorCode, json_error_category());
}

template<typename CharType>
class JSON_Parser
{
public:
    JSON_Parser() : m_currentLine(1), m_currentColumn(1), m_currentParsingDepth(0) {}

    struct Location
    {
        size_t m_line;
        size_t m_column;
    };

    struct Token
    {
        enum Kind
        {
            TKN_EOF,

            TKN_OpenBrace,
            TKN_CloseBrace,
            TKN_OpenBracket,
            TKN_CloseBracket,
            TKN_Comma,
            TKN_Colon,
            TKN_StringLiteral,
            TKN_NumberLiteral,
            TKN_IntegerLiteral,
            TKN_BooleanLiteral,
            TKN_NullLiteral,
            TKN_Comment
        };

        Token() : kind(TKN_EOF) {}

        Kind kind;
        std::basic_string<CharType> string_val;

        typename JSON_Parser<CharType>::Location start;

        union {
            double double_val;
            int64_t int64_val;
            uint64_t uint64_val;
            bool boolean_val;
            bool has_unescape_symbol;
        };

        bool signed_number;

        std::error_code m_error;
    };

    void GetNextToken(Token&);

    web::json::value ParseValue(typename JSON_Parser<CharType>::Token& first)
    {
#ifndef _WIN32
        utility::details::scoped_c_thread_locale locale;
#endif

#ifdef ENABLE_JSON_VALUE_VISUALIZER
        auto _value = _ParseValue(first);
        auto type = _value->type();
        return web::json::value(std::move(_value), type);
#else
        return web::json::value(_ParseValue(first));
#endif
    }

protected:
    typedef typename std::char_traits<CharType>::int_type int_type;
    virtual int_type NextCharacter() = 0;
    virtual int_type PeekCharacter() = 0;

    virtual bool CompleteComment(Token& token);
    virtual bool CompleteStringLiteral(Token& token);
    int convert_unicode_to_code_point();
    bool handle_unescape_char(Token& token);

private:
    bool CompleteNumberLiteral(CharType first, Token& token);
    bool ParseInt64(CharType first, uint64_t& value);
    bool CompleteKeywordTrue(Token& token);
    bool CompleteKeywordFalse(Token& token);
    bool CompleteKeywordNull(Token& token);
    std::unique_ptr<web::json::details::_Value> _ParseValue(typename JSON_Parser<CharType>::Token& first);
    std::unique_ptr<web::json::details::_Value> _ParseObject(typename JSON_Parser<CharType>::Token& tkn);
    std::unique_ptr<web::json::details::_Value> _ParseArray(typename JSON_Parser<CharType>::Token& tkn);

    JSON_Parser& operator=(const JSON_Parser&);

    int_type EatWhitespace();

    void CreateToken(typename JSON_Parser<CharType>::Token& tk, typename Token::Kind kind, Location& start)
    {
        tk.kind = kind;
        tk.start = start;
        tk.string_val.clear();
    }

    void CreateToken(typename JSON_Parser<CharType>::Token& tk, typename Token::Kind kind)
    {
        tk.kind = kind;
        tk.start.m_line = m_currentLine;
        tk.start.m_column = m_currentColumn;
        tk.string_val.clear();
    }

protected:
    size_t m_currentLine;
    size_t m_currentColumn;
    size_t m_currentParsingDepth;

// The DEBUG macro is defined in XCode but we don't in our CMakeList
// so for now we will keep the same on debug and release. In the future
// this can be increase on release if necessary.
#if defined(__APPLE__)
    static const size_t maxParsingDepth = 32;
#else
    static const size_t maxParsingDepth = 128;
#endif
};

// Replace with template alias once VS 2012 support is removed.
template<typename CharType>
typename std::char_traits<CharType>::int_type eof()
{
    return std::char_traits<CharType>::eof();
}

template<typename CharType>
class JSON_StreamParser : public JSON_Parser<CharType>
{
public:
    JSON_StreamParser(std::basic_istream<CharType>& stream) : m_streambuf(stream.rdbuf()) {}

protected:
    virtual typename JSON_Parser<CharType>::int_type NextCharacter();
    virtual typename JSON_Parser<CharType>::int_type PeekCharacter();

private:
    typename std::basic_streambuf<CharType, std::char_traits<CharType>>* m_streambuf;
};

template<typename CharType>
class JSON_StringParser : public JSON_Parser<CharType>
{
public:
    JSON_StringParser(const std::basic_string<CharType>& string) : m_position(&string[0])
    {
        m_startpos = m_position;
        m_endpos = m_position + string.size();
    }

protected:
    virtual typename JSON_Parser<CharType>::int_type NextCharacter();
    virtual typename JSON_Parser<CharType>::int_type PeekCharacter();

    virtual bool CompleteComment(typename JSON_Parser<CharType>::Token& token);
    virtual bool CompleteStringLiteral(typename JSON_Parser<CharType>::Token& token);

private:
    bool finish_parsing_string_with_unescape_char(typename JSON_Parser<CharType>::Token& token);
    const CharType* m_position;
    const CharType* m_startpos;
    const CharType* m_endpos;
};

template<typename CharType>
typename JSON_Parser<CharType>::int_type JSON_StreamParser<CharType>::NextCharacter()
{
    auto ch = m_streambuf->sbumpc();

    if (ch == '\n')
    {
        this->m_currentLine += 1;
        this->m_currentColumn = 0;
    }
    else
    {
        this->m_currentColumn += 1;
    }

    return ch;
}

template<typename CharType>
typename JSON_Parser<CharType>::int_type JSON_StreamParser<CharType>::PeekCharacter()
{
    return m_streambuf->sgetc();
}

template<typename CharType>
typename JSON_Parser<CharType>::int_type JSON_StringParser<CharType>::NextCharacter()
{
    if (m_position == m_endpos) return eof<CharType>();

    CharType ch = *m_position;
    m_position += 1;

    if (ch == '\n')
    {
        this->m_currentLine += 1;
        this->m_currentColumn = 0;
    }
    else
    {
        this->m_currentColumn += 1;
    }

    return ch;
}

template<typename CharType>
typename JSON_Parser<CharType>::int_type JSON_StringParser<CharType>::PeekCharacter()
{
    if (m_position == m_endpos) return eof<CharType>();

    return *m_position;
}

//
// Consume whitespace characters and return the first non-space character or EOF
//
template<typename CharType>
typename JSON_Parser<CharType>::int_type JSON_Parser<CharType>::EatWhitespace()
{
    auto ch = NextCharacter();

    while (ch != eof<CharType>() && iswspace(static_cast<wint_t>(ch)))
    {
        ch = NextCharacter();
    }

    return ch;
}

template<typename CharType>
bool JSON_Parser<CharType>::CompleteKeywordTrue(Token& token)
{
    if (NextCharacter() != 'r') return false;
    if (NextCharacter() != 'u') return false;
    if (NextCharacter() != 'e') return false;
    token.kind = Token::TKN_BooleanLiteral;
    token.boolean_val = true;
    return true;
}

template<typename CharType>
bool JSON_Parser<CharType>::CompleteKeywordFalse(Token& token)
{
    if (NextCharacter() != 'a') return false;
    if (NextCharacter() != 'l') return false;
    if (NextCharacter() != 's') return false;
    if (NextCharacter() != 'e') return false;
    token.kind = Token::TKN_BooleanLiteral;
    token.boolean_val = false;
    return true;
}

template<typename CharType>
bool JSON_Parser<CharType>::CompleteKeywordNull(Token& token)
{
    if (NextCharacter() != 'u') return false;
    if (NextCharacter() != 'l') return false;
    if (NextCharacter() != 'l') return false;
    token.kind = Token::TKN_NullLiteral;
    return true;
}

// Returns false only on overflow
template<typename CharType>
inline bool JSON_Parser<CharType>::ParseInt64(CharType first, uint64_t& value)
{
    value = first - '0';
    auto ch = PeekCharacter();
    while (ch >= '0' && ch <= '9')
    {
        unsigned int next_digit = (unsigned int)(ch - '0');
        if (value > (ULLONG_MAX / 10) || (value == ULLONG_MAX / 10 && next_digit > ULLONG_MAX % 10)) return false;

        NextCharacter();

        value *= 10;
        value += next_digit;
        ch = PeekCharacter();
    }
    return true;
}

// This namespace hides the x-plat helper functions
namespace
{
#if defined(_WIN32)
static int print_llu(char* ptr, size_t n, uint64_t val64)
{
    return _snprintf_s_l(ptr, n, _TRUNCATE, "%I64u", utility::details::scoped_c_thread_locale::c_locale(), val64);
}

static int print_llu(wchar_t* ptr, size_t n, uint64_t val64)
{
    return _snwprintf_s_l(ptr, n, _TRUNCATE, L"%I64u", utility::details::scoped_c_thread_locale::c_locale(), val64);
}
static double anystod(const char* str)
{
    return _strtod_l(str, nullptr, utility::details::scoped_c_thread_locale::c_locale());
}
static double anystod(const wchar_t* str)
{
    return _wcstod_l(str, nullptr, utility::details::scoped_c_thread_locale::c_locale());
}
#else
static int __attribute__((__unused__)) print_llu(char* ptr, size_t n, unsigned long long val64)
{
    return snprintf(ptr, n, "%llu", val64);
}
static int __attribute__((__unused__)) print_llu(char* ptr, size_t n, unsigned long val64)
{
    return snprintf(ptr, n, "%lu", val64);
}
static double __attribute__((__unused__)) anystod(const char* str) { return strtod(str, nullptr); }
static double __attribute__((__unused__)) anystod(const wchar_t* str) { return wcstod(str, nullptr); }
#endif
} // namespace

template<typename CharType>
bool JSON_Parser<CharType>::CompleteNumberLiteral(CharType first, Token& token)
{
    bool minus_sign;

    if (first == '-')
    {
        minus_sign = true;

        // Safe to cast because the check after this if/else statement will cover EOF.
        first = static_cast<CharType>(NextCharacter());
    }
    else
    {
        minus_sign = false;
    }

    if (first < '0' || first > '9') return false;

    auto ch = PeekCharacter();

    // Check for two (or more) zeros at the beginning
    if (first == '0' && ch == '0') return false;

    // Parse the number assuming its integer
    uint64_t val64;
    bool complete = ParseInt64(first, val64);

    ch = PeekCharacter();
    if (complete && ch != '.' && ch != 'E' && ch != 'e')
    {
        if (minus_sign)
        {
            if (val64 > static_cast<uint64_t>(1) << 63)
            {
                // It is negative and cannot be represented in int64, so we resort to double
                token.double_val = 0 - static_cast<double>(val64);
                token.signed_number = true;
                token.kind = JSON_Parser<CharType>::Token::TKN_NumberLiteral;
                return true;
            }

            // It is negative, but fits into int64
            token.int64_val = 0 - static_cast<int64_t>(val64);
            token.kind = JSON_Parser<CharType>::Token::TKN_IntegerLiteral;
            token.signed_number = true;
            return true;
        }

        // It is positive so we use unsigned int64
        token.uint64_val = val64;
        token.kind = JSON_Parser<CharType>::Token::TKN_IntegerLiteral;
        token.signed_number = false;
        return true;
    }

    // Magic number 5 leaves room for decimal point, null terminator, etc (in most cases)
    ::std::vector<CharType> buf(::std::numeric_limits<uint64_t>::digits10 + 5);
    int count = print_llu(buf.data(), buf.size(), val64);
    _ASSERTE(count >= 0);
    _ASSERTE((size_t)count < buf.size());
    // Resize to cut off the null terminator
    buf.resize(count);

    bool decimal = false;

    while (ch != eof<CharType>())
    {
        // Digit encountered?
        if (ch >= '0' && ch <= '9')
        {
            buf.push_back(static_cast<CharType>(ch));
            NextCharacter();
            ch = PeekCharacter();
        }

        // Decimal dot?
        else if (ch == '.')
        {
            if (decimal) return false;

            decimal = true;
            buf.push_back(static_cast<CharType>(ch));

            NextCharacter();
            ch = PeekCharacter();

            // Check that the following char is a digit
            if (ch < '0' || ch > '9') return false;

            buf.push_back(static_cast<CharType>(ch));
            NextCharacter();
            ch = PeekCharacter();
        }

        // Exponent?
        else if (ch == 'E' || ch == 'e')
        {
            buf.push_back(static_cast<CharType>(ch));
            NextCharacter();
            ch = PeekCharacter();

            // Check for the exponent sign
            if (ch == '+')
            {
                buf.push_back(static_cast<CharType>(ch));
                NextCharacter();
                ch = PeekCharacter();
            }
            else if (ch == '-')
            {
                buf.push_back(static_cast<CharType>(ch));
                NextCharacter();
                ch = PeekCharacter();
            }

            // First number of the exponent
            if (ch >= '0' && ch <= '9')
            {
                buf.push_back(static_cast<CharType>(ch));
                NextCharacter();
                ch = PeekCharacter();
            }
            else
                return false;

            // The rest of the exponent
            while (ch >= '0' && ch <= '9')
            {
                buf.push_back(static_cast<CharType>(ch));
                NextCharacter();
                ch = PeekCharacter();
            }

            // The peeked character is not a number, so we can break from the loop and construct the number
            break;
        }
        else
        {
            // Not expected number character?
            break;
        }
    };

    buf.push_back('\0');
    token.double_val = anystod(buf.data());
    if (minus_sign)
    {
        token.double_val = -token.double_val;
    }
    token.kind = (JSON_Parser<CharType>::Token::TKN_NumberLiteral);

    return true;
}

template<typename CharType>
bool JSON_Parser<CharType>::CompleteComment(Token& token)
{
    // We already found a '/' character as the first of a token -- what kind of comment is it?

    auto ch = NextCharacter();

    if (ch == eof<CharType>() || (ch != '/' && ch != '*')) return false;

    if (ch == '/')
    {
        // Line comment -- look for a newline or EOF to terminate.

        ch = NextCharacter();

        while (ch != eof<CharType>() && ch != '\n')
        {
            ch = NextCharacter();
        }
    }
    else
    {
        // Block comment -- look for a terminating "*/" sequence.

        ch = NextCharacter();

        while (true)
        {
            if (ch == eof<CharType>()) return false;

            if (ch == '*')
            {
                auto ch1 = PeekCharacter();

                if (ch1 == eof<CharType>()) return false;

                if (ch1 == '/')
                {
                    // Consume the character
                    NextCharacter();
                    break;
                }

                ch = ch1;
            }

            ch = NextCharacter();
        }
    }

    token.kind = Token::TKN_Comment;

    return true;
}

template<typename CharType>
bool JSON_StringParser<CharType>::CompleteComment(typename JSON_Parser<CharType>::Token& token)
{
    // This function is specialized for the string parser, since we can be slightly more
    // efficient in copying data from the input to the token: do a memcpy() rather than
    // one character at a time.

    auto ch = JSON_StringParser<CharType>::NextCharacter();

    if (ch == eof<CharType>() || (ch != '/' && ch != '*')) return false;

    if (ch == '/')
    {
        // Line comment -- look for a newline or EOF to terminate.

        ch = JSON_StringParser<CharType>::NextCharacter();

        while (ch != eof<CharType>() && ch != '\n')
        {
            ch = JSON_StringParser<CharType>::NextCharacter();
        }
    }
    else
    {
        // Block comment -- look for a terminating "*/" sequence.

        ch = JSON_StringParser<CharType>::NextCharacter();

        while (true)
        {
            if (ch == eof<CharType>()) return false;

            if (ch == '*')
            {
                ch = JSON_StringParser<CharType>::PeekCharacter();

                if (ch == eof<CharType>()) return false;

                if (ch == '/')
                {
                    // Consume the character
                    JSON_StringParser<CharType>::NextCharacter();
                    break;
                }
            }

            ch = JSON_StringParser<CharType>::NextCharacter();
        }
    }

    token.kind = JSON_Parser<CharType>::Token::TKN_Comment;

    return true;
}

void convert_append_unicode_code_unit(JSON_Parser<utf16char>::Token& token, utf16string value)
{
    token.string_val.append(value);
}
void convert_append_unicode_code_unit(JSON_Parser<char>::Token& token, utf16string value)
{
    token.string_val.append(::utility::conversions::utf16_to_utf8(value));
}
void convert_append_unicode_code_unit(JSON_Parser<utf16char>::Token& token, utf16char value)
{
    token.string_val.push_back(value);
}
void convert_append_unicode_code_unit(JSON_Parser<char>::Token& token, utf16char value)
{
    utf16string utf16(reinterpret_cast<utf16char*>(&value), 1);
    token.string_val.append(::utility::conversions::utf16_to_utf8(utf16));
}

template<typename CharType>
int JSON_Parser<CharType>::convert_unicode_to_code_point()
{
    // A four-hexdigit Unicode character.
    // Transform into a 16 bit code point.
    int decoded = 0;
    for (int i = 0; i < 4; ++i)
    {
        auto ch = NextCharacter();
        int ch_int = static_cast<int>(ch);
        if (ch_int < 0 || ch_int > 127) return -1;
#ifdef _WIN32
        const int isxdigitResult = _isxdigit_l(ch_int, utility::details::scoped_c_thread_locale::c_locale());
#else
        const int isxdigitResult = isxdigit(ch_int);
#endif
        if (!isxdigitResult) return -1;

        int val = _hexval[static_cast<size_t>(ch_int)];

        _ASSERTE(val != -1);

        // Add the input char to the decoded number
        decoded |= (val << (4 * (3 - i)));
    }
    return decoded;
}

#define H_SURROGATE_START 0xD800
#define H_SURROGATE_END 0xDBFF

template<typename CharType>
inline bool JSON_Parser<CharType>::handle_unescape_char(Token& token)
{
    token.has_unescape_symbol = true;

    // This function converts unescaped character pairs (e.g. "\t") into their ASCII or Unicode representations (e.g.
    // tab sign) Also it handles \u + 4 hexadecimal digits
    auto ch = NextCharacter();
    switch (ch)
    {
        case '\"': token.string_val.push_back('\"'); return true;
        case '\\': token.string_val.push_back('\\'); return true;
        case '/': token.string_val.push_back('/'); return true;
        case 'b': token.string_val.push_back('\b'); return true;
        case 'f': token.string_val.push_back('\f'); return true;
        case 'r': token.string_val.push_back('\r'); return true;
        case 'n': token.string_val.push_back('\n'); return true;
        case 't': token.string_val.push_back('\t'); return true;
        case 'u':
        {
            int decoded = convert_unicode_to_code_point();
            if (decoded == -1)
            {
                return false;
            }

            // handle multi-block characters that start with a high-surrogate
            if (decoded >= H_SURROGATE_START && decoded <= H_SURROGATE_END)
            {
                // skip escape character '\u'
                if (NextCharacter() != '\\' || NextCharacter() != 'u')
                {
                    return false;
                }
                int decoded2 = convert_unicode_to_code_point();

                if (decoded2 == -1)
                {
                    return false;
                }

                utf16string compoundUTF16 = {static_cast<utf16char>(decoded), static_cast<utf16char>(decoded2)};
                convert_append_unicode_code_unit(token, compoundUTF16);

                return true;
            }

            // Construct the character based on the decoded number
            convert_append_unicode_code_unit(token, static_cast<utf16char>(decoded));

            return true;
        }
        default: return false;
    }
}

template<typename CharType>
bool JSON_Parser<CharType>::CompleteStringLiteral(Token& token)
{
    token.has_unescape_symbol = false;
    auto ch = NextCharacter();
    while (ch != '"')
    {
        if (ch == '\\')
        {
            handle_unescape_char(token);
        }
        else if (ch >= CharType(0x0) && ch < CharType(0x20))
        {
            return false;
        }
        else
        {
            if (ch == eof<CharType>()) return false;

            token.string_val.push_back(static_cast<CharType>(ch));
        }
        ch = NextCharacter();
    }

    if (ch == '"')
    {
        token.kind = Token::TKN_StringLiteral;
    }
    else
    {
        return false;
    }

    return true;
}

template<typename CharType>
bool JSON_StringParser<CharType>::CompleteStringLiteral(typename JSON_Parser<CharType>::Token& token)
{
    // This function is specialized for the string parser, since we can be slightly more
    // efficient in copying data from the input to the token: do a memcpy() rather than
    // one character at a time.

    auto start = m_position;
    token.has_unescape_symbol = false;

    auto ch = JSON_StringParser<CharType>::NextCharacter();

    while (ch != '"')
    {
        if (ch == eof<CharType>()) return false;

        if (ch == '\\')
        {
            const size_t numChars = m_position - start - 1;
            const size_t prevSize = token.string_val.size();
            token.string_val.resize(prevSize + numChars);
            memcpy(const_cast<CharType*>(token.string_val.c_str() + prevSize), start, numChars * sizeof(CharType));

            if (!JSON_StringParser<CharType>::handle_unescape_char(token))
            {
                return false;
            }

            // Reset start position and continue.
            start = m_position;
        }
        else if (ch >= CharType(0x0) && ch < CharType(0x20))
        {
            return false;
        }

        ch = JSON_StringParser<CharType>::NextCharacter();
    }

    const size_t numChars = m_position - start - 1;
    const size_t prevSize = token.string_val.size();
    token.string_val.resize(prevSize + numChars);
    memcpy(const_cast<CharType*>(token.string_val.c_str() + prevSize), start, numChars * sizeof(CharType));

    token.kind = JSON_Parser<CharType>::Token::TKN_StringLiteral;

    return true;
}

template<typename CharType>
void JSON_Parser<CharType>::GetNextToken(typename JSON_Parser<CharType>::Token& result)
{
try_again:
    auto ch = EatWhitespace();

    CreateToken(result, Token::TKN_EOF);

    if (ch == eof<CharType>()) return;

    switch (ch)
    {
        case '{':
        case '[':
        {
            if (++m_currentParsingDepth > JSON_Parser<CharType>::maxParsingDepth)
            {
                SetErrorCode(result, json_error::nesting);
                break;
            }

            typename JSON_Parser<CharType>::Token::Kind tk = ch == '{' ? Token::TKN_OpenBrace : Token::TKN_OpenBracket;
            CreateToken(result, tk, result.start);
            break;
        }
        case '}':
        case ']':
        {
            if ((signed int)(--m_currentParsingDepth) < 0)
            {
                SetErrorCode(result, json_error::mismatched_brances);
                break;
            }

            typename JSON_Parser<CharType>::Token::Kind tk =
                ch == '}' ? Token::TKN_CloseBrace : Token::TKN_CloseBracket;
            CreateToken(result, tk, result.start);
            break;
        }
        case ',': CreateToken(result, Token::TKN_Comma, result.start); break;

        case ':': CreateToken(result, Token::TKN_Colon, result.start); break;

        case 't':
            if (!CompleteKeywordTrue(result))
            {
                SetErrorCode(result, json_error::malformed_literal);
            }
            break;
        case 'f':
            if (!CompleteKeywordFalse(result))
            {
                SetErrorCode(result, json_error::malformed_literal);
            }
            break;
        case 'n':
            if (!CompleteKeywordNull(result))
            {
                SetErrorCode(result, json_error::malformed_literal);
            }
            break;
        case '/':
            if (!CompleteComment(result))
            {
                SetErrorCode(result, json_error::malformed_comment);
                break;
            }
            // For now, we're ignoring comments.
            goto try_again;
        case '"':
            if (!CompleteStringLiteral(result))
            {
                SetErrorCode(result, json_error::malformed_string_literal);
            }
            break;

        case '-':
        case '0':
        case '1':
        case '2':
        case '3':
        case '4':
        case '5':
        case '6':
        case '7':
        case '8':
        case '9':
            if (!CompleteNumberLiteral(static_cast<CharType>(ch), result))
            {
                SetErrorCode(result, json_error::malformed_numeric_literal);
            }
            break;
        default: SetErrorCode(result, json_error::malformed_token); break;
    }
}

template<typename CharType>
std::unique_ptr<web::json::details::_Value> JSON_Parser<CharType>::_ParseObject(
    typename JSON_Parser<CharType>::Token& tkn)
{
    auto obj = utility::details::make_unique<web::json::details::_Object>(g_keep_json_object_unsorted);
    auto& elems = obj->m_object.m_elements;

    GetNextToken(tkn);
    if (tkn.m_error) goto error;

    if (tkn.kind != JSON_Parser<CharType>::Token::TKN_CloseBrace)
    {
        while (true)
        {
            // State 1: New field or end of object, looking for field name or closing brace
            std::basic_string<CharType> fieldName;
            switch (tkn.kind)
            {
                case JSON_Parser<CharType>::Token::TKN_StringLiteral: fieldName = std::move(tkn.string_val); break;
                default: goto error;
            }

            GetNextToken(tkn);
            if (tkn.m_error) goto error;

            // State 2: Looking for a colon.
            if (tkn.kind != JSON_Parser<CharType>::Token::TKN_Colon) goto done;

            GetNextToken(tkn);
            if (tkn.m_error) goto error;

                // State 3: Looking for an expression.
#ifdef ENABLE_JSON_VALUE_VISUALIZER
            auto fieldValue = _ParseValue(tkn);
            auto type = fieldValue->type();
            elems.emplace_back(utility::conversions::to_string_t(std::move(fieldName)),
                               json::value(std::move(fieldValue), type));
#else
            elems.emplace_back(utility::conversions::to_string_t(std::move(fieldName)), json::value(_ParseValue(tkn)));
#endif
            if (tkn.m_error) goto error;

            // State 4: Looking for a comma or a closing brace
            switch (tkn.kind)
            {
                case JSON_Parser<CharType>::Token::TKN_Comma:
                    GetNextToken(tkn);
                    if (tkn.m_error) goto error;
                    break;
                case JSON_Parser<CharType>::Token::TKN_CloseBrace: goto done;
                default: goto error;
            }
        }
    }

done:
    GetNextToken(tkn);
    if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();

    if (!g_keep_json_object_unsorted)
    {
        ::std::sort(elems.begin(), elems.end(), json::object::compare_pairs);
    }

    return std::unique_ptr<web::json::details::_Value>(obj.release());

error:
    if (!tkn.m_error)
    {
        SetErrorCode(tkn, json_error::malformed_object_literal);
    }
    return utility::details::make_unique<web::json::details::_Null>();
}

template<typename CharType>
std::unique_ptr<web::json::details::_Value> JSON_Parser<CharType>::_ParseArray(
    typename JSON_Parser<CharType>::Token& tkn)
{
    GetNextToken(tkn);
    if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();

    auto result = utility::details::make_unique<web::json::details::_Array>();

    if (tkn.kind != JSON_Parser<CharType>::Token::TKN_CloseBracket)
    {
        while (true)
        {
            // State 1: Looking for an expression.
            result->m_array.m_elements.emplace_back(ParseValue(tkn));
            if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();

            // State 4: Looking for a comma or a closing bracket
            switch (tkn.kind)
            {
                case JSON_Parser<CharType>::Token::TKN_Comma:
                    GetNextToken(tkn);
                    if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
                    break;
                case JSON_Parser<CharType>::Token::TKN_CloseBracket:
                    GetNextToken(tkn);
                    if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
                    return std::move(result);
                default:
                    SetErrorCode(tkn, json_error::malformed_array_literal);
                    return utility::details::make_unique<web::json::details::_Null>();
            }
        }
    }

    GetNextToken(tkn);
    if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();

    return std::unique_ptr<web::json::details::_Value>(result.release());
}

template<typename CharType>
std::unique_ptr<web::json::details::_Value> JSON_Parser<CharType>::_ParseValue(
    typename JSON_Parser<CharType>::Token& tkn)
{
    switch (tkn.kind)
    {
        case JSON_Parser<CharType>::Token::TKN_OpenBrace:
        {
            return _ParseObject(tkn);
        }
        case JSON_Parser<CharType>::Token::TKN_OpenBracket:
        {
            return _ParseArray(tkn);
        }
        case JSON_Parser<CharType>::Token::TKN_StringLiteral:
        {
            auto value = utility::details::make_unique<web::json::details::_String>(std::move(tkn.string_val),
                                                                                    tkn.has_unescape_symbol);
            GetNextToken(tkn);
            if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
            return std::move(value);
        }
        case JSON_Parser<CharType>::Token::TKN_IntegerLiteral:
        {
            std::unique_ptr<web::json::details::_Number> value;
            if (tkn.signed_number)
                value = utility::details::make_unique<web::json::details::_Number>(tkn.int64_val);
            else
                value = utility::details::make_unique<web::json::details::_Number>(tkn.uint64_val);

            GetNextToken(tkn);
            if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
            return std::move(value);
        }
        case JSON_Parser<CharType>::Token::TKN_NumberLiteral:
        {
            auto value = utility::details::make_unique<web::json::details::_Number>(tkn.double_val);
            GetNextToken(tkn);
            if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
            return std::move(value);
        }
        case JSON_Parser<CharType>::Token::TKN_BooleanLiteral:
        {
            auto value = utility::details::make_unique<web::json::details::_Boolean>(tkn.boolean_val);
            GetNextToken(tkn);
            if (tkn.m_error) return utility::details::make_unique<web::json::details::_Null>();
            return std::move(value);
        }
        case JSON_Parser<CharType>::Token::TKN_NullLiteral:
        {
            GetNextToken(tkn);
            // Returning a null value whether or not an error occurred.
            return utility::details::make_unique<web::json::details::_Null>();
        }
        default:
        {
            SetErrorCode(tkn, json_error::malformed_token);
            return utility::details::make_unique<web::json::details::_Null>();
        }
    }
}

} // namespace details
} // namespace json
} // namespace web

static web::json::value _parse_stream(utility::istream_t& stream)
{
    web::json::details::JSON_StreamParser<utility::char_t> parser(stream);
    web::json::details::JSON_Parser<utility::char_t>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }

    auto value = parser.ParseValue(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }
    else if (tkn.kind != web::json::details::JSON_Parser<utility::char_t>::Token::TKN_EOF)
    {
        web::json::details::CreateException(tkn,
                                            _XPLATSTR("Left-over characters in stream after parsing a JSON value"));
    }
    return value;
}

static web::json::value _parse_stream(utility::istream_t& stream, std::error_code& error)
{
    web::json::details::JSON_StreamParser<utility::char_t> parser(stream);
    web::json::details::JSON_Parser<utility::char_t>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        error = std::move(tkn.m_error);
        return web::json::value();
    }

    auto returnObject = parser.ParseValue(tkn);
    if (tkn.kind != web::json::details::JSON_Parser<utility::char_t>::Token::TKN_EOF)
    {
        web::json::details::SetErrorCode(tkn, web::json::details::json_error::left_over_character_in_stream);
    }

    error = std::move(tkn.m_error);
    return returnObject;
}

#ifdef _WIN32
static web::json::value _parse_narrow_stream(std::istream& stream)
{
    web::json::details::JSON_StreamParser<char> parser(stream);
    web::json::details::JSON_StreamParser<char>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }

    auto value = parser.ParseValue(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }
    else if (tkn.kind != web::json::details::JSON_Parser<char>::Token::TKN_EOF)
    {
        web::json::details::CreateException(tkn,
                                            _XPLATSTR("Left-over characters in stream after parsing a JSON value"));
    }
    return value;
}

static web::json::value _parse_narrow_stream(std::istream& stream, std::error_code& error)
{
    web::json::details::JSON_StreamParser<char> parser(stream);
    web::json::details::JSON_StreamParser<char>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        error = std::move(tkn.m_error);
        return web::json::value();
    }

    auto returnObject = parser.ParseValue(tkn);
    if (tkn.kind != web::json::details::JSON_Parser<utility::char_t>::Token::TKN_EOF)
    {
        returnObject = web::json::value();
        web::json::details::SetErrorCode(tkn, web::json::details::json_error::left_over_character_in_stream);
    }

    error = std::move(tkn.m_error);
    return returnObject;
}
#endif

web::json::value web::json::value::parse(const utility::string_t& str)
{
    web::json::details::JSON_StringParser<utility::char_t> parser(str);
    web::json::details::JSON_Parser<utility::char_t>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }

    auto value = parser.ParseValue(tkn);
    if (tkn.m_error)
    {
        web::json::details::CreateException(tkn, utility::conversions::to_string_t(tkn.m_error.message()));
    }
    else if (tkn.kind != web::json::details::JSON_Parser<utility::char_t>::Token::TKN_EOF)
    {
        web::json::details::CreateException(tkn,
                                            _XPLATSTR("Left-over characters in stream after parsing a JSON value"));
    }
    return value;
}

web::json::value web::json::value::parse(const utility::string_t& str, std::error_code& error)
{
    web::json::details::JSON_StringParser<utility::char_t> parser(str);
    web::json::details::JSON_Parser<utility::char_t>::Token tkn;

    parser.GetNextToken(tkn);
    if (tkn.m_error)
    {
        error = std::move(tkn.m_error);
        return web::json::value();
    }

    auto returnObject = parser.ParseValue(tkn);
    if (tkn.kind != web::json::details::JSON_Parser<utility::char_t>::Token::TKN_EOF)
    {
        returnObject = web::json::value();
        web::json::details::SetErrorCode(tkn, web::json::details::json_error::left_over_character_in_stream);
    }

    error = std::move(tkn.m_error);
    return returnObject;
}

web::json::value web::json::value::parse(utility::istream_t& stream) { return _parse_stream(stream); }

web::json::value web::json::value::parse(utility::istream_t& stream, std::error_code& error)
{
    return _parse_stream(stream, error);
}

#ifdef _WIN32
web::json::value web::json::value::parse(std::istream& stream) { return _parse_narrow_stream(stream); }

web::json::value web::json::value::parse(std::istream& stream, std::error_code& error)
{
    return _parse_narrow_stream(stream, error);
}
#endif
