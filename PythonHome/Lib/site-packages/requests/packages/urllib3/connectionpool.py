# urllib3/connectionpool.py
# Copyright 2008-2013 Andrey Petrov and contributors (see CONTRIBUTORS.txt)
#
# This module is part of urllib3 and is released under
# the MIT License: http://www.opensource.org/licenses/mit-license.php

import sys
import errno
import logging

from socket import error as SocketError, timeout as SocketTimeout
import socket

try: # Python 3
    from queue import LifoQueue, Empty, Full
except ImportError:
    from Queue import LifoQueue, Empty, Full
    import Queue as _  # Platform-specific: Windows


from .exceptions import (
    ClosedPoolError,
    ConnectionError,
    ConnectTimeoutError,
    EmptyPoolError,
    HostChangedError,
    LocationParseError,
    MaxRetryError,
    SSLError,
    TimeoutError,
    ReadTimeoutError,
    ProxyError,
)
from .packages.ssl_match_hostname import CertificateError
from .packages import six
from .connection import (
    port_by_scheme,
    DummyConnection,
    HTTPConnection, HTTPSConnection, VerifiedHTTPSConnection,
    HTTPException, BaseSSLError,
)
from .request import RequestMethods
from .response import HTTPResponse
from .util import (
    get_host,
    is_connection_dropped,
    Timeout,
)


xrange = six.moves.xrange

log = logging.getLogger(__name__)

_Default = object()

## Pool objects

class ConnectionPool(object):
    """
    Base class for all connection pools, such as
    :class:`.HTTPConnectionPool` and :class:`.HTTPSConnectionPool`.
    """

    scheme = None
    QueueCls = LifoQueue

    def __init__(self, host, port=None):
        if host is None:
            raise LocationParseError(host)

        # httplib doesn't like it when we include brackets in ipv6 addresses
        host = host.strip('[]')

        self.host = host
        self.port = port

    def __str__(self):
        return '%s(host=%r, port=%r)' % (type(self).__name__,
                                         self.host, self.port)

# This is taken from http://hg.python.org/cpython/file/7aaba721ebc0/Lib/socket.py#l252
_blocking_errnos = set([errno.EAGAIN, errno.EWOULDBLOCK])

