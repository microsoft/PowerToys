import sys, os, socket, atexit, re
import pkg_resources
from pkg_resources import ResolutionError, ExtractionError
from setuptools.compat import urllib2

try:
    import ssl
except ImportError:
    ssl = None

__all__ = [
    'VerifyingHTTPSHandler', 'find_ca_bundle', 'is_available', 'cert_paths',
    'opener_for'
]

cert_paths = """
/etc/pki/tls/certs/ca-bundle.crt
/etc/ssl/certs/ca-certificates.crt
/usr/share/ssl/certs/ca-bundle.crt
/usr/local/share/certs/ca-root.crt
/etc/ssl/cert.pem
/System/Library/OpenSSL/certs/cert.pem
""".strip().split()


HTTPSHandler = HTTPSConnection = object

for what, where in (
    ('HTTPSHandler', ['urllib2','urllib.request']),
    ('HTTPSConnection', ['httplib', 'http.client']),
):
    for module in where:
        try:
            exec("from %s import %s" % (module, what))
        except ImportError:
            pass

is_available = ssl is not None and object not in (HTTPSHandler, HTTPSConnection)





try:
    from socket import create_connection
except ImportError:
    _GLOBAL_DEFAULT_TIMEOUT = getattr(socket, '_GLOBAL_DEFAULT_TIMEOUT', object())
    def create_connection(address, timeout=_GLOBAL_DEFAULT_TIMEOUT,
                          source_address=None):
        """Connect to *address* and return the socket object.

        Convenience function.  Connect to *address* (a 2-tuple ``(host,
        port)``) and return the socket object.  Passing the optional
        *timeout* parameter will set the timeout on the socket instance
        before attempting to connect.  If no *timeout* is supplied, the
        global default timeout setting returned by :func:`getdefaulttimeout`
        is used.  If *source_address* is set it must be a tuple of (host, port)
        for the socket to bind as a source address before making the connection.
        An host of '' or port 0 tells the OS to use the default.
        """
        host, port = address
        err = None
        for res in socket.getaddrinfo(host, port, 0, socket.SOCK_STREAM):
            af, socktype, proto, canonname, sa = res
            sock = None
            try:
                sock = socket.socket(af, socktype, proto)
                if timeout is not _GLOBAL_DEFAULT_TIMEOUT:
                    sock.settimeout(timeout)
                if source_address:
                    sock.bind(source_address)
                sock.connect(sa)
                return sock

            except error:
                err = True
                if sock is not None:
                    sock.close()
        if err:
            raise
        else:
            raise error("getaddrinfo returns an empty list")


try:
    from ssl import CertificateError, match_hostname
except ImportError:
    class CertificateError(ValueError):
        pass

    def _dnsname_to_pat(dn, max_wildcards=1):
        pats = []
        for frag in dn.split(r'.'):
            if frag.count('*') > max_wildcards:
                # Issue #17980: avoid denials of service by refusing more
                # than one wildcard per fragment.  A survery of established
                # policy among SSL implementations showed it to be a
                # reasonable choice.
                raise CertificateError(
                    "too many wildcards in certificate DNS name: " + repr(dn))
            if frag == '*':
                # When '*' is a fragment by itself, it matches a non-empty dotless
                # fragment.
                pats.append('[^.]+')
            else:
                # Otherwise, '*' matches any dotless fragment.
                frag = re.escape(frag)
                pats.append(frag.replace(r'\*', '[^.]*'))
        return re.compile(r'\A' + r'\.'.join(pats) + r'\Z', re.IGNORECASE)

    def match_hostname(cert, hostname):
        """Verify that *cert* (in decoded format as returned by
        SSLSocket.getpeercert()) matches the *hostname*.  RFC 2818 rules
        are mostly followed, but IP addresses are not accepted for *hostname*.

        CertificateError is raised on failure. On success, the function
        returns nothing.
        """
        if not cert:
            raise ValueError("empty or no certificate")
        dnsnames = []
        san = cert.get('subjectAltName', ())
        for key, value in san:
            if key == 'DNS':
                if _dnsname_to_pat(value).match(hostname):
                    return
                dnsnames.append(value)
        if not dnsnames:
            # The subject is only checked when there is no dNSName entry
            # in subjectAltName
            for sub in cert.get('subject', ()):
                for key, value in sub:
                    # XXX according to RFC 2818, the most specific Common Name
                    # must be used.
                    if key == 'commonName':
                        if _dnsname_to_pat(value).match(hostname):
                            return
                        dnsnames.append(value)
        if len(dnsnames) > 1:
            raise CertificateError("hostname %r "
                "doesn't match either of %s"
                % (hostname, ', '.join(map(repr, dnsnames))))
        elif len(dnsnames) == 1:
            raise CertificateError("hostname %r "
                "doesn't match %r"
                % (hostname, dnsnames[0]))
        else:
            raise CertificateError("no appropriate commonName or "
                "subjectAltName fields were found")
























class VerifyingHTTPSHandler(HTTPSHandler):
    """Simple verifying handler: no auth, subclasses, timeouts, etc."""

    def __init__(self, ca_bundle):
        self.ca_bundle = ca_bundle
        HTTPSHandler.__init__(self)

    def https_open(self, req):
        return self.do_open(
            lambda host, **kw: VerifyingHTTPSConn(host, self.ca_bundle, **kw), req
        )


class VerifyingHTTPSConn(HTTPSConnection):
    """Simple verifying connection: no auth, subclasses, timeouts, etc."""
    def __init__(self, host, ca_bundle, **kw):
        HTTPSConnection.__init__(self, host, **kw)
        self.ca_bundle = ca_bundle

    def connect(self):
        sock = create_connection(
            (self.host, self.port), getattr(self,'source_address',None)
        )
        self.sock = ssl.wrap_socket(
            sock, cert_reqs=ssl.CERT_REQUIRED, ca_certs=self.ca_bundle
        )
        try:
            match_hostname(self.sock.getpeercert(), self.host)
        except CertificateError:
            self.sock.shutdown(socket.SHUT_RDWR)
            self.sock.close()
            raise

def opener_for(ca_bundle=None):
    """Get a urlopen() replacement that uses ca_bundle for verification"""
    return urllib2.build_opener(
        VerifyingHTTPSHandler(ca_bundle or find_ca_bundle())
    ).open



_wincerts = None

def get_win_certfile():
    global _wincerts
    if _wincerts is not None:
        return _wincerts.name

    try:
        from wincertstore import CertFile
    except ImportError:
        return None

    class MyCertFile(CertFile):
        def __init__(self, stores=(), certs=()):
            CertFile.__init__(self)
            for store in stores:
                self.addstore(store)
            self.addcerts(certs)
            atexit.register(self.close)

    _wincerts = MyCertFile(stores=['CA', 'ROOT'])
    return _wincerts.name


def find_ca_bundle():
    """Return an existing CA bundle path, or None"""
    if os.name=='nt':
        return get_win_certfile()
    else:
        for cert_path in cert_paths:
            if os.path.isfile(cert_path):
                return cert_path
    try:
        return pkg_resources.resource_filename('certifi', 'cacert.pem')
    except (ImportError, ResolutionError, ExtractionError):
        return None





