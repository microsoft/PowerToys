"""Stuff that differs in different Python versions"""

import os
import imp
import sys
import site

__all__ = ['WindowsError']

uses_pycache = hasattr(imp, 'cache_from_source')

class NeverUsedException(Exception):
    """this exception should never be raised"""

try:
    WindowsError = WindowsError
except NameError:
    WindowsError = NeverUsedException

try:
    #new in Python 3.3
    PermissionError = PermissionError
except NameError:
    PermissionError = NeverUsedException

console_encoding = sys.__stdout__.encoding

if sys.version_info >= (3,):
    from io import StringIO, BytesIO
    from functools import reduce
    from urllib.error import URLError, HTTPError
    from queue import Queue, Empty
    from urllib.request import url2pathname
    from urllib.request import urlretrieve
    from email import message as emailmessage
    import urllib.parse as urllib
    import urllib.request as urllib2
    import configparser as ConfigParser
    import xmlrpc.client as xmlrpclib
    import urllib.parse as urlparse
    import http.client as httplib

    def cmp(a, b):
        return (a > b) - (a < b)

    def b(s):
        return s.encode('utf-8')

    def u(s):
        return s.decode('utf-8')

    def console_to_str(s):
        try:
            return s.decode(console_encoding)
        except UnicodeDecodeError:
            return s.decode('utf_8')

    def fwrite(f, s):
        f.buffer.write(b(s))

    def get_http_message_param(http_message, param, default_value):
        return http_message.get_param(param, default_value)

    bytes = bytes
    string_types = (str,)
    raw_input = input
else:
    from cStringIO import StringIO
    from urllib2 import URLError, HTTPError
    from Queue import Queue, Empty
    from urllib import url2pathname, urlretrieve
    from email import Message as emailmessage
    import urllib
    import urllib2
    import urlparse
    import ConfigParser
    import xmlrpclib
    import httplib

    def b(s):
        return s

    def u(s):
        return s

    def console_to_str(s):
        return s

    def fwrite(f, s):
        f.write(s)

    def get_http_message_param(http_message, param, default_value):
        result = http_message.getparam(param)
        return result or default_value

    bytes = str
    string_types = (basestring,)
    reduce = reduce
    cmp = cmp
    raw_input = raw_input
    BytesIO = StringIO


from distutils.sysconfig import get_python_lib, get_python_version

#site.USER_SITE was created in py2.6
user_site = getattr(site, 'USER_SITE', None)


def product(*args, **kwds):
    # product('ABCD', 'xy') --> Ax Ay Bx By Cx Cy Dx Dy
    # product(range(2), repeat=3) --> 000 001 010 011 100 101 110 111
    pools = list(map(tuple, args)) * kwds.get('repeat', 1)
    result = [[]]
    for pool in pools:
        result = [x + [y] for x in result for y in pool]
    for prod in result:
        yield tuple(prod)


## only >=py32 has ssl.match_hostname and ssl.CertificateError
try:
    from ssl import match_hostname, CertificateError
except ImportError:
    from ssl_match_hostname import match_hostname, CertificateError
