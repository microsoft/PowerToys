import cgi
import getpass
import hashlib
import mimetypes
import os
import platform
import re
import shutil
import socket
import ssl
import sys
import tempfile

import pip

from pip.backwardcompat import (urllib, urllib2, httplib,
                                urlparse, string_types, get_http_message_param,
                                match_hostname, CertificateError)
from pip.exceptions import InstallationError, HashMismatch
from pip.util import (splitext, rmtree, format_size, display_path,
                      backup_dir, ask_path_exists, unpack_file,
                      create_download_cache_folder, cache_download)
from pip.vcs import vcs
from pip.log import logger
from pip.locations import default_cert_path

__all__ = ['get_file_content', 'urlopen',
           'is_url', 'url_to_path', 'path_to_url', 'path_to_url2',
           'geturl', 'is_archive_file', 'unpack_vcs_link',
           'unpack_file_url', 'is_vcs_url', 'is_file_url', 'unpack_http_url']


def build_user_agent():
    """Return a string representing the user agent."""
    _implementation = platform.python_implementation()

    if _implementation == 'CPython':
        _implementation_version = platform.python_version()
    elif _implementation == 'PyPy':
        _implementation_version = '%s.%s.%s' % (sys.pypy_version_info.major,
                                                sys.pypy_version_info.minor,
                                                sys.pypy_version_info.micro)
        if sys.pypy_version_info.releaselevel != 'final':
            _implementation_version = ''.join([_implementation_version, sys.pypy_version_info.releaselevel])
    elif _implementation == 'Jython':
        _implementation_version = platform.python_version()  # Complete Guess
    elif _implementation == 'IronPython':
        _implementation_version = platform.python_version()  # Complete Guess
    else:
        _implementation_version = 'Unknown'

    try:
        p_system = platform.system()
        p_release = platform.release()
    except IOError:
        p_system = 'Unknown'
        p_release = 'Unknown'

    return " ".join(['pip/%s' % pip.__version__,
                     '%s/%s' % (_implementation, _implementation_version),
                     '%s/%s' % (p_system, p_release)])


def get_file_content(url, comes_from=None):
    """Gets the content of a file; it may be a filename, file: URL, or
    http: URL.  Returns (location, content).  Content is unicode."""
    match = _scheme_re.search(url)
    if match:
        scheme = match.group(1).lower()
        if (scheme == 'file' and comes_from
            and comes_from.startswith('http')):
            raise InstallationError(
                'Requirements file %s references URL %s, which is local'
                % (comes_from, url))
        if scheme == 'file':
            path = url.split(':', 1)[1]
            path = path.replace('\\', '/')
            match = _url_slash_drive_re.match(path)
            if match:
                path = match.group(1) + ':' + path.split('|', 1)[1]
            path = urllib.unquote(path)
            if path.startswith('/'):
                path = '/' + path.lstrip('/')
            url = path
        else:
            ## FIXME: catch some errors
            resp = urlopen(url)
            encoding = get_http_message_param(resp.headers, 'charset', 'utf-8')
            return geturl(resp), resp.read().decode(encoding)
    try:
        f = open(url)
        content = f.read()
    except IOError:
        e = sys.exc_info()[1]
        raise InstallationError('Could not open requirements file: %s' % str(e))
    else:
        f.close()
    return url, content


_scheme_re = re.compile(r'^(http|https|file):', re.I)
_url_slash_drive_re = re.compile(r'/*([a-z])\|', re.I)

class VerifiedHTTPSConnection(httplib.HTTPSConnection):
    """
    A connection that wraps connections with ssl certificate verification.
    """
    def connect(self):

        self.connection_kwargs = {}

        #TODO: refactor compatibility logic into backwardcompat?

        # for > py2.5
        if hasattr(self, 'timeout'):
            self.connection_kwargs.update(timeout = self.timeout)

        # for >= py2.7
        if hasattr(self, 'source_address'):
            self.connection_kwargs.update(source_address = self.source_address)

        sock = socket.create_connection((self.host, self.port), **self.connection_kwargs)

        # for >= py2.7
        if getattr(self, '_tunnel_host', None):
            self.sock = sock
            self._tunnel()

        # get alternate bundle or use our included bundle
        cert_path = os.environ.get('PIP_CERT', '') or default_cert_path

        self.sock = ssl.wrap_socket(sock,
                                self.key_file,
                                self.cert_file,
                                cert_reqs=ssl.CERT_REQUIRED,
                                ca_certs=cert_path)

        try:
            match_hostname(self.sock.getpeercert(), self.host)
        except CertificateError:
            self.sock.shutdown(socket.SHUT_RDWR)
            self.sock.close()
            raise



