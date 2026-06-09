// Json.h
//
// Minimal, dependency-free JSON reader shared by the persistence and config
// loaders. Tolerates JSONC niceties — // line comments, /* block */ comments,
// and trailing commas — so the human-editable config.json can be annotated.
// Header-only: every free helper is marked inline to stay ODR-safe across the
// translation units that include it.

#pragma once

#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <map>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace desktopgrass::json {

// ASCII-only lowercase fold (a-z), locale-independent, so object member keys
// can be matched case-insensitively to mirror the Win2D loader's
// PropertyNameCaseInsensitive=true / OrdinalIgnoreCase behavior. Non-ASCII and
// non-letter bytes pass through unchanged.
inline std::string AsciiLower(std::string_view text) {
    std::string out(text);
    for (char& c : out) {
        if (c >= 'A' && c <= 'Z') {
            c = static_cast<char>(c - 'A' + 'a');
        }
    }
    return out;
}

struct Value {
    enum class Type { Null, Bool, Number, String, Array, Object };

    Type type = Type::Null;
    bool boolValue = false;
    double numberValue = 0.0;
    std::string stringValue;
    std::vector<Value> arrayValue;
    std::map<std::string, Value> objectValue;
};

class Parser {
public:
    explicit Parser(std::string_view text) : text_(text) {}

    bool Parse(Value& out) {
        SkipWhitespace();
        if (!ParseValue(out)) {
            return false;
        }
        SkipWhitespace();
        return pos_ == text_.size();
    }

private:
    void SkipWhitespace() noexcept {
        while (pos_ < text_.size()) {
            const unsigned char c = static_cast<unsigned char>(text_[pos_]);
            if (std::isspace(c)) {
                ++pos_;
                continue;
            }
            // JSONC: skip // line and /* block */ comments.
            if (c == '/' && pos_ + 1 < text_.size()) {
                if (text_[pos_ + 1] == '/') {
                    pos_ += 2;
                    while (pos_ < text_.size() && text_[pos_] != '\n') ++pos_;
                    continue;
                }
                if (text_[pos_ + 1] == '*') {
                    pos_ += 2;
                    while (pos_ + 1 < text_.size() &&
                           !(text_[pos_] == '*' && text_[pos_ + 1] == '/')) {
                        ++pos_;
                    }
                    pos_ = (pos_ + 1 < text_.size()) ? pos_ + 2 : text_.size();
                    continue;
                }
            }
            break;
        }
    }

    bool Match(std::string_view literal) noexcept {
        if (text_.substr(pos_, literal.size()) != literal) {
            return false;
        }
        pos_ += literal.size();
        return true;
    }

    bool ParseValue(Value& out) {
        SkipWhitespace();
        if (pos_ >= text_.size()) return false;

        const char c = text_[pos_];
        if (c == '{') return ParseObject(out);
        if (c == '[') return ParseArray(out);
        if (c == '"') {
            out.type = Value::Type::String;
            return ParseString(out.stringValue);
        }
        if (c == 't') {
            if (!Match("true")) return false;
            out.type = Value::Type::Bool;
            out.boolValue = true;
            return true;
        }
        if (c == 'f') {
            if (!Match("false")) return false;
            out.type = Value::Type::Bool;
            out.boolValue = false;
            return true;
        }
        if (c == 'n') {
            if (!Match("null")) return false;
            out.type = Value::Type::Null;
            return true;
        }
        if (c == '-' || (c >= '0' && c <= '9')) {
            return ParseNumber(out);
        }
        return false;
    }

    bool ParseObject(Value& out) {
        if (text_[pos_] != '{') return false;
        ++pos_;
        out.type = Value::Type::Object;
        out.objectValue.clear();

        SkipWhitespace();
        if (pos_ < text_.size() && text_[pos_] == '}') {
            ++pos_;
            return true;
        }

        while (pos_ < text_.size()) {
            SkipWhitespace();
            std::string key;
            if (!ParseString(key)) return false;
            SkipWhitespace();
            if (pos_ >= text_.size() || text_[pos_] != ':') return false;
            ++pos_;

            Value value;
            if (!ParseValue(value)) return false;
            // Normalize keys to ASCII-lowercase so config/state lookups are
            // case-insensitive (matching the Win2D loader). Monitor keys like
            // "1920x1080@0,0" contain no uppercase letters, so this is a no-op
            // for them.
            out.objectValue.emplace(AsciiLower(key), std::move(value));

            SkipWhitespace();
            if (pos_ >= text_.size()) return false;
            if (text_[pos_] == '}') {
                ++pos_;
                return true;
            }
            if (text_[pos_] != ',') return false;
            ++pos_;
            // JSONC: allow a trailing comma before the closing brace.
            SkipWhitespace();
            if (pos_ < text_.size() && text_[pos_] == '}') {
                ++pos_;
                return true;
            }
        }
        return false;
    }

