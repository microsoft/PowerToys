"""Stuff that differs in different Python versions and platform
distributions."""

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
    from urllib.request import url2pathname, urlretrieve, pathname2url
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

    def get_http_message_param(http_message, param, default_value):
        return http_message.get_param(param, default_value)

    bytes = bytes
    string_types = (str,)
    raw_input = input
else:
    from cStringIO import StringIO
    from urllib2 import URLError, HTTPError
    from Queue import Queue, Empty
    from urllib import url2pathname, urlretrieve, pathname2url
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


def get_path_uid(path):
    """
    Return path's uid.

    Does not follow symlinks: https://github.com/pypa/pip/pull/935#discussion_r5307003

    Placed this function in backwardcompat due to differences on AIX and Jython,
    that should eventually go away.

    :raises OSError: When path is a symlink or can't be read.
    """
    if hasattr(os, 'O_NOFOLLOW'):
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
        file_uid = os.fstat(fd).st_uid
        os.close(fd)
    else:  # AIX and Jython
        # WARNING: time of check vulnerabity, but best we can do w/o NOFOLLOW
        if not os.path.islink(path):
            # older versions of Jython don't have `os.fstat`
            file_uid = os.stat(path).st_uid
        else:
            # raise OSError for parity with os.O_NOFOLLOW above
            raise OSError("%s is a symlink; Will not return uid for symlinks" % path)
    return file_uid