class VerifiedHTTPSHandler(urllib2.HTTPSHandler):
    """
    A HTTPSHandler that uses our own VerifiedHTTPSConnection.
    """
    def __init__(self, connection_class = VerifiedHTTPSConnection):
        self.specialized_conn_class = connection_class
        urllib2.HTTPSHandler.__init__(self)
    def https_open(self, req):
        return self.do_open(self.specialized_conn_class, req)


class URLOpener(object):
    """
    pip's own URL helper that adds HTTP auth and proxy support
    """
    def __init__(self):
        self.passman = urllib2.HTTPPasswordMgrWithDefaultRealm()
        self.proxy_handler = None

    def __call__(self, url):
        """
        If the given url contains auth info or if a normal request gets a 401
        response, an attempt is made to fetch the resource using basic HTTP
        auth.

        """
        url, username, password, scheme = self.extract_credentials(url)
        if username is None:
            try:
                response = self.get_opener(scheme=scheme).open(url)
            except urllib2.HTTPError:
                e = sys.exc_info()[1]
                if e.code != 401:
                    raise
                response = self.get_response(url)
        else:
            response = self.get_response(url, username, password)
        return response

    def get_request(self, url):
        """
        Wraps the URL to retrieve to protects against "creative"
        interpretation of the RFC: http://bugs.python.org/issue8732
        """
        if isinstance(url, string_types):
            url = urllib2.Request(url, headers={'Accept-encoding': 'identity'})
        return url

    def get_response(self, url, username=None, password=None):
        """
        does the dirty work of actually getting the rsponse object using urllib2
        and its HTTP auth builtins.
        """
        scheme, netloc, path, query, frag = urlparse.urlsplit(url)
        req = self.get_request(url)

        stored_username, stored_password = self.passman.find_user_password(None, netloc)
        # see if we have a password stored
        if stored_username is None:
            if username is None and self.prompting:
                username = urllib.quote(raw_input('User for %s: ' % netloc))
                password = urllib.quote(getpass.getpass('Password: '))
            if username and password:
                self.passman.add_password(None, netloc, username, password)
            stored_username, stored_password = self.passman.find_user_password(None, netloc)
        authhandler = urllib2.HTTPBasicAuthHandler(self.passman)
        opener = self.get_opener(authhandler, scheme=scheme)
        # FIXME: should catch a 401 and offer to let the user reenter credentials
        return opener.open(req)

    def get_opener(self, *args, **kwargs):
        """
        Build an OpenerDirector instance based on the scheme and proxy option
        """

        args = list(args)
        if self.proxy_handler:
            args.extend([self.proxy_handler, urllib2.CacheFTPHandler])

        if kwargs.get('scheme') == 'https':
            https_handler = VerifiedHTTPSHandler()
            director = urllib2.build_opener(https_handler, *args)
            #strip out HTTPHandler to prevent MITM spoof
            for handler in director.handlers:
                if isinstance(handler, urllib2.HTTPHandler):
                    director.handlers.remove(handler)
        else:
            director = urllib2.build_opener(*args)

        # Add our new headers to the opener
        headers = [x for x in director.addheaders if x[0].lower() != "user-agent"]
        headers.append(("User-agent", build_user_agent()))
        director.addheaders = headers

        return director

    def setup(self, proxystr='', prompting=True):
        """
        Sets the proxy handler given the option passed on the command
        line.  If an empty string is passed it looks at the HTTP_PROXY
        environment variable.
        """
        self.prompting = prompting
        proxy = self.get_proxy(proxystr)
        if proxy:
            self.proxy_handler = urllib2.ProxyHandler({"http": proxy, "ftp": proxy, "https": proxy})

    def parse_credentials(self, netloc):
        if "@" in netloc:
            userinfo = netloc.rsplit("@", 1)[0]
            if ":" in userinfo:
                return userinfo.split(":", 1)
            return userinfo, None
        return None, None

    def extract_credentials(self, url):
        """
        Extracts user/password from a url.

        Returns a tuple:
            (url-without-auth, username, password)
        """
        if isinstance(url, urllib2.Request):
            result = urlparse.urlsplit(url.get_full_url())
        else:
            result = urlparse.urlsplit(url)
        scheme, netloc, path, query, frag = result

        username, password = self.parse_credentials(netloc)
        if username is None:
            return url, None, None, scheme
        elif password is None and self.prompting:
            # remove the auth credentials from the url part
            netloc = netloc.replace('%s@' % username, '', 1)
            # prompt for the password
            prompt = 'Password for %s@%s: ' % (username, netloc)
            password = urllib.quote(getpass.getpass(prompt))
        else:
            # remove the auth credentials from the url part
            netloc = netloc.replace('%s:%s@' % (username, password), '', 1)

        target_url = urlparse.urlunsplit((scheme, netloc, path, query, frag))
        return target_url, username, password, scheme

    def get_proxy(self, proxystr=''):
        """
        Get the proxy given the option passed on the command line.
        If an empty string is passed it looks at the HTTP_PROXY
        environment variable.
        """
        if not proxystr:
            proxystr = os.environ.get('HTTP_PROXY', '')
        if proxystr:
            if '@' in proxystr:
                user_password, server_port = proxystr.split('@', 1)
                if ':' in user_password:
                    user, password = user_password.split(':', 1)
                else:
                    user = user_password
                    prompt = 'Password for %s@%s: ' % (user, server_port)
                    password = urllib.quote(getpass.getpass(prompt))
                return '%s:%s@%s' % (user, password, server_port)
            else:
                return proxystr
        else:
            return None

