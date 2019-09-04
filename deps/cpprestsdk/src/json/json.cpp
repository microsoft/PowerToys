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

using namespace web;

bool json::details::g_keep_json_object_unsorted = false;
void json::keep_object_element_order(bool keep_order) { json::details::g_keep_json_object_unsorted = keep_order; }

utility::ostream_t& web::json::operator<<(utility::ostream_t& os, const web::json::value& val)
{
    val.serialize(os);
    return os;
}

utility::istream_t& web::json::operator>>(utility::istream_t& is, json::value& val)
{
    val = json::value::parse(is);
    return is;
}

web::json::value::value()
    : m_value(utility::details::make_unique<web::json::details::_Null>())
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Null)
#endif
{
}

web::json::value::value(int32_t value)
    : m_value(utility::details::make_unique<web::json::details::_Number>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Number)
#endif
{
}

web::json::value::value(uint32_t value)
    : m_value(utility::details::make_unique<web::json::details::_Number>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Number)
#endif
{
}

web::json::value::value(int64_t value)
    : m_value(utility::details::make_unique<web::json::details::_Number>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Number)
#endif
{
}

web::json::value::value(uint64_t value)
    : m_value(utility::details::make_unique<web::json::details::_Number>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Number)
#endif
{
}

web::json::value::value(double value)
    : m_value(utility::details::make_unique<web::json::details::_Number>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Number)
#endif
{
}

web::json::value::value(bool value)
    : m_value(utility::details::make_unique<web::json::details::_Boolean>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::Boolean)
#endif
{
}

web::json::value::value(utility::string_t value)
    : m_value(utility::details::make_unique<web::json::details::_String>(std::move(value)))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::String)
#endif
{
}

web::json::value::value(utility::string_t value, bool has_escape_chars)
    : m_value(utility::details::make_unique<web::json::details::_String>(std::move(value), has_escape_chars))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::String)
#endif
{
}

web::json::value::value(const utility::char_t* value)
    : m_value(utility::details::make_unique<web::json::details::_String>(value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::String)
#endif
{
}

web::json::value::value(const utility::char_t* value, bool has_escape_chars)
    : m_value(utility::details::make_unique<web::json::details::_String>(utility::string_t(value), has_escape_chars))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(value::String)
#endif
{
}

web::json::value::value(const value& other)
    : m_value(other.m_value->_copy_value())
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    , m_kind(other.m_kind)
#endif
{
}

web::json::value& web::json::value::operator=(const value& other)
{
    if (this != &other)
    {
        m_value = std::unique_ptr<details::_Value>(other.m_value->_copy_value());
#ifdef ENABLE_JSON_VALUE_VISUALIZER
        m_kind = other.m_kind;
#endif
    }
    return *this;
}

web::json::value::value(value&& other) CPPREST_NOEXCEPT : m_value(std::move(other.m_value))
#ifdef ENABLE_JSON_VALUE_VISUALIZER
    ,
                                                          m_kind(other.m_kind)
#endif
{
}

web::json::value& web::json::value::operator=(web::json::value&& other) CPPREST_NOEXCEPT
{
    if (this != &other)
    {
        m_value.swap(other.m_value);
#ifdef ENABLE_JSON_VALUE_VISUALIZER
        m_kind = other.m_kind;
#endif
    }
    return *this;
}

web::json::value web::json::value::null() { return web::json::value(); }

web::json::value web::json::value::number(double value) { return web::json::value(value); }

web::json::value web::json::value::number(int32_t value) { return web::json::value(value); }

web::json::value web::json::value::number(uint32_t value) { return web::json::value(value); }

web::json::value web::json::value::number(int64_t value) { return web::json::value(value); }

web::json::value web::json::value::number(uint64_t value) { return web::json::value(value); }

web::json::value web::json::value::boolean(bool value) { return web::json::value(value); }

web::json::value web::json::value::string(utility::string_t value)
{
    std::unique_ptr<details::_Value> ptr = utility::details::make_unique<details::_String>(std::move(value));
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::String
#endif
    );
}

web::json::value web::json::value::string(utility::string_t value, bool has_escape_chars)
{
    std::unique_ptr<details::_Value> ptr =
        utility::details::make_unique<details::_String>(std::move(value), has_escape_chars);
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::String
#endif
    );
}

#ifdef _WIN32
web::json::value web::json::value::string(const std::string& value)
{
    std::unique_ptr<details::_Value> ptr =
        utility::details::make_unique<details::_String>(utility::conversions::to_utf16string(value));
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::String
#endif
    );
}
#endif

web::json::value web::json::value::object(bool keep_order)
{
    std::unique_ptr<details::_Value> ptr = utility::details::make_unique<details::_Object>(keep_order);
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::Object
#endif
    );
}

web::json::value web::json::value::object(std::vector<std::pair<::utility::string_t, value>> fields, bool keep_order)
{
    std::unique_ptr<details::_Value> ptr =
        utility::details::make_unique<details::_Object>(std::move(fields), keep_order);
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::Object
#endif
    );
}

web::json::value web::json::value::array()
{
    std::unique_ptr<details::_Value> ptr = utility::details::make_unique<details::_Array>();
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::Array
#endif
    );
}

web::json::value web::json::value::array(size_t size)
{
    std::unique_ptr<details::_Value> ptr = utility::details::make_unique<details::_Array>(size);
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::Array
#endif
    );
}

