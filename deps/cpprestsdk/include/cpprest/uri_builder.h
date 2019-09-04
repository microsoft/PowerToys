/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * Builder style class for creating URIs.
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#pragma once

#include "cpprest/base_uri.h"
#include <string>

namespace web
{
/// <summary>
/// Builder for constructing URIs incrementally.
/// </summary>
class uri_builder
{
public:
    /// <summary>
    /// Creates a builder with an initially empty URI.
    /// </summary>
    uri_builder() = default;

    /// <summary>
    /// Creates a builder with a existing URI object.
    /// </summary>
    /// <param name="uri_str">Encoded string containing the URI.</param>
    uri_builder(const uri& uri_str) : m_uri(uri_str.m_components) {}

    /// <summary>
    /// Get the scheme component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI scheme as a string.</returns>
    const utility::string_t& scheme() const { return m_uri.m_scheme; }

    /// <summary>
    /// Get the user information component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI user information as a string.</returns>
    const utility::string_t& user_info() const { return m_uri.m_user_info; }

    /// <summary>
    /// Get the host component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI host as a string.</returns>
    const utility::string_t& host() const { return m_uri.m_host; }

    /// <summary>
    /// Get the port component of the URI. Returns -1 if no port is specified.
    /// </summary>
    /// <returns>The URI port as an integer.</returns>
    int port() const { return m_uri.m_port; }

    /// <summary>
    /// Get the path component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI path as a string.</returns>
    const utility::string_t& path() const { return m_uri.m_path; }

    /// <summary>
    /// Get the query component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI query as a string.</returns>
    const utility::string_t& query() const { return m_uri.m_query; }

    /// <summary>
    /// Get the fragment component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI fragment as a string.</returns>
    const utility::string_t& fragment() const { return m_uri.m_fragment; }

    /// <summary>
    /// Set the scheme of the URI.
    /// </summary>
    /// <param name="scheme">Uri scheme.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_scheme(const utility::string_t& scheme)
    {
        m_uri.m_scheme = scheme;
        return *this;
    }

    /// <summary>
    /// Set the user info component of the URI.
    /// </summary>
    /// <param name="user_info">User info as a decoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_user_info(const utility::string_t& user_info, bool do_encoding = false)
    {
        if (do_encoding)
        {
            m_uri.m_user_info = uri::encode_uri(user_info, uri::components::user_info);
        }
        else
        {
            m_uri.m_user_info = user_info;
        }

        return *this;
    }

    /// <summary>
    /// Set the host component of the URI.
    /// </summary>
    /// <param name="host">Host as a decoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_host(const utility::string_t& host, bool do_encoding = false)
    {
        if (do_encoding)
        {
            m_uri.m_host = uri::encode_uri(host, uri::components::host);
        }
        else
        {
            m_uri.m_host = host;
        }

        return *this;
    }

    /// <summary>
    /// Set the port component of the URI.
    /// </summary>
    /// <param name="port">Port as an integer.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_port(int port)
    {
        m_uri.m_port = port;
        return *this;
    }

    /// <summary>
    /// Set the port component of the URI.
    /// </summary>
    /// <param name="port">Port as a string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    /// <remarks>When string can't be converted to an integer the port is left unchanged.</remarks>
    _ASYNCRTIMP uri_builder& set_port(const utility::string_t& port);

    /// <summary>
    /// Set the path component of the URI.
    /// </summary>
    /// <param name="path">Path as a decoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_path(const utility::string_t& path, bool do_encoding = false)
    {
        if (do_encoding)
        {
            m_uri.m_path = uri::encode_uri(path, uri::components::path);
        }
        else
        {
            m_uri.m_path = path;
        }

        return *this;
    }

    /// <summary>
    /// Set the query component of the URI.
    /// </summary>
    /// <param name="query">Query as a decoded string.</param>
    /// <param name="do_encoding">Specify whether apply URI encoding to the given string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_query(const utility::string_t& query, bool do_encoding = false)
    {
        if (do_encoding)
        {
            m_uri.m_query = uri::encode_uri(query, uri::components::query);
        }
        else
        {
            m_uri.m_query = query;
        }

        return *this;
    }