urlopen = URLOpener()


def is_url(name):
    """Returns true if the name looks like a URL"""
    if ':' not in name:
        return False
    scheme = name.split(':', 1)[0].lower()
    return scheme in ['http', 'https', 'file', 'ftp'] + vcs.all_schemes


def url_to_path(url):
    """
    Convert a file: URL to a path.
    """
    assert url.startswith('file:'), (
        "You can only turn file: urls into filenames (not %r)" % url)
    path = url[len('file:'):].lstrip('/')
    path = urllib.unquote(path)
    if _url_drive_re.match(path):
        path = path[0] + ':' + path[2:]
    else:
        path = '/' + path
    return path


_drive_re = re.compile('^([a-z]):', re.I)
_url_drive_re = re.compile('^([a-z])[:|]', re.I)


def path_to_url(path):
    """
    Convert a path to a file: URL.  The path will be made absolute.
    """
    path = os.path.normcase(os.path.abspath(path))
    if _drive_re.match(path):
        path = path[0] + '|' + path[2:]
    url = urllib.quote(path)
    url = url.replace(os.path.sep, '/')
    url = url.lstrip('/')
    return 'file:///' + url


def path_to_url2(path):
    """
    Convert a path to a file: URL.  The path will be made absolute and have
    quoted path parts.
    """
    path = os.path.normpath(os.path.abspath(path))
    drive, path = os.path.splitdrive(path)
    filepath = path.split(os.path.sep)
    url = '/'.join([urllib.quote(part) for part in filepath])
    if not drive:
        url = url.lstrip('/')
    return 'file:///' + drive + url


def geturl(urllib2_resp):
    """
    Use instead of urllib.addinfourl.geturl(), which appears to have
    some issues with dropping the double slash for certain schemes
    (e.g. file://).  This implementation is probably over-eager, as it
    always restores '://' if it is missing, and it appears some url
    schemata aren't always followed by '//' after the colon, but as
    far as I know pip doesn't need any of those.
    The URI RFC can be found at: http://tools.ietf.org/html/rfc1630

    This function assumes that
        scheme:/foo/bar
    is the same as
        scheme:///foo/bar
    """
    url = urllib2_resp.geturl()
    scheme, rest = url.split(':', 1)
    if rest.startswith('//'):
        return url
    else:
        # FIXME: write a good test to cover it
        return '%s://%s' % (scheme, rest)