web::json::value web::json::value::array(std::vector<value> elements)
{
    std::unique_ptr<details::_Value> ptr = utility::details::make_unique<details::_Array>(std::move(elements));
    return web::json::value(std::move(ptr)
#ifdef ENABLE_JSON_VALUE_VISUALIZER
                                ,
                            value::Array
#endif
    );
}

const web::json::number& web::json::value::as_number() const { return m_value->as_number(); }

double web::json::value::as_double() const { return m_value->as_double(); }

int web::json::value::as_integer() const { return m_value->as_integer(); }

bool web::json::value::as_bool() const { return m_value->as_bool(); }

json::array& web::json::value::as_array() { return m_value->as_array(); }

const json::array& web::json::value::as_array() const { return m_value->as_array(); }

json::object& web::json::value::as_object() { return m_value->as_object(); }

const json::object& web::json::value::as_object() const { return m_value->as_object(); }

bool web::json::number::is_int32() const
{
    switch (m_type)
    {
        case signed_type:
            return m_intval >= (std::numeric_limits<int32_t>::min)() && m_intval <= (std::numeric_limits<int32_t>::max)();
        case unsigned_type: return m_uintval <= (std::numeric_limits<int32_t>::max)();
        case double_type:
        default: return false;
    }
}

bool web::json::number::is_uint32() const
{
    switch (m_type)
    {
    case signed_type: return m_intval >= 0 && m_intval <= (std::numeric_limits<uint32_t>::max)();
        case unsigned_type: return m_uintval <= (std::numeric_limits<uint32_t>::max)();
        case double_type:
        default: return false;
    }
}

bool web::json::number::is_int64() const
{
    switch (m_type)
    {
        case signed_type: return true;
        case unsigned_type: return m_uintval <= static_cast<uint64_t>((std::numeric_limits<int64_t>::max)());
        case double_type:
        default: return false;
    }
}

bool web::json::details::_String::has_escape_chars(const _String& str)
{
    return std::any_of(std::begin(str.m_string), std::end(str.m_string), [](utility::string_t::value_type const x) {
        if (x <= 31)
        {
            return true;
        }
        if (x == '"')
        {
            return true;
        }
        if (x == '\\')
        {
            return true;
        }
        return false;
    });
}

web::json::value::value_type json::value::type() const { return m_value->type(); }

bool json::value::is_integer() const
{
    if (!is_number())
    {
        return false;
    }
    return m_value->is_integer();
}

bool json::value::is_double() const
{
    if (!is_number())
    {
        return false;
    }
    return m_value->is_double();
}

json::value& web::json::details::_Object::index(const utility::string_t& key) { return m_object[key]; }

bool web::json::details::_Object::has_field(const utility::string_t& key) const
{
    return m_object.find(key) != m_object.end();
}

bool web::json::value::has_number_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_number();
}

bool web::json::value::has_integer_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_integer();
}

bool web::json::value::has_double_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_double();
}

bool web::json::value::has_boolean_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_boolean();
}

bool web::json::value::has_string_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_string();
}

bool web::json::value::has_array_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_array();
}

bool web::json::value::has_object_field(const utility::string_t& key) const
{
    return has_field(key) && at(key).is_object();
}

utility::string_t json::value::to_string() const
{
#ifndef _WIN32
    utility::details::scoped_c_thread_locale locale;
#endif
    return m_value->to_string();
}

bool json::value::operator==(const json::value& other) const
{
    if (this->m_value.get() == other.m_value.get()) return true;
    if (this->type() != other.type()) return false;

    switch (this->type())
    {
        case Null: return true;
        case Number: return this->as_number() == other.as_number();
        case Boolean: return this->as_bool() == other.as_bool();
        case String: return this->as_string() == other.as_string();
        case Object:
            return static_cast<const json::details::_Object*>(this->m_value.get())
                ->is_equal(static_cast<const json::details::_Object*>(other.m_value.get()));
        case Array:
            return static_cast<const json::details::_Array*>(this->m_value.get())
                ->is_equal(static_cast<const json::details::_Array*>(other.m_value.get()));
    }
    __assume(0);
}

void web::json::value::erase(size_t index) { return this->as_array().erase(index); }

void web::json::value::erase(const utility::string_t& key) { return this->as_object().erase(key); }

// at() overloads
web::json::value& web::json::value::at(size_t index) { return this->as_array().at(index); }

const web::json::value& web::json::value::at(size_t index) const { return this->as_array().at(index); }

web::json::value& web::json::value::at(const utility::string_t& key) { return this->as_object().at(key); }

const web::json::value& web::json::value::at(const utility::string_t& key) const { return this->as_object().at(key); }

web::json::value& web::json::value::operator[](const utility::string_t& key)
{
    if (this->is_null())
    {
        m_value.reset(new web::json::details::_Object(details::g_keep_json_object_unsorted));
#ifdef ENABLE_JSON_VALUE_VISUALIZER
        m_kind = value::Object;
#endif
    }
    return m_value->index(key);
}

web::json::value& web::json::value::operator[](size_t index)
{
    if (this->is_null())
    {
        m_value.reset(new web::json::details::_Array());
#ifdef ENABLE_JSON_VALUE_VISUALIZER
        m_kind = value::Array;
#endif
    }
    return m_value->index(index);
}

// Remove once VS 2013 is no longer supported.
#if defined(_WIN32) && _MSC_VER < 1900
static web::json::details::json_error_category_impl instance;
#endif
const web::json::details::json_error_category_impl& web::json::details::json_error_category()
{
#if !defined(_WIN32) || _MSC_VER >= 1900
    static web::json::details::json_error_category_impl instance;
#endif
    return instance;
}