    /// <summary>
    /// Set the fragment component of the URI.
    /// </summary>
    /// <param name="fragment">Fragment as a decoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this <c>uri_builder</c> to support chaining.</returns>
    uri_builder& set_fragment(const utility::string_t& fragment, bool do_encoding = false)
    {
        if (do_encoding)
        {
            m_uri.m_fragment = uri::encode_uri(fragment, uri::components::fragment);
        }
        else
        {
            m_uri.m_fragment = fragment;
        }

        return *this;
    }

    /// <summary>
    /// Clears all components of the underlying URI in this uri_builder.
    /// </summary>
    void clear() { m_uri = details::uri_components(); }

    /// <summary>
    /// Appends another path to the path of this uri_builder.
    /// </summary>
    /// <param name="path">Path to append as a already encoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this uri_builder to support chaining.</returns>
    _ASYNCRTIMP uri_builder& append_path(const utility::string_t& path, bool do_encoding = false);

    /// <summary>
    /// Appends the raw contents of the path argument to the path of this uri_builder with no separator de-duplication.
    /// </summary>
    /// <remarks>
    /// The path argument is appended after adding a '/' separator without regards to the contents of path. If an empty
    /// string is provided, this function will immediately return without changes to the stored path value. For example:
    /// if the current contents are "/abc" and path="/xyz", the result will be "/abc//xyz".
    /// </remarks>
    /// <param name="path">Path to append as a already encoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this uri_builder to support chaining.</returns>
    _ASYNCRTIMP uri_builder& append_path_raw(const utility::string_t& path, bool do_encoding = false);

    /// <summary>
    /// Appends another query to the query of this uri_builder.
    /// </summary>
    /// <param name="query">Query to append as a decoded string.</param>
    /// <param name="do_encoding">Specify whether to apply URI encoding to the given string.</param>
    /// <returns>A reference to this uri_builder to support chaining.</returns>
    _ASYNCRTIMP uri_builder& append_query(const utility::string_t& query, bool do_encoding = false);

    /// <summary>
    /// Appends an relative uri (Path, Query and fragment) at the end of the current uri.
    /// </summary>
    /// <param name="relative_uri">The relative uri to append.</param>
    /// <returns>A reference to this uri_builder to support chaining.</returns>
    _ASYNCRTIMP uri_builder& append(const uri& relative_uri);

    /// <summary>
    /// Appends another query to the query of this uri_builder, encoding it first. This overload is useful when building
    /// a query segment of the form "element=10", where the right hand side of the query is stored as a type other than
    /// a string, for instance, an integral type.
    /// </summary>
    /// <param name="name">The name portion of the query string</param>
    /// <param name="value">The value portion of the query string</param>
    /// <returns>A reference to this uri_builder to support chaining.</returns>
    template<typename T>
    uri_builder& append_query(const utility::string_t& name, const T& value, bool do_encoding = true)
    {
        if (do_encoding)
            append_query_encode_impl(name, utility::conversions::details::print_utf8string(value));
        else
            append_query_no_encode_impl(name, utility::conversions::details::print_string(value));
        return *this;
    }

    /// <summary>
    /// Combine and validate the URI components into a encoded string. An exception will be thrown if the URI is
    /// invalid.
    /// </summary>
    /// <returns>The created URI as a string.</returns>
    _ASYNCRTIMP utility::string_t to_string() const;

    /// <summary>
    /// Combine and validate the URI components into a URI class instance. An exception will be thrown if the URI is
    /// invalid.
    /// </summary>
    /// <returns>The create URI as a URI class instance.</returns>
    _ASYNCRTIMP uri to_uri() const;

    /// <summary>
    /// Validate the generated URI from all existing components of this uri_builder.
    /// </summary>
    /// <returns>Whether the URI is valid.</returns>
    _ASYNCRTIMP bool is_valid();

private:
    _ASYNCRTIMP void append_query_encode_impl(const utility::string_t& name, const utf8string& value);
    _ASYNCRTIMP void append_query_no_encode_impl(const utility::string_t& name, const utility::string_t& value);

    details::uri_components m_uri;
};
} // namespace web