class HTTPConnectionPool(ConnectionPool, RequestMethods):
    """
    Thread-safe connection pool for one host.

    :param host:
        Host used for this HTTP Connection (e.g. "localhost"), passed into
        :class:`httplib.HTTPConnection`.

    :param port:
        Port used for this HTTP Connection (None is equivalent to 80), passed
        into :class:`httplib.HTTPConnection`.

    :param strict:
        Causes BadStatusLine to be raised if the status line can't be parsed
        as a valid HTTP/1.0 or 1.1 status line, passed into
        :class:`httplib.HTTPConnection`.

        .. note::
           Only works in Python 2. This parameter is ignored in Python 3.

    :param timeout:
        Socket timeout in seconds for each individual connection. This can
        be a float or integer, which sets the timeout for the HTTP request,
        or an instance of :class:`urllib3.util.Timeout` which gives you more
        fine-grained control over request timeouts. After the constructor has
        been parsed, this is always a `urllib3.util.Timeout` object.

    :param maxsize:
        Number of connections to save that can be reused. More than 1 is useful
        in multithreaded situations. If ``block`` is set to false, more
        connections will be created but they will not be saved once they've
        been used.

    :param block:
        If set to True, no more than ``maxsize`` connections will be used at
        a time. When no free connections are available, the call will block
        until a connection has been released. This is a useful side effect for
        particular multithreaded situations where one does not want to use more
        than maxsize connections per host to prevent flooding.

    :param headers:
        Headers to include with all requests, unless other headers are given
        explicitly.

    :param _proxy:
        Parsed proxy URL, should not be used directly, instead, see
        :class:`urllib3.connectionpool.ProxyManager`"

    :param _proxy_headers:
        A dictionary with proxy headers, should not be used directly,
        instead, see :class:`urllib3.connectionpool.ProxyManager`"
    """

    scheme = 'http'
    ConnectionCls = HTTPConnection

    def __init__(self, host, port=None, strict=False,
                 timeout=Timeout.DEFAULT_TIMEOUT, maxsize=1, block=False,
                 headers=None, _proxy=None, _proxy_headers=None, **conn_kw):
        ConnectionPool.__init__(self, host, port)
        RequestMethods.__init__(self, headers)

        self.strict = strict

        # This is for backwards compatibility and can be removed once a timeout
        # can only be set to a Timeout object
        if not isinstance(timeout, Timeout):
            timeout = Timeout.from_float(timeout)

        self.timeout = timeout

        self.pool = self.QueueCls(maxsize)
        self.block = block

        self.proxy = _proxy
        self.proxy_headers = _proxy_headers or {}

        # Fill the queue up so that doing get() on it will block properly
        for _ in xrange(maxsize):
            self.pool.put(None)

        # These are mostly for testing and debugging purposes.
        self.num_connections = 0
        self.num_requests = 0

        if sys.version_info < (2, 7):  # Python 2.6 and older
            conn_kw.pop('source_address', None)
        self.conn_kw = conn_kw

    def _new_conn(self):
        """
        Return a fresh :class:`HTTPConnection`.
        """
        self.num_connections += 1
        log.info("Starting new HTTP connection (%d): %s" %
                 (self.num_connections, self.host))

        conn = self.ConnectionCls(host=self.host, port=self.port,
                                  timeout=self.timeout.connect_timeout,
                                  strict=self.strict, **self.conn_kw)
        if self.proxy is not None:
            # Enable Nagle's algorithm for proxies, to avoid packet
            # fragmentation.
            conn.tcp_nodelay = 0
        return conn

    def _get_conn(self, timeout=None):
        """
        Get a connection. Will return a pooled connection if one is available.

        If no connections are available and :prop:`.block` is ``False``, then a
        fresh connection is returned.

        :param timeout:
            Seconds to wait before giving up and raising
            :class:`urllib3.exceptions.EmptyPoolError` if the pool is empty and
            :prop:`.block` is ``True``.
        """
        conn = None
        try:
            conn = self.pool.get(block=self.block, timeout=timeout)

        except AttributeError: # self.pool is None
            raise ClosedPoolError(self, "Pool is closed.")

        except Empty:
            if self.block:
                raise EmptyPoolError(self,
                                     "Pool reached maximum size and no more "
                                     "connections are allowed.")
            pass  # Oh well, we'll create a new connection then

        # If this is a persistent connection, check if it got disconnected
        if conn and is_connection_dropped(conn):
            log.info("Resetting dropped connection: %s" % self.host)
            conn.close()

        return conn or self._new_conn()

    def _put_conn(self, conn):
        """
        Put a connection back into the pool.

        :param conn:
            Connection object for the current host and port as returned by
            :meth:`._new_conn` or :meth:`._get_conn`.

        If the pool is already full, the connection is closed and discarded
        because we exceeded maxsize. If connections are discarded frequently,
        then maxsize should be increased.

        If the pool is closed, then the connection will be closed and discarded.
        """
        try:
            self.pool.put(conn, block=False)
            return # Everything is dandy, done.
        except AttributeError:
            # self.pool is None.
            pass
        except Full:
            # This should never happen if self.block == True
            log.warning(
                "Connection pool is full, discarding connection: %s" %
                self.host)

        # Connection never got put back into the pool, close it.
        if conn:
            conn.close()

    def _get_timeout(self, timeout):
        """ Helper that always returns a :class:`urllib3.util.Timeout` """
        if timeout is _Default:
            return self.timeout.clone()

        if isinstance(timeout, Timeout):
            return timeout.clone()
        else:
            # User passed us an int/float. This is for backwards compatibility,
            # can be removed later
            return Timeout.from_float(timeout)

    def _make_request(self, conn, method, url, timeout=_Default,
                      **httplib_request_kw):
        """
        Perform a request on a given urllib connection object taken from our
        pool.

        :param conn:
            a connection from one of our connection pools

        :param timeout:
            Socket timeout in seconds for the request. This can be a
            float or integer, which will set the same timeout value for
            the socket connect and the socket read, or an instance of
            :class:`urllib3.util.Timeout`, which gives you more fine-grained
            control over your timeouts.
        """
        self.num_requests += 1

        timeout_obj = self._get_timeout(timeout)

        try:
            timeout_obj.start_connect()
            conn.timeout = timeout_obj.connect_timeout
            # conn.request() calls httplib.*.request, not the method in
            # urllib3.request. It also calls makefile (recv) on the socket.
            conn.request(method, url, **httplib_request_kw)
        except SocketTimeout:
            raise ConnectTimeoutError(
                self, "Connection to %s timed out. (connect timeout=%s)" %
                (self.host, timeout_obj.connect_timeout))

        # Reset the timeout for the recv() on the socket
        read_timeout = timeout_obj.read_timeout

        # App Engine doesn't have a sock attr
        if hasattr(conn, 'sock'):
            # In Python 3 socket.py will catch EAGAIN and return None when you
            # try and read into the file pointer created by http.client, which
            # instead raises a BadStatusLine exception. Instead of catching
            # the exception and assuming all BadStatusLine exceptions are read
            # timeouts, check for a zero timeout before making the request.
            if read_timeout == 0:
                raise ReadTimeoutError(
                    self, url,
                    "Read timed out. (read timeout=%s)" % read_timeout)
            if read_timeout is Timeout.DEFAULT_TIMEOUT:
                conn.sock.settimeout(socket.getdefaulttimeout())
            else: # None or a value
                conn.sock.settimeout(read_timeout)

        # Receive the response from the server
        try:
            try: # Python 2.7+, use buffering of HTTP responses
                httplib_response = conn.getresponse(buffering=True)
            except TypeError: # Python 2.6 and older
                httplib_response = conn.getresponse()
        except SocketTimeout:
            raise ReadTimeoutError(
                self, url, "Read timed out. (read timeout=%s)" % read_timeout)

        except BaseSSLError as e:
            # Catch possible read timeouts thrown as SSL errors. If not the
            # case, rethrow the original. We need to do this because of:
            # http://bugs.python.org/issue10272
            if 'timed out' in str(e) or \
               'did not complete (read)' in str(e):  # Python 2.6
                raise ReadTimeoutError(self, url, "Read timed out.")

            raise

        except SocketError as e: # Platform-specific: Python 2
            # See the above comment about EAGAIN in Python 3. In Python 2 we
            # have to specifically catch it and throw the timeout error
            if e.errno in _blocking_errnos:
                raise ReadTimeoutError(
                    self, url,
                    "Read timed out. (read timeout=%s)" % read_timeout)

            raise

        # AppEngine doesn't have a version attr.
        http_version = getattr(conn, '_http_vsn_str', 'HTTP/?')
        log.debug("\"%s %s %s\" %s %s" % (method, url, http_version,
                                          httplib_response.status,
                                          httplib_response.length))
        return httplib_response

    def close(self):
        """
        Close all pooled connections and disable the pool.
        """
        # Disable access to the pool
        old_pool, self.pool = self.pool, None

        try:
            while True:
                conn = old_pool.get(block=False)
                if conn:
                    conn.close()

        except Empty:
            pass # Done.

    def is_same_host(self, url):
        """
        Check if the given ``url`` is a member of the same host as this
        connection pool.
        """
        if url.startswith('/'):
            return True

        # TODO: Add optional support for socket.gethostbyname checking.
        scheme, host, port = get_host(url)

        # Use explicit default port for comparison when none is given
        if self.port and not port:
            port = port_by_scheme.get(scheme)
        elif not self.port and port == port_by_scheme.get(scheme):
            port = None

        return (scheme, host, port) == (self.scheme, self.host, self.port)

    def urlopen(self, method, url, body=None, headers=None, retries=3,
                redirect=True, assert_same_host=True, timeout=_Default,
                pool_timeout=None, release_conn=None, **response_kw):
        """
        Get a connection from the pool and perform an HTTP request. This is the
        lowest level call for making a request, so you'll need to specify all
        the raw details.

        .. note::

           More commonly, it's appropriate to use a convenience method provided
           by :class:`.RequestMethods`, such as :meth:`request`.

        .. note::

           `release_conn` will only behave as expected if
           `preload_content=False` because we want to make
           `preload_content=False` the default behaviour someday soon without
           breaking backwards compatibility.

        :param method:
            HTTP request method (such as GET, POST, PUT, etc.)

        :param body:
            Data to send in the request body (useful for creating
            POST requests, see HTTPConnectionPool.post_url for
            more convenience).

        :param headers:
            Dictionary of custom headers to send, such as User-Agent,
            If-None-Match, etc. If None, pool headers are used. If provided,
            these headers completely replace any pool-specific headers.

        :param retries:
            Number of retries to allow before raising a MaxRetryError exception.
            If `False`, then retries are disabled and any exception is raised
            immediately.

        :param redirect:
            If True, automatically handle redirects (status codes 301, 302,
            303, 307, 308). Each redirect counts as a retry. Disabling retries
            will disable redirect, too.

        :param assert_same_host:
            If ``True``, will make sure that the host of the pool requests is
            consistent else will raise HostChangedError. When False, you can
            use the pool on an HTTP proxy and request foreign hosts.

        :param timeout:
            If specified, overrides the default timeout for this one
            request. It may be a float (in seconds) or an instance of
            :class:`urllib3.util.Timeout`.

        :param pool_timeout:
            If set and the pool is set to block=True, then this method will
            block for ``pool_timeout`` seconds and raise EmptyPoolError if no
            connection is available within the time period.

        :param release_conn:
            If False, then the urlopen call will not release the connection
            back into the pool once a response is received (but will release if
            you read the entire contents of the response such as when
            `preload_content=True`). This is useful if you're not preloading
            the response's content immediately. You will need to call
            ``r.release_conn()`` on the response ``r`` to return the connection
            back into the pool. If None, it takes the value of
            ``response_kw.get('preload_content', True)``.

        :param \**response_kw:
            Additional parameters are passed to
            :meth:`urllib3.response.HTTPResponse.from_httplib`
        """
        if headers is None:
            headers = self.headers

        if retries < 0 and retries is not False:
            raise MaxRetryError(self, url)

        if release_conn is None:
            release_conn = response_kw.get('preload_content', True)

        # Check host
        if assert_same_host and not self.is_same_host(url):
            raise HostChangedError(self, url, retries - 1)

        conn = None

        # Merge the proxy headers. Only do this in HTTP. We have to copy the
        # headers dict so we can safely change it without those changes being
        # reflected in anyone else's copy.
        if self.scheme == 'http':
            headers = headers.copy()
            headers.update(self.proxy_headers)

        # Must keep the exception bound to a separate variable or else Python 3
        # complains about UnboundLocalError.
        err = None

        try:
            # Request a connection from the queue
            conn = self._get_conn(timeout=pool_timeout)

            # Make the request on the httplib connection object
            httplib_response = self._make_request(conn, method, url,
                                                  timeout=timeout,
                                                  body=body, headers=headers)

            # If we're going to release the connection in ``finally:``, then
            # the request doesn't need to know about the connection. Otherwise
            # it will also try to release it and we'll have a double-release
            # mess.
            response_conn = not release_conn and conn

            # Import httplib's response into our own wrapper object
            response = HTTPResponse.from_httplib(httplib_response,
                                                 pool=self,
                                                 connection=response_conn,
                                                 **response_kw)

            # else:
            #     The connection will be put back into the pool when
            #     ``response.release_conn()`` is called (implicitly by
            #     ``response.read()``)

        except Empty:
            # Timed out by queue.
            raise EmptyPoolError(self, "No pool connections are available.")

        except (BaseSSLError, CertificateError) as e:
            # Release connection unconditionally because there is no way to
            # close it externally in case of exception.
            release_conn = True
            raise SSLError(e)

        except (TimeoutError, HTTPException, SocketError) as e:
            if conn:
                # Discard the connection for these exceptions. It will be
                # be replaced during the next _get_conn() call.
                conn.close()
                conn = None

            if not retries:
                if isinstance(e, TimeoutError):
                    # TimeoutError is exempt from MaxRetryError-wrapping.
                    # FIXME: ... Not sure why. Add a reason here.
                    raise

                # Wrap unexpected exceptions with the most appropriate
                # module-level exception and re-raise.
                if isinstance(e, SocketError) and self.proxy:
                    raise ProxyError('Cannot connect to proxy.', e)

                if retries is False:
                    raise ConnectionError('Connection failed.', e)

                raise MaxRetryError(self, url, e)

            # Keep track of the error for the retry warning.
            err = e

        finally:
            if release_conn:
                # Put the connection back to be reused. If the connection is
                # expired then it will be None, which will get replaced with a
                # fresh connection during _get_conn.
                self._put_conn(conn)

        if not conn:
            # Try again
            log.warning("Retrying (%d attempts remain) after connection "
                        "broken by '%r': %s" % (retries, err, url))
            return self.urlopen(method, url, body, headers, retries - 1,
                                redirect, assert_same_host,
                                timeout=timeout, pool_timeout=pool_timeout,
                                release_conn=release_conn, **response_kw)

        # Handle redirect?
        redirect_location = redirect and response.get_redirect_location()
        if redirect_location and retries is not False:
            if response.status == 303:
                method = 'GET'
            log.info("Redirecting %s -> %s" % (url, redirect_location))
            return self.urlopen(method, redirect_location, body, headers,
                                retries - 1, redirect, assert_same_host,
                                timeout=timeout, pool_timeout=pool_timeout,
                                release_conn=release_conn, **response_kw)

        return response


