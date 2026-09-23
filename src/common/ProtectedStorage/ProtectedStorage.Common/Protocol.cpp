#include "Protocol.h"
#include <algorithm>
#include <limits>

namespace PowerToys::ProtectedStorage
{
    namespace
    {
        class Parser
        {
            std::string_view m_text;
            size_t m_offset = 0;
            size_t m_nodes = 0;
            void Space()
            {
                while (m_offset < m_text.size() && std::string_view(" \t\r\n").find(m_text[m_offset]) != std::string_view::npos)
                    ++m_offset;
            }
            char Take()
            {
                Check(m_offset < m_text.size(), ErrorCode::InvalidPayload);
                return m_text[m_offset++];
            }
            unsigned Hex4()
            {
                unsigned result = 0;
                for (unsigned i = 0; i < 4; ++i)
                {
                    char c = Take();
                    unsigned n = c >= '0' && c <= '9' ? c - '0' :
                                 c >= 'a' && c <= 'f' ? c - 'a' + 10 :
                                 c >= 'A' && c <= 'F' ? c - 'A' + 10 :
                                                        16;
                    Check(n < 16, ErrorCode::InvalidPayload);
                    result = result * 16 + n;
                }
                return result;
            }
            std::string String()
            {
                Check(Take() == '"', ErrorCode::InvalidPayload);
                std::string result;
                while (true)
                {
                    char c = Take();
                    if (c == '"')
                        break;
                    Check(static_cast<unsigned char>(c) >= 32, ErrorCode::InvalidPayload);
                    if (c != '\\')
                    {
                        result += c;
                        continue;
                    }
                    c = Take();
                    switch (c)
                    {
                    case '"':
                    case '\\':
                    case '/':
                        result += c;
                        break;
                    case 'b':
                        result += '\b';
                        break;
                    case 'f':
                        result += '\f';
                        break;
                    case 'n':
                        result += '\n';
                        break;
                    case 'r':
                        result += '\r';
                        break;
                    case 't':
                        result += '\t';
                        break;
                    case 'u':
                    {
                        unsigned first = Hex4();
                        std::wstring wide(1, static_cast<wchar_t>(first));
                        if (first >= 0xD800 && first <= 0xDBFF)
                        {
                            Check(Take() == '\\' && Take() == 'u', ErrorCode::InvalidPayload);
                            unsigned second = Hex4();
                            Check(second >= 0xDC00 && second <= 0xDFFF, ErrorCode::InvalidPayload);
                            wide += static_cast<wchar_t>(second);
                        }
                        else
                            Check(first < 0xDC00 || first > 0xDFFF, ErrorCode::InvalidPayload);
                        result += Utf8(wide);
                        break;
                    }
                    default:
                        throw StorageError(ErrorCode::InvalidPayload);
                    }
                }
                Check(result.find('\0') == result.npos, ErrorCode::InvalidPayload);
                Wide(result);
                return result;
            }
            Value Read(unsigned depth)
            {
                Check(depth < 16 && ++m_nodes <= 8192, ErrorCode::InvalidPayload);
                Space();
                Check(m_offset < m_text.size(), ErrorCode::InvalidPayload);
                char c = m_text[m_offset];
                if (c == '"')
                    return Value(String());
                if (c == '{' || c == '[')
                {
                    ++m_offset;
                    Value::Object object;
                    Value::Array array;
                    const char close = c == '{' ? '}' : ']';
                    Space();
                    if (m_offset < m_text.size() && m_text[m_offset] == close)
                    {
                        ++m_offset;
                        return c == '{' ? Value(std::move(object)) : Value(std::move(array));
                    }
                    while (true)
                    {
                        Space();
                        if (c == '{')
                        {
                            auto key = String();
                            Space();
                            Check(Take() == ':', ErrorCode::InvalidPayload);
                            Check(object.emplace(std::move(key), Read(depth + 1)).second, ErrorCode::InvalidPayload);
                        }
                        else
                            array.push_back(Read(depth + 1));
                        Space();
                        char separator = Take();
                        if (separator == close)
                            break;
                        Check(separator == ',', ErrorCode::InvalidPayload);
                    }
                    return c == '{' ? Value(std::move(object)) : Value(std::move(array));
                }
                for (const auto literal : { "true", "false", "null" })
                {
                    const std::string_view token(literal);
                    if (m_text.substr(m_offset, token.size()) == token)
                    {
                        m_offset += token.size();
                        if (token == "null")
                            return Value();
                        return Value(token == "true");
                    }
                }
                const size_t start = m_offset;
                while (m_offset < m_text.size() && m_text[m_offset] >= '0' && m_text[m_offset] <= '9')
                    ++m_offset;
                return Value(Decimal(m_text.substr(start, m_offset - start)));
            }

