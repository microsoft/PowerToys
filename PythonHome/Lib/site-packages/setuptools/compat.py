import sys
import itertools

PY3 = sys.version_info >= (3,)
PY2 = not PY3

if PY2:
    basestring = basestring
    import __builtin__ as builtins
    import ConfigParser
    from StringIO import StringIO
    BytesIO = StringIO
    func_code = lambda o: o.func_code
    func_globals = lambda o: o.func_globals
    im_func = lambda o: o.im_func
    from htmlentitydefs import name2codepoint
    import httplib
    from BaseHTTPServer import HTTPServer
    from SimpleHTTPServer import SimpleHTTPRequestHandler
    from BaseHTTPServer import BaseHTTPRequestHandler
    iteritems = lambda o: o.iteritems()
    long_type = long
    maxsize = sys.maxint
    unichr = unichr
    unicode = unicode
    bytes = str
    from urllib import url2pathname, splittag, pathname2url
    import urllib2
    from urllib2 import urlopen, HTTPError, URLError, unquote, splituser
    from urlparse import urlparse, urlunparse, urljoin, urlsplit, urlunsplit
    filterfalse = itertools.ifilterfalse

    exec("""def reraise(tp, value, tb=None):
    raise tp, value, tb""")

if PY3:
    basestring = str
    import builtins
    import configparser as ConfigParser
    from io import StringIO, BytesIO
    func_code = lambda o: o.__code__
    func_globals = lambda o: o.__globals__
    im_func = lambda o: o.__func__
    from html.entities import name2codepoint
    import http.client as httplib
    from http.server import HTTPServer, SimpleHTTPRequestHandler
    from http.server import BaseHTTPRequestHandler
    iteritems = lambda o: o.items()
    long_type = int
    maxsize = sys.maxsize
    unichr = chr
    unicode = str
    bytes = bytes
    from urllib.error import HTTPError, URLError
    import urllib.request as urllib2
    from urllib.request import urlopen, url2pathname, pathname2url
    from urllib.parse import (
        urlparse, urlunparse, unquote, splituser, urljoin, urlsplit,
        urlunsplit, splittag,
    )
    filterfalse = itertools.filterfalse

    def reraise(tp, value, tb=None):
        if value.__traceback__ is not tb:
            raise value.with_traceback(tb)
        raise value