def is_archive_file(name):
    """Return True if `name` is a considered as an archive file."""
    archives = ('.zip', '.tar.gz', '.tar.bz2', '.tgz', '.tar', '.pybundle',
               '.whl')
    ext = splitext(name)[1].lower()
    if ext in archives:
        return True
    return False


def unpack_vcs_link(link, location, only_download=False):
    vcs_backend = _get_used_vcs_backend(link)
    if only_download:
        vcs_backend.export(location)
    else:
        vcs_backend.unpack(location)


def unpack_file_url(link, location):
    source = url_to_path(link.url)
    content_type = mimetypes.guess_type(source)[0]
    if os.path.isdir(source):
        # delete the location since shutil will create it again :(
        if os.path.isdir(location):
            rmtree(location)
        shutil.copytree(source, location)
    else:
        unpack_file(source, location, content_type, link)


def _get_used_vcs_backend(link):
    for backend in vcs.backends:
        if link.scheme in backend.schemes:
            vcs_backend = backend(link.url)
            return vcs_backend


def is_vcs_url(link):
    return bool(_get_used_vcs_backend(link))


def is_file_url(link):
    return link.url.lower().startswith('file:')


def _check_hash(download_hash, link):
    if download_hash.digest_size != hashlib.new(link.hash_name).digest_size:
        logger.fatal("Hash digest size of the package %d (%s) doesn't match the expected hash name %s!"
                    % (download_hash.digest_size, link, link.hash_name))
        raise HashMismatch('Hash name mismatch for package %s' % link)
    if download_hash.hexdigest() != link.hash:
        logger.fatal("Hash of the package %s (%s) doesn't match the expected hash %s!"
                     % (link, download_hash.hexdigest(), link.hash))
        raise HashMismatch('Bad %s hash for package %s' % (link.hash_name, link))


def _get_hash_from_file(target_file, link):
    try:
        download_hash = hashlib.new(link.hash_name)
    except (ValueError, TypeError):
        logger.warn("Unsupported hash name %s for package %s" % (link.hash_name, link))
        return None

    fp = open(target_file, 'rb')
    while True:
        chunk = fp.read(4096)
        if not chunk:
            break
        download_hash.update(chunk)
    fp.close()
    return download_hash


def _download_url(resp, link, temp_location):
    fp = open(temp_location, 'wb')
    download_hash = None
    if link.hash and link.hash_name:
        try:
            download_hash = hashlib.new(link.hash_name)
        except ValueError:
            logger.warn("Unsupported hash name %s for package %s" % (link.hash_name, link))
    try:
        total_length = int(resp.info()['content-length'])
    except (ValueError, KeyError, TypeError):
        total_length = 0
    downloaded = 0
    show_progress = total_length > 40 * 1000 or not total_length
    show_url = link.show_url
    try:
        if show_progress:
            ## FIXME: the URL can get really long in this message:
            if total_length:
                logger.start_progress('Downloading %s (%s): ' % (show_url, format_size(total_length)))
            else:
                logger.start_progress('Downloading %s (unknown size): ' % show_url)
        else:
            logger.notify('Downloading %s' % show_url)
        logger.info('Downloading from URL %s' % link)

        while True:
            chunk = resp.read(4096)
            if not chunk:
                break
            downloaded += len(chunk)
            if show_progress:
                if not total_length:
                    logger.show_progress('%s' % format_size(downloaded))
                else:
                    logger.show_progress('%3i%%  %s' % (100 * downloaded / total_length, format_size(downloaded)))
            if download_hash is not None:
                download_hash.update(chunk)
            fp.write(chunk)
        fp.close()
    finally:
        if show_progress:
            logger.end_progress('%s downloaded' % format_size(downloaded))
    return download_hash


def _copy_file(filename, location, content_type, link):
    copy = True
    download_location = os.path.join(location, link.filename)
    if os.path.exists(download_location):
        response = ask_path_exists(
            'The file %s exists. (i)gnore, (w)ipe, (b)ackup ' %
            display_path(download_location), ('i', 'w', 'b'))
        if response == 'i':
            copy = False
        elif response == 'w':
            logger.warn('Deleting %s' % display_path(download_location))
            os.remove(download_location)
        elif response == 'b':
            dest_file = backup_dir(download_location)
            logger.warn('Backing up %s to %s'
                        % (display_path(download_location), display_path(dest_file)))
            shutil.move(download_location, dest_file)
    if copy:
        shutil.copy(filename, download_location)
        logger.indent -= 2
        logger.notify('Saved %s' % display_path(download_location))