        public:
            explicit Parser(std::string_view text) : m_text(text) {}
            Value Parse()
            {
                Check(!m_text.empty() && m_text.size() <= MaxMetadata, ErrorCode::InvalidPayload);
                Wide(m_text);
                Value result = Read(0);
                Space();
                Check(m_offset == m_text.size(), ErrorCode::InvalidPayload);
                return result;
            }
        };
        uint32_t Read32(const BYTE* data)
        {
            return static_cast<uint32_t>(data[0]) | (static_cast<uint32_t>(data[1]) << 8) |
                   (static_cast<uint32_t>(data[2]) << 16) | (static_cast<uint32_t>(data[3]) << 24);
        }
        void Put32(std::vector<BYTE>& data, size_t offset, uint32_t value)
        {
            for (size_t i = 0; i < 4; ++i)
                data[offset + i] = static_cast<BYTE>(value >> (8 * i));
        }
        uint32_t BodySize(const BYTE* header)
        {
            Check(std::equal(header, header + 4, reinterpret_cast<const BYTE*>("PTPS")), ErrorCode::InvalidPayload);
            Check(header[4] == 1 && header[5] == 0 && header[6] == 0 && header[7] == 0, ErrorCode::IncompatibleVersion);
            Check(Read32(header + 12) == HeaderLength, ErrorCode::InvalidPayload);
            uint32_t body = Read32(header + 16);
            Check(body >= 4 && body <= MaxBody, ErrorCode::QuotaExceeded);
            return body;
        }
        void ExactIo(HANDLE pipe, bool write, BYTE* data, DWORD count, HANDLE cancel, DWORD timeout)
        {
            const ULONGLONG deadline = GetTickCount64() + timeout;
            DWORD offset = 0;
            while (offset != count)
            {
                const ULONGLONG now = GetTickCount64();
                Check(now < deadline, ErrorCode::Timeout, ERROR_TIMEOUT);
                DWORD done = BoundedIo(pipe, write, data + offset, count - offset, cancel, static_cast<DWORD>(deadline - now));
                Check(done && done <= count - offset, ErrorCode::OutcomeUnknown, ERROR_BROKEN_PIPE);
                offset += done;
            }
        }
    }
    const Value::Object& Value::Members() const
    {
        Check(std::holds_alternative<Object>(data), ErrorCode::InvalidPayload);
        return std::get<Object>(data);
    }
    const Value::Array& Value::Items() const
    {
        Check(std::holds_alternative<Array>(data), ErrorCode::InvalidPayload);
        return std::get<Array>(data);
    }
    const std::string& Value::Text() const
    {
        Check(std::holds_alternative<std::string>(data), ErrorCode::InvalidPayload);
        return std::get<std::string>(data);
    }
    uint64_t Value::Number() const
    {
        Check(std::holds_alternative<uint64_t>(data), ErrorCode::InvalidPayload);
        return std::get<uint64_t>(data);
    }
    bool Value::Boolean() const
    {
        Check(std::holds_alternative<bool>(data), ErrorCode::InvalidPayload);
        return std::get<bool>(data);
    }
    const Value* Value::Find(std::string_view key) const
    {
        auto it = Members().find(std::string(key));
        return it == Members().end() ? nullptr : &it->second;
    }
    const Value& Value::At(std::string_view key) const
    {
        const Value* value = Find(key);
        Check(value != nullptr, ErrorCode::InvalidPayload);
        return *value;
    }
    Value Value::Parse(std::string_view text)
    {
        return Parser(text).Parse();
    }
    std::string Value::Stringify() const
    {
        if (std::holds_alternative<std::nullptr_t>(data))
            return "null";
        if (std::holds_alternative<bool>(data))
            return Boolean() ? "true" : "false";
        if (std::holds_alternative<uint64_t>(data))
            return std::to_string(Number());
        if (std::holds_alternative<std::string>(data))
            return Json(Text());
        std::string result;
        if (std::holds_alternative<Object>(data))
        {
            result = "{";
            for (const auto& [key, value] : Members())
            {
                if (result.size() > 1)
                    result += ',';
                result += Json(key) + ":" + value.Stringify();
            }
            return result + "}";
        }
        result = "[";
        for (const auto& value : Items())
        {
            if (result.size() > 1)
                result += ',';
            result += value.Stringify();
        }
        return result + "]";
    }
    uint64_t Decimal(std::string_view text)
    {
        Check(!text.empty() && text.size() <= 20 && (text.size() == 1 || text[0] != '0'), ErrorCode::InvalidPayload);
        uint64_t result = 0;
        for (char c : text)
        {
            Check(c >= '0' && c <= '9', ErrorCode::InvalidPayload);
            const auto digit = static_cast<uint64_t>(c) - static_cast<uint64_t>('0');
            Check(result <= (UINT64_MAX - digit) / 10, ErrorCode::InvalidPayload);
            result = result * 10 + digit;
        }
        return result;
    }
    std::array<BYTE, 16> GuidBytes(std::string_view id)
    {
        Check(ValidId(Wide(id)), ErrorCode::InvalidPayload);
        std::array<BYTE, 16> result{};
        size_t index = 0;
        int high = -1;
        for (char c : id)
        {
            if (c == '-')
                continue;
            int n = c >= '0' && c <= '9' ? c - '0' : (c >= 'a' ? c - 'a' : c - 'A') + 10;
            if (high < 0)
                high = n;
            else
            {
                result[index++] = static_cast<BYTE>(high * 16 + n);
                high = -1;
            }
        }
        return result;
    }
    std::string GuidText(const std::array<BYTE, 16>& id)
    {
        constexpr char digits[] = "0123456789abcdef";
        std::string result;
        for (size_t i = 0; i < id.size(); ++i)
        {
            if (i == 4 || i == 6 || i == 8 || i == 10)
                result += '-';
            result += digits[id[i] >> 4];
            result += digits[id[i] & 15];
        }
        return result;
    }
    std::vector<BYTE> EncodeFrame(const Frame& frame)
    {
        auto metadata = frame.metadata.Stringify();
        frame.metadata.Members();
        Check(metadata.size() <= MaxMetadata && frame.bytes.size() <= MaxBlob, ErrorCode::QuotaExceeded);
        std::vector<BYTE> bytes(HeaderLength + 4 + metadata.size() + frame.bytes.size());
        std::copy_n(reinterpret_cast<const BYTE*>("PTPS"), 4, bytes.begin());
        bytes[4] = 1;
        Put32(bytes, 8, static_cast<uint32_t>(frame.command));
        Put32(bytes, 12, HeaderLength);
        Put32(bytes, 16, static_cast<uint32_t>(bytes.size() - HeaderLength));
        std::copy(frame.requestId.begin(), frame.requestId.end(), bytes.begin() + 20);
        Put32(bytes, HeaderLength, static_cast<uint32_t>(metadata.size()));
        std::copy(metadata.begin(), metadata.end(), bytes.begin() + HeaderLength + 4);
        std::copy(frame.bytes.begin(), frame.bytes.end(), bytes.begin() + HeaderLength + 4 + metadata.size());
        return bytes;
    }
    Frame DecodeFrame(const std::vector<BYTE>& bytes)
    {
        Check(bytes.size() >= HeaderLength, ErrorCode::InvalidPayload);
        auto body = BodySize(bytes.data());
        Check(bytes.size() == static_cast<size_t>(HeaderLength) + body, ErrorCode::InvalidPayload);
        auto size = Read32(bytes.data() + HeaderLength);
        Check(size <= MaxMetadata && size <= body - 4 && body - 4 - size <= MaxBlob, ErrorCode::InvalidPayload);
        Frame result;
        result.command = static_cast<DataCommand>(Read32(bytes.data() + 8));
        Check(result.command >= DataCommand::GetCapabilities && result.command <= DataCommand::UpdateTransient, ErrorCode::InvalidPayload);
        std::copy_n(bytes.begin() + 20, 16, result.requestId.begin());
        result.metadata = Value::Parse(std::string_view(reinterpret_cast<const char*>(bytes.data() + HeaderLength + 4), size));
        result.metadata.Members();
        result.bytes.assign(bytes.begin() + HeaderLength + 4 + size, bytes.end());
        return result;
    }
    Frame ReadFrame(HANDLE pipe, HANDLE cancel, DWORD timeout)
    {
        std::vector<BYTE> data(HeaderLength);
        ExactIo(pipe, false, data.data(), HeaderLength, cancel, timeout);
        auto size = BodySize(data.data());
        data.resize(static_cast<size_t>(HeaderLength) + size);
        ExactIo(pipe, false, data.data() + HeaderLength, size, cancel, timeout);
        auto frame = DecodeFrame(data);
        ValidateNetworkFrame(frame);
        return frame;
    }
    void WriteFrame(HANDLE pipe, const Frame& frame, HANDLE cancel, DWORD timeout)
    {
        ValidateNetworkFrame(frame);
        auto data = EncodeFrame(frame);
        ExactIo(pipe, true, data.data(), static_cast<DWORD>(data.size()), cancel, timeout);
    }
    void ValidateNetworkFrame(const Frame& frame)
    {
        Check(std::any_of(frame.requestId.begin(), frame.requestId.end(), [](BYTE value) { return value != 0; }),
              ErrorCode::InvalidPayload);
    }
    void ValidateImportIntent(const Frame& request, bool autoImportSuppressed)
    {
        Check(!autoImportSuppressed || request.command != DataCommand::PutBlob ||
                  !request.metadata.Find("migrationSource"),
              ErrorCode::InvalidPayload);
    }
    std::string_view ErrorName(ErrorCode code)
    {
        constexpr std::string_view names[]{ "Unauthorized", "TargetDenied", "NotProvisioned", "AuthorizationRequired", "AlreadyInitialized", "RevisionConflict", "BusyMaintenance", "InvalidPayload", "Timeout", "OutcomeUnknown", "RecoveryRequired", "IncompatibleVersion", "QuotaExceeded", "NotFound", "OperationConflict", "CleanupPending", "OwnerContextRequired", "InternalError" };
        auto index = static_cast<size_t>(code);
        return index < std::size(names) ? names[index] : "InternalError";
    }
    StorageError::StorageError(ErrorCode value, DWORD nativeCode, std::string operation) :
        Error(std::string(ErrorName(value)), nativeCode), errorCode(value), operationId(std::move(operation)) {}
    void Check(bool condition, ErrorCode error, DWORD nativeCode)
    {
        if (!condition)
            throw StorageError(error, nativeCode);
    }
    Value ErrorMetadata(const std::exception& error, std::string_view operation)
    {
        auto storage = dynamic_cast<const StorageError*>(&error);
        auto native = dynamic_cast<const Error*>(&error);
        ErrorCode code = storage ? storage->errorCode : ErrorCode::InternalError;
        if (!storage && native && native->code == ERROR_TIMEOUT)
            code = ErrorCode::OutcomeUnknown;
        if (!storage && native && native->code == ERROR_ACCESS_DENIED)
            code = ErrorCode::Unauthorized;
        const bool query = code == ErrorCode::Timeout || code == ErrorCode::OutcomeUnknown;
        return Value::Object{ { "errorCode", std::string(ErrorName(code)) }, { "nativeCode", uint64_t(native ? native->code : ERROR_GEN_FAILURE) }, { "retryClass", query ? "QueryOutcome" : code == ErrorCode::BusyMaintenance ? "Retry" :
                                                                                                                                                                                                                                  "None" },
                              { "messageKey", "ProtectedStorage." + std::string(ErrorName(code)) },
                              { "operationId", std::string(operation) } };
    }
    void ThrowIfError(const Value& metadata)
    {
        const auto* error = metadata.Find("errorCode");
        if (!error || error->Text() == "None")
            return;
        for (unsigned i = 0; i <= static_cast<unsigned>(ErrorCode::InternalError); ++i)
        {
            auto code = static_cast<ErrorCode>(i);
            if (error->Text() == ErrorName(code))
                throw StorageError(code, static_cast<DWORD>(metadata.At("nativeCode").Number()), metadata.Find("operationId") ? metadata.At("operationId").Text() : "");
        }
        throw StorageError(ErrorCode::IncompatibleVersion);
    }
}
