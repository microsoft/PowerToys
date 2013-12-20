import sys
import itertools

if sys.version_info[0] < 3:
    PY3 = False

    basestring = basestring
    import __builtin__ as builtins
    import ConfigParser
    from StringIO import StringIO
    BytesIO = StringIO
    execfile = execfile
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
    next = lambda o: o.next()
    numeric_types = (int, long, float)
    reduce = reduce
    unichr = unichr
    unicode = unicode
    from urllib import url2pathname
    import urllib2
    from urllib2 import urlopen, HTTPError, URLError, unquote, splituser
    from urlparse import urlparse, urlunparse, urljoin, urlsplit, urlunsplit
    xrange = xrange
    filterfalse = itertools.ifilterfalse

    def exec_(code, globs=None, locs=None):
        if globs is None:
            frame = sys._getframe(1)
            globs = frame.f_globals
            if locs is None:
                locs = frame.f_locals
            del frame
        elif locs is None:
            locs = globs
        exec("""exec code in globs, locs""")

    exec_("""def reraise(tp, value, tb=None):
    raise tp, value, tb""")
else:
    PY3 = True

    basestring = str
    import builtins
    import configparser as ConfigParser
    exec_ = eval('exec')
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
    next = next
    numeric_types = (int, float)
    from functools import reduce
    unichr = chr
    unicode = str
    from urllib.error import HTTPError, URLError
    import urllib.request as urllib2
    from urllib.request import urlopen, url2pathname
    from urllib.parse import (
        urlparse, urlunparse, unquote, splituser, urljoin, urlsplit,
        urlunsplit,
    )
    xrange = range
    filterfalse = itertools.filterfalse

    def execfile(fn, globs=None, locs=None):
        if globs is None:
            globs = globals()
        if locs is None:
            locs = globs
        f = open(fn)
        try:
            source = f.read()
        finally:
            f.close()
        exec_(compile(source, fn, 'exec'), globs, locs)

    def reraise(tp, value, tb=None):
        if value.__traceback__ is not tb:
            raise value.with_traceback(tb)
        raise value
