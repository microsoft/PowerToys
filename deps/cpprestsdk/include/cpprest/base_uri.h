/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * Protocol independent support for URIs.
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#pragma once

#include "cpprest/asyncrt_utils.h"
#include "cpprest/details/basic_types.h"
#include <map>
#include <string>
#include <utility>
#include <vector>

namespace web
{
namespace details
{
struct uri_components
{
    uri_components() : m_path(_XPLATSTR("/")), m_port(-1) {}

    uri_components(const uri_components&) = default;
    uri_components& operator=(const uri_components&) = default;

    // This is for VS2013 compatibility -- replace with '= default' when VS2013 is completely dropped.
    uri_components(uri_components&& other) CPPREST_NOEXCEPT : m_scheme(std::move(other.m_scheme)),
                                                              m_host(std::move(other.m_host)),
                                                              m_user_info(std::move(other.m_user_info)),
                                                              m_path(std::move(other.m_path)),
                                                              m_query(std::move(other.m_query)),
                                                              m_fragment(std::move(other.m_fragment)),
                                                              m_port(other.m_port)
    {
    }

    // This is for VS2013 compatibility -- replace with '= default' when VS2013 is completely dropped.
    uri_components& operator=(uri_components&& other) CPPREST_NOEXCEPT
    {
        if (this != &other)
        {
            m_scheme = std::move(other.m_scheme);
            m_host = std::move(other.m_host);
            m_user_info = std::move(other.m_user_info);
            m_path = std::move(other.m_path);
            m_query = std::move(other.m_query);
            m_fragment = std::move(other.m_fragment);
            m_port = other.m_port;
        }
        return *this;
    }

    _ASYNCRTIMP utility::string_t join();

    utility::string_t m_scheme;
    utility::string_t m_host;
    utility::string_t m_user_info;
    utility::string_t m_path;
    utility::string_t m_query;
    utility::string_t m_fragment;
    int m_port;
};
} // namespace details

/// <summary>
/// A single exception type to represent errors in parsing, encoding, and decoding URIs.
/// </summary>
class uri_exception : public std::exception
{
public:
    uri_exception(std::string msg) : m_msg(std::move(msg)) {}

    ~uri_exception() CPPREST_NOEXCEPT {}

    const char* what() const CPPREST_NOEXCEPT { return m_msg.c_str(); }

private:
    std::string m_msg;
};

/// <summary>
/// A flexible, protocol independent URI implementation.
///
/// URI instances are immutable. Querying the various fields on an empty URI will return empty strings. Querying
/// various diagnostic members on an empty URI will return false.
/// </summary>
/// <remarks>
/// This implementation accepts both URIs ('http://msn.com/path') and URI relative-references
/// ('/path?query#frag').
///
/// This implementation does not provide any scheme-specific handling -- an example of this
/// would be the following: 'http://path1/path'. This is a valid URI, but it's not a valid
/// http-uri -- that is, it's syntactically correct but does not conform to the requirements
/// of the http scheme (http requires a host).
/// We could provide this by allowing a pluggable 'scheme' policy-class, which would provide
/// extra capability for validating and canonicalizing a URI according to scheme, and would
/// introduce a layer of type-safety for URIs of differing schemes, and thus differing semantics.
///
/// One issue with implementing a scheme-independent URI facility is that of comparing for equality.
/// For instance, these URIs are considered equal 'http://msn.com', 'http://msn.com:80'. That is --
/// the 'default' port can be either omitted or explicit. Since we don't have a way to map a scheme
/// to it's default port, we don't have a way to know these are equal. This is just one of a class of
/// issues with regard to scheme-specific behavior.
/// </remarks>
class uri
{
public:
    /// <summary>
    /// The various components of a URI. This enum is used to indicate which
    /// URI component is being encoded to the encode_uri_component. This allows
    /// specific encoding to be performed.
    ///
    /// Scheme and port don't allow '%' so they don't need to be encoded.
    /// </summary>
    class components
    {
    public:
        enum component
        {
            user_info,
            host,
            path,
            query,
            fragment,
            full_uri
        };
    };

    /// <summary>
    /// Encodes a URI component according to RFC 3986.
    /// Note if a full URI is specified instead of an individual URI component all
    /// characters not in the unreserved set are escaped.
    /// </summary>
    /// <param name="raw">The URI as a string.</param>
    /// <returns>The encoded string.</returns>
    _ASYNCRTIMP static utility::string_t __cdecl encode_uri(const utility::string_t& raw,
                                                            uri::components::component = components::full_uri);

    /// <summary>
    /// Encodes a string by converting all characters except for RFC 3986 unreserved characters to their
    /// hexadecimal representation.
    /// </summary>
    /// <returns>The encoded string.</returns>
    _ASYNCRTIMP static utility::string_t __cdecl encode_data_string(const utility::string_t& data);

