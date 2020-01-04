#pragma once

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Json.h>

#include <optional>

namespace json
{
  using namespace winrt::Windows::Data::Json;

  std::optional<JsonObject> from_file(std::wstring_view file_name);

  void to_file(std::wstring_view file_name, const JsonObject& obj);

  inline bool has(
    const json::JsonObject & o,
    std::wstring_view name,
    const json::JsonValueType type = JsonValueType::Object)
  {
    return o.HasKey(name) && o.GetNamedValue(name).ValueType() == type;
  }

  template<typename T>
  inline std::enable_if_t<std::is_arithmetic_v<T>, JsonValue> value(const T arithmetic)
  {
    return json::JsonValue::CreateNumberValue(arithmetic);
  }

  template<typename T>
  inline std::enable_if_t<!std::is_arithmetic_v<T>, JsonValue> value(T s)
  {
    return json::JsonValue::CreateStringValue(s);
  }

  inline JsonValue value(const bool boolean)
  {
    return json::JsonValue::CreateBooleanValue(boolean);
  }

  inline JsonValue value(JsonObject value)
  {
    return value.as<JsonValue>();
  }

  inline JsonValue value(JsonValue value)
  {
    return value; // identity function overload for convenience
  }
}