def unpack_http_url(link, location, download_cache, download_dir=None):
    temp_dir = tempfile.mkdtemp('-unpack', 'pip-')
    temp_location = None
    target_url = link.url.split('#', 1)[0]

    already_cached = False
    cache_file = None
    cache_content_type_file = None
    download_hash = None
    if download_cache:
        cache_file = os.path.join(download_cache,
                                   urllib.quote(target_url, ''))
        cache_content_type_file = cache_file + '.content-type'
        already_cached = (
            os.path.exists(cache_file) and
            os.path.exists(cache_content_type_file)
            )
        if not os.path.isdir(download_cache):
            create_download_cache_folder(download_cache)

    already_downloaded = None
    if download_dir:
        already_downloaded = os.path.join(download_dir, link.filename)
        if not os.path.exists(already_downloaded):
            already_downloaded = None

    if already_downloaded:
        temp_location = already_downloaded
        content_type = mimetypes.guess_type(already_downloaded)[0]
        logger.notify('File was already downloaded %s' % already_downloaded)
        if link.hash:
            download_hash = _get_hash_from_file(temp_location, link)
            try:
                _check_hash(download_hash, link)
            except HashMismatch:
                logger.warn(
                    'Previously-downloaded file %s has bad hash, '
                    're-downloading.' % temp_location
                    )
                temp_location = None
                os.unlink(already_downloaded)
                already_downloaded = None

    # We have a cached file, and we haven't already found a good downloaded copy
    if already_cached and not temp_location:
        with open(cache_content_type_file) as fp:
            content_type = fp.read().strip()
        temp_location = cache_file
        logger.notify('Using download cache from %s' % cache_file)
        if link.hash and link.hash_name:
            download_hash = _get_hash_from_file(cache_file, link)
            try:
                _check_hash(download_hash, link)
            except HashMismatch:
                logger.warn(
                    'Cached file %s has bad hash, '
                    're-downloading.' % temp_location
                    )
                temp_location = None
                os.unlink(cache_file)
                os.unlink(cache_content_type_file)
                already_cached = False

    # We don't have either a cached or a downloaded copy
    if not temp_location:
        resp = _get_response_from_url(target_url, link)
        content_type = resp.info().get('content-type', '')
        filename = link.filename  # fallback
        # Have a look at the Content-Disposition header for a better guess
        content_disposition = resp.info().get('content-disposition')
        if content_disposition:
            type, params = cgi.parse_header(content_disposition)
            # We use ``or`` here because we don't want to use an "empty" value
            # from the filename param.
            filename = params.get('filename') or filename
        ext = splitext(filename)[1]
        if not ext:
            ext = mimetypes.guess_extension(content_type)
            if ext:
                filename += ext
        if not ext and link.url != geturl(resp):
            ext = os.path.splitext(geturl(resp))[1]
            if ext:
                filename += ext
        temp_location = os.path.join(temp_dir, filename)
        download_hash = _download_url(resp, link, temp_location)
        if link.hash and link.hash_name:
            _check_hash(download_hash, link)

    if download_dir and not already_downloaded:
        _copy_file(temp_location, download_dir, content_type, link)
    unpack_file(temp_location, location, content_type, link)
    if cache_file and not already_cached:
        cache_download(cache_file, temp_location, content_type)
    if not (already_cached or already_downloaded):
        os.unlink(temp_location)
    os.rmdir(temp_dir)


def _get_response_from_url(target_url, link):
    try:
        resp = urlopen(target_url)
    except urllib2.HTTPError:
        e = sys.exc_info()[1]
        logger.fatal("HTTP error %s while getting %s" % (e.code, link))
        raise
    except IOError:
        e = sys.exc_info()[1]
        # Typically an FTP error
        logger.fatal("Error %s while getting %s" % (e, link))
        raise
    return resp


class Urllib2HeadRequest(urllib2.Request):
    def get_method(self):
        return "HEAD"