    bool ParseArray(Value& out) {
        if (text_[pos_] != '[') return false;
        ++pos_;
        out.type = Value::Type::Array;
        out.arrayValue.clear();

        SkipWhitespace();
        if (pos_ < text_.size() && text_[pos_] == ']') {
            ++pos_;
            return true;
        }

        while (pos_ < text_.size()) {
            Value value;
            if (!ParseValue(value)) return false;
            out.arrayValue.push_back(std::move(value));

            SkipWhitespace();
            if (pos_ >= text_.size()) return false;
            if (text_[pos_] == ']') {
                ++pos_;
                return true;
            }
            if (text_[pos_] != ',') return false;
            ++pos_;
            // JSONC: allow a trailing comma before the closing bracket.
            SkipWhitespace();
            if (pos_ < text_.size() && text_[pos_] == ']') {
                ++pos_;
                return true;
            }
        }
        return false;
    }

    bool ParseString(std::string& out) {
        if (pos_ >= text_.size() || text_[pos_] != '"') return false;
        ++pos_;
        out.clear();

        while (pos_ < text_.size()) {
            const char c = text_[pos_++];
            if (c == '"') return true;
            if (c == '\\') {
                if (pos_ >= text_.size()) return false;
                const char esc = text_[pos_++];
                switch (esc) {
                case '"': out.push_back('"'); break;
                case '\\': out.push_back('\\'); break;
                case '/': out.push_back('/'); break;
                case 'b': out.push_back('\b'); break;
                case 'f': out.push_back('\f'); break;
                case 'n': out.push_back('\n'); break;
                case 'r': out.push_back('\r'); break;
                case 't': out.push_back('\t'); break;
                case 'u':
                    if (pos_ + 4 > text_.size()) return false;
                    out.push_back('?');
                    pos_ += 4;
                    break;
                default:
                    return false;
                }
            } else {
                out.push_back(c);
            }
        }
        return false;
    }

    bool ParseNumber(Value& out) {
        const std::size_t start = pos_;
        if (text_[pos_] == '-') ++pos_;
        if (pos_ >= text_.size()) return false;

        if (text_[pos_] == '0') {
            ++pos_;
        } else if (text_[pos_] >= '1' && text_[pos_] <= '9') {
            while (pos_ < text_.size() && text_[pos_] >= '0' && text_[pos_] <= '9') {
                ++pos_;
            }
        } else {
            return false;
        }

        if (pos_ < text_.size() && text_[pos_] == '.') {
            ++pos_;
            if (pos_ >= text_.size() || text_[pos_] < '0' || text_[pos_] > '9') return false;
            while (pos_ < text_.size() && text_[pos_] >= '0' && text_[pos_] <= '9') {
                ++pos_;
            }
        }

        if (pos_ < text_.size() && (text_[pos_] == 'e' || text_[pos_] == 'E')) {
            ++pos_;
            if (pos_ < text_.size() && (text_[pos_] == '+' || text_[pos_] == '-')) ++pos_;
            if (pos_ >= text_.size() || text_[pos_] < '0' || text_[pos_] > '9') return false;
            while (pos_ < text_.size() && text_[pos_] >= '0' && text_[pos_] <= '9') {
                ++pos_;
            }
        }

        const std::string token(text_.substr(start, pos_ - start));
        char* endPtr = nullptr;
        const double value = std::strtod(token.c_str(), &endPtr);
        if (endPtr == token.c_str() || *endPtr != '\0') return false;

        out.type = Value::Type::Number;
        out.numberValue = value;
        return true;
    }

    std::string_view text_;
    std::size_t pos_ = 0;
};

inline bool Parse(std::string_view text, Value& out) {
    Parser parser(text);
    return parser.Parse(out);
}

inline const Value* FindMember(const Value& object, const std::string& name) {
    if (object.type != Value::Type::Object) return nullptr;
    // Keys are stored ASCII-lowercased at parse time, so fold the lookup name
    // the same way for case-insensitive matching.
    const auto it = object.objectValue.find(AsciiLower(name));
    return it == object.objectValue.end() ? nullptr : &it->second;
}

inline std::optional<int> ReadInt(const Value& object, const std::string& name) {
    const Value* value = FindMember(object, name);
    if (!value || value->type != Value::Type::Number) return std::nullopt;
    return static_cast<int>(value->numberValue);
}

inline std::optional<double> ReadDouble(const Value& object, const std::string& name) {
    const Value* value = FindMember(object, name);
    if (!value || value->type != Value::Type::Number) return std::nullopt;
    return value->numberValue;
}

inline std::optional<bool> ReadBool(const Value& object, const std::string& name) {
    const Value* value = FindMember(object, name);
    if (!value || value->type != Value::Type::Bool) return std::nullopt;
    return value->boolValue;
}

inline std::optional<std::string> ReadString(const Value& object, const std::string& name) {
    const Value* value = FindMember(object, name);
    if (!value || value->type != Value::Type::String) return std::nullopt;
    return value->stringValue;
}

inline std::string Escape(std::string_view text) {
    std::string out;
    for (char c : text) {
        switch (c) {
        case '"': out += "\\\""; break;
        case '\\': out += "\\\\"; break;
        case '\b': out += "\\b"; break;
        case '\f': out += "\\f"; break;
        case '\n': out += "\\n"; break;
        case '\r': out += "\\r"; break;
        case '\t': out += "\\t"; break;
        default:
            if (static_cast<unsigned char>(c) < 0x20) {
                char buffer[7]{};
                std::snprintf(buffer, sizeof(buffer), "\\u%04x", static_cast<unsigned char>(c));
                out += buffer;
            } else {
                out.push_back(c);
            }
            break;
        }
    }
    return out;
}

} // namespace desktopgrass::json