    /// <summary>
    /// Decodes an encoded string.
    /// </summary>
    /// <param name="encoded">The URI as a string.</param>
    /// <returns>The decoded string.</returns>
    _ASYNCRTIMP static utility::string_t __cdecl decode(const utility::string_t& encoded);

    /// <summary>
    /// Splits a path into its hierarchical components.
    /// </summary>
    /// <param name="path">The path as a string</param>
    /// <returns>A <c>std::vector&lt;utility::string_t&gt;</c> containing the segments in the path.</returns>
    _ASYNCRTIMP static std::vector<utility::string_t> __cdecl split_path(const utility::string_t& path);

    /// <summary>
    /// Splits a query into its key-value components.
    /// </summary>
    /// <param name="query">The query string</param>
    /// <returns>A <c>std::map&lt;utility::string_t, utility::string_t&gt;</c> containing the key-value components of
    /// the query.</returns>
    _ASYNCRTIMP static std::map<utility::string_t, utility::string_t> __cdecl split_query(
        const utility::string_t& query);

    /// <summary>
    /// Validates a string as a URI.
    /// </summary>
    /// <remarks>
    /// This function accepts both uris ('http://msn.com') and uri relative-references ('path1/path2?query').
    /// </remarks>
    /// <param name="uri_string">The URI string to be validated.</param>
    /// <returns><c>true</c> if the given string represents a valid URI, <c>false</c> otherwise.</returns>
    _ASYNCRTIMP static bool __cdecl validate(const utility::string_t& uri_string);

    /// <summary>
    /// Creates an empty uri
    /// </summary>
    uri() : m_uri(_XPLATSTR("/")) {}

    /// <summary>
    /// Creates a URI from the given encoded string. This will throw an exception if the string
    /// does not contain a valid URI. Use uri::validate if processing user-input.
    /// </summary>
    /// <param name="uri_string">A pointer to an encoded string to create the URI instance.</param>
    _ASYNCRTIMP uri(const utility::char_t* uri_string);

    /// <summary>
    /// Creates a URI from the given encoded string. This will throw an exception if the string
    /// does not contain a valid URI. Use uri::validate if processing user-input.
    /// </summary>
    /// <param name="uri_string">An encoded URI string to create the URI instance.</param>
    _ASYNCRTIMP uri(const utility::string_t& uri_string);

    /// <summary>
    /// Copy constructor.
    /// </summary>
    uri(const uri&) = default;

    /// <summary>
    /// Copy assignment operator.
    /// </summary>
    uri& operator=(const uri&) = default;

    /// <summary>
    /// Move constructor.
    /// </summary>
    // This is for VS2013 compatibility -- replace with '= default' when VS2013 is completely dropped.
    uri(uri&& other) CPPREST_NOEXCEPT : m_uri(std::move(other.m_uri)), m_components(std::move(other.m_components)) {}

    /// <summary>
    /// Move assignment operator
    /// </summary>
    // This is for VS2013 compatibility -- replace with '= default' when VS2013 is completely dropped.
    uri& operator=(uri&& other) CPPREST_NOEXCEPT
    {
        if (this != &other)
        {
            m_uri = std::move(other.m_uri);
            m_components = std::move(other.m_components);
        }
        return *this;
    }

    /// <summary>
    /// Get the scheme component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI scheme as a string.</returns>
    const utility::string_t& scheme() const { return m_components.m_scheme; }

    /// <summary>
    /// Get the user information component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI user information as a string.</returns>
    const utility::string_t& user_info() const { return m_components.m_user_info; }

    /// <summary>
    /// Get the host component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI host as a string.</returns>
    const utility::string_t& host() const { return m_components.m_host; }

    /// <summary>
    /// Get the port component of the URI. Returns -1 if no port is specified.
    /// </summary>
    /// <returns>The URI port as an integer.</returns>
    int port() const { return m_components.m_port; }

    /// <summary>
    /// Get the path component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI path as a string.</returns>
    const utility::string_t& path() const { return m_components.m_path; }

    /// <summary>
    /// Get the query component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI query as a string.</returns>
    const utility::string_t& query() const { return m_components.m_query; }

    /// <summary>
    /// Get the fragment component of the URI as an encoded string.
    /// </summary>
    /// <returns>The URI fragment as a string.</returns>
    const utility::string_t& fragment() const { return m_components.m_fragment; }

    /// <summary>
    /// Creates a new uri object with the same authority portion as this one, omitting the resource and query portions.
    /// </summary>
    /// <returns>The new uri object with the same authority.</returns>
    _ASYNCRTIMP uri authority() const;