class HTTPSConnectionPool(HTTPConnectionPool):
    """
    Same as :class:`.HTTPConnectionPool`, but HTTPS.

    When Python is compiled with the :mod:`ssl` module, then
    :class:`.VerifiedHTTPSConnection` is used, which *can* verify certificates,
    instead of :class:`.HTTPSConnection`.

    :class:`.VerifiedHTTPSConnection` uses one of ``assert_fingerprint``,
    ``assert_hostname`` and ``host`` in this order to verify connections.
    If ``assert_hostname`` is False, no verification is done.

    The ``key_file``, ``cert_file``, ``cert_reqs``, ``ca_certs`` and
    ``ssl_version`` are only used if :mod:`ssl` is available and are fed into
    :meth:`urllib3.util.ssl_wrap_socket` to upgrade the connection socket
    into an SSL socket.
    """

    scheme = 'https'
    ConnectionCls = HTTPSConnection

    def __init__(self, host, port=None,
                 strict=False, timeout=None, maxsize=1,
                 block=False, headers=None,
                 _proxy=None, _proxy_headers=None,
                 key_file=None, cert_file=None, cert_reqs=None,
                 ca_certs=None, ssl_version=None,
                 assert_hostname=None, assert_fingerprint=None,
                 **conn_kw):

        if sys.version_info < (2, 7):  # Python 2.6 or older
            conn_kw.pop('source_address', None)

        HTTPConnectionPool.__init__(self, host, port, strict, timeout, maxsize,
                                    block, headers, _proxy, _proxy_headers, **conn_kw)
        self.key_file = key_file
        self.cert_file = cert_file
        self.cert_reqs = cert_reqs
        self.ca_certs = ca_certs
        self.ssl_version = ssl_version
        self.assert_hostname = assert_hostname
        self.assert_fingerprint = assert_fingerprint
        self.conn_kw = conn_kw

    def _prepare_conn(self, conn):
        """
        Prepare the ``connection`` for :meth:`urllib3.util.ssl_wrap_socket`
        and establish the tunnel if proxy is used.
        """

        if isinstance(conn, VerifiedHTTPSConnection):
            conn.set_cert(key_file=self.key_file,
                          cert_file=self.cert_file,
                          cert_reqs=self.cert_reqs,
                          ca_certs=self.ca_certs,
                          assert_hostname=self.assert_hostname,
                          assert_fingerprint=self.assert_fingerprint)
            conn.ssl_version = self.ssl_version
            conn.conn_kw = self.conn_kw

        if self.proxy is not None:
            # Python 2.7+
            try:
                set_tunnel = conn.set_tunnel
            except AttributeError:  # Platform-specific: Python 2.6
                set_tunnel = conn._set_tunnel
            set_tunnel(self.host, self.port, self.proxy_headers)
            # Establish tunnel connection early, because otherwise httplib
            # would improperly set Host: header to proxy's IP:port.
            conn.connect()

        return conn

    def _new_conn(self):
        """
        Return a fresh :class:`httplib.HTTPSConnection`.
        """
        self.num_connections += 1
        log.info("Starting new HTTPS connection (%d): %s"
                 % (self.num_connections, self.host))

        if not self.ConnectionCls or self.ConnectionCls is DummyConnection:
            # Platform-specific: Python without ssl
            raise SSLError("Can't connect to HTTPS URL because the SSL "
                           "module is not available.")

        actual_host = self.host
        actual_port = self.port
        if self.proxy is not None:
            actual_host = self.proxy.host
            actual_port = self.proxy.port

        extra_params = {}
        if not six.PY3:  # Python 2
            extra_params['strict'] = self.strict
        extra_params.update(self.conn_kw)

        conn = self.ConnectionCls(host=actual_host, port=actual_port,
                                  timeout=self.timeout.connect_timeout,
                                  **extra_params)
        if self.proxy is not None:
            # Enable Nagle's algorithm for proxies, to avoid packet
            # fragmentation.
            conn.tcp_nodelay = 0

        return self._prepare_conn(conn)


def connection_from_url(url, **kw):
    """
    Given a url, return an :class:`.ConnectionPool` instance of its host.

    This is a shortcut for not having to parse out the scheme, host, and port
    of the url before creating an :class:`.ConnectionPool` instance.

    :param url:
        Absolute URL string that must include the scheme. Port is optional.

    :param \**kw:
        Passes additional parameters to the constructor of the appropriate
        :class:`.ConnectionPool`. Useful for specifying things like
        timeout, maxsize, headers, etc.

    Example: ::

        >>> conn = connection_from_url('http://google.com/')
        >>> r = conn.request('GET', '/')
    """
    scheme, host, port = get_host(url)
    if scheme == 'https':
        return HTTPSConnectionPool(host, port=port, **kw)
    else:
        return HTTPConnectionPool(host, port=port, **kw)