    /// <summary>
    /// Gets the path, query, and fragment portion of this uri, which may be empty.
    /// </summary>
    /// <returns>The new URI object with the path, query and fragment portion of this URI.</returns>
    _ASYNCRTIMP uri resource() const;

    /// <summary>
    /// An empty URI specifies no components, and serves as a default value
    /// </summary>
    bool is_empty() const { return this->m_uri.empty() || this->m_uri == _XPLATSTR("/"); }

    /// <summary>
    /// A loopback URI is one which refers to a hostname or ip address with meaning only on the local machine.
    /// </summary>
    /// <remarks>
    /// Examples include "localhost", or ip addresses in the loopback range (127.0.0.0/24).
    /// </remarks>
    /// <returns><c>true</c> if this URI references the local host, <c>false</c> otherwise.</returns>
    bool is_host_loopback() const
    {
        return !is_empty() &&
               ((host() == _XPLATSTR("localhost")) || (host().size() > 4 && host().substr(0, 4) == _XPLATSTR("127.")));
    }

    /// <summary>
    /// A wildcard URI is one which refers to all hostnames that resolve to the local machine (using the * or +)
    /// </summary>
    /// <example>
    /// http://*:80
    /// </example>
    bool is_host_wildcard() const
    {
        return !is_empty() && (this->host() == _XPLATSTR("*") || this->host() == _XPLATSTR("+"));
    }

    /// <summary>
    /// A portable URI is one with a hostname that can be resolved globally (used from another machine).
    /// </summary>
    /// <returns><c>true</c> if this URI can be resolved globally (used from another machine), <c>false</c>
    /// otherwise.</returns> <remarks> The hostname "localhost" is a reserved name that is guaranteed to resolve to the
    /// local machine, and cannot be used for inter-machine communication. Likewise the hostnames "*" and "+" on Windows
    /// represent wildcards, and do not map to a resolvable address.
    /// </remarks>
    bool is_host_portable() const { return !(is_empty() || is_host_loopback() || is_host_wildcard()); }

    /// <summary>
    /// A default port is one where the port is unspecified, and will be determined by the operating system.
    /// The choice of default port may be dictated by the scheme (http -> 80) or not.
    /// </summary>
    /// <returns><c>true</c> if this URI instance has a default port, <c>false</c> otherwise.</returns>
    bool is_port_default() const { return !is_empty() && this->port() == 0; }

    /// <summary>
    /// An "authority" URI is one with only a scheme, optional userinfo, hostname, and (optional) port.
    /// </summary>
    /// <returns><c>true</c> if this is an "authority" URI, <c>false</c> otherwise.</returns>
    bool is_authority() const { return !is_empty() && is_path_empty() && query().empty() && fragment().empty(); }

    /// <summary>
    /// Returns whether the other URI has the same authority as this one
    /// </summary>
    /// <param name="other">The URI to compare the authority with.</param>
    /// <returns><c>true</c> if both the URI's have the same authority, <c>false</c> otherwise.</returns>
    bool has_same_authority(const uri& other) const { return !is_empty() && this->authority() == other.authority(); }

    /// <summary>
    /// Returns whether the path portion of this URI is empty
    /// </summary>
    /// <returns><c>true</c> if the path portion of this URI is empty, <c>false</c> otherwise.</returns>
    bool is_path_empty() const { return path().empty() || path() == _XPLATSTR("/"); }

    /// <summary>
    /// Returns the full (encoded) URI as a string.
    /// </summary>
    /// <returns>The full encoded URI string.</returns>
    utility::string_t to_string() const { return m_uri; }

    /// <summary>
    /// Returns an URI resolved against <c>this</c> as the base URI
    /// according to RFC3986, Section 5 (https://tools.ietf.org/html/rfc3986#section-5).
    /// </summary>
    /// <param name="relativeUri">The relative URI to be resolved against <c>this</c> as base.</param>
    /// <returns>The new resolved URI string.</returns>
    _ASYNCRTIMP utility::string_t resolve_uri(const utility::string_t& relativeUri) const;

    _ASYNCRTIMP bool operator==(const uri& other) const;

    bool operator<(const uri& other) const { return m_uri < other.m_uri; }

    bool operator!=(const uri& other) const { return !(this->operator==(other)); }

private:
    friend class uri_builder;

    /// <summary>
    /// Creates a URI from the given URI components.
    /// </summary>
    /// <param name="components">A URI components object to create the URI instance.</param>
    _ASYNCRTIMP uri(const details::uri_components& components);

    // Used by uri_builder
    static utility::string_t __cdecl encode_query_impl(const utf8string& raw);

    utility::string_t m_uri;
    details::uri_components m_components;
};

} // namespace web
