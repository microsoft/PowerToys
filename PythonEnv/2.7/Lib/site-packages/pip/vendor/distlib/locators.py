# -*- coding: utf-8 -*-
#
# Copyright (C) 2012-2013 Vinay Sajip.
# Licensed to the Python Software Foundation under a contributor agreement.
# See LICENSE.txt and CONTRIBUTORS.txt.
#

import gzip
from io import BytesIO
import json
import logging
import os
import posixpath
import re
import threading
import zlib

from . import DistlibException
from .compat import (urljoin, urlparse, urlunparse, url2pathname, pathname2url,
                     queue, quote, unescape, string_types, build_opener,
                     HTTPRedirectHandler as BaseRedirectHandler,
                     Request, HTTPError, URLError)
from .database import Distribution, DistributionPath, make_dist
from .metadata import Metadata
from .util import (cached_property, parse_credentials, ensure_slash,
                   split_filename, get_project_data, parse_requirement,
                   ServerProxy)
from .version import get_scheme, UnsupportedVersionError
from .wheel import Wheel, is_compatible

logger = logging.getLogger(__name__)

MD5_HASH = re.compile('^md5=([a-f0-9]+)$')
CHARSET = re.compile(r';\s*charset\s*=\s*(.*)\s*$', re.I)
HTML_CONTENT_TYPE = re.compile('text/html|application/x(ht)?ml')
DEFAULT_INDEX = 'http://python.org/pypi'

def get_all_distribution_names(url=None):
    """
    Return all distribution names known by an index.
    :param url: The URL of the index.
    :return: A list of all known distribution names.
    """
    if url is None:
        url = DEFAULT_INDEX
    client = ServerProxy(url, timeout=3.0)
    return client.list_packages()

class RedirectHandler(BaseRedirectHandler):
    """
    A class to work around a bug in some Python 3.2.x releases.
    """
    # There's a bug in the base version for some 3.2.x
    # (e.g. 3.2.2 on Ubuntu Oneiric). If a Location header
    # returns e.g. /abc, it bails because it says the scheme ''
    # is bogus, when actually it should use the request's
    # URL for the scheme. See Python issue #13696.
    def http_error_302(self, req, fp, code, msg, headers):
        # Some servers (incorrectly) return multiple Location headers
        # (so probably same goes for URI).  Use first header.
        newurl = None
        for key in ('location', 'uri'):
            if key in headers:
                newurl = headers[key]
                break
        if newurl is None:
            return
        urlparts = urlparse(newurl)
        if urlparts.scheme == '':
            newurl = urljoin(req.get_full_url(), newurl)
            if hasattr(headers, 'replace_header'):
                headers.replace_header(key, newurl)
            else:
                headers[key] = newurl
        return BaseRedirectHandler.http_error_302(self, req, fp, code, msg,
                                                  headers)

    http_error_301 = http_error_303 = http_error_307 = http_error_302

class Locator(object):
    """
    A base class for locators - things that locate distributions.
    """
    source_extensions = ('.tar.gz', '.tar.bz2', '.tar', '.zip', '.tgz', '.tbz')
    binary_extensions = ('.egg', '.exe', '.whl')
    excluded_extensions = ('.pdf',)

    # A list of tags indicating which wheels you want to match. The default
    # value of None matches against the tags compatible with the running
    # Python. If you want to match other values, set wheel_tags on a locator
    # instance to a list of tuples (pyver, abi, arch) which you want to match.
    wheel_tags = None

    downloadable_extensions = source_extensions + ('.whl',)

    def __init__(self, scheme='default'):
        """
        Initialise an instance.
        :param scheme: Because locators look for most recent versions, they
                       need to know the version scheme to use. This specifies
                       the current PEP-recommended scheme - use ``'legacy'``
                       if you need to support existing distributions on PyPI.
        """
        self._cache = {}
        self.scheme = scheme
        # Because of bugs in some of the handlers on some of the platforms,
        # we use our own opener rather than just using urlopen.
        self.opener = build_opener(RedirectHandler())

    def clear_cache(self):
        self._cache.clear()

    def _get_scheme(self):
        return self._scheme

    def _set_scheme(self, value):
        self._scheme = value

    scheme = property(_get_scheme, _set_scheme)

    def _get_project(self, name):
        """
        For a given project, get a dictionary mapping available versions to Distribution
        instances.

        This should be implemented in subclasses.
        """
        raise NotImplementedError('Please implement in the subclass')

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        raise NotImplementedError('Please implement in the subclass')

    def get_project(self, name):
        """
        For a given project, get a dictionary mapping available versions to Distribution
        instances.

        This calls _get_project to do all the work, and just implements a caching layer on top.
        """
        if self._cache is None:
            result = self._get_project(name)
        elif name in self._cache:
            result = self._cache[name]
        else:
            result = self._get_project(name)
            self._cache[name] = result
        return result

    def score_url(self, url):
        """
        Give an url a score which can be used to choose preferred URLs
        for a given project release.
        """
        t = urlparse(url)
        return (t.scheme != 'https', 'pypi.python.org' in t.netloc,
                posixpath.basename(t.path))

    def prefer_url(self, url1, url2):
        """
        Choose one of two URLs where both are candidates for distribution
        archives for the same version of a distribution (for example,
        .tar.gz vs. zip).

        The current implement favours http:// URLs over https://, archives
        from PyPI over those from other locations and then the archive name.
        """
        if url1 == 'UNKNOWN':
            result = url2
        else:
            result = url2
            s1 = self.score_url(url1)
            s2 = self.score_url(url2)
            if s1 > s2:
                result = url1
            if result != url2:
                logger.debug('Not replacing %r with %r', url1, url2)
            else:
                logger.debug('Replacing %r with %r', url1, url2)
        return result

    def split_filename(self, filename, project_name):
        """
        Attempt to split a filename in project name, version and Python version.
        """
        return split_filename(filename, project_name)

    def convert_url_to_download_info(self, url, project_name):
        """
        See if a URL is a candidate for a download URL for a project (the URL
        has typically been scraped from an HTML page).

        If it is, a dictionary is returned with keys "name", "version",
        "filename" and "url"; otherwise, None is returned.
        """
        def same_project(name1, name2):
            name1, name2 = name1.lower(), name2.lower()
            if name1 == name2:
                result = True
            else:
                # distribute replaces '-' by '_' in project names, so it
                # can tell where the version starts in a filename.
                result = name1.replace('_', '-') == name2.replace('_', '-')
            return result

        result = None
        scheme, netloc, path, params, query, frag = urlparse(url)
        if frag.lower().startswith('egg='):
            logger.debug('%s: version hint in fragment: %r',
                         project_name, frag)
        origpath = path
        if path and path[-1] == '/':
            path = path[:-1]
        if path.endswith('.whl'):
            try:
                wheel = Wheel(path)
                if is_compatible(wheel, self.wheel_tags):
                    if project_name is None:
                        include = True
                    else:
                        include = same_project(wheel.name, project_name)
                    if include:
                        result = {
                            'name': wheel.name,
                            'version': wheel.version,
                            'filename': wheel.filename,
                            'url': urlunparse((scheme, netloc, origpath,
                                               params, query, '')),
                            'python-version': ', '.join(
                                ['.'.join(list(v[2:])) for v in wheel.pyver]),
                        }
                        m = MD5_HASH.match(frag)
                        if m:
                            result['md5_digest'] = m.group(1)
            except Exception as e:
                logger.warning('invalid path for wheel: %s', path)
        elif path.endswith(self.downloadable_extensions):
            path = filename = posixpath.basename(path)
            for ext in self.downloadable_extensions:
                if path.endswith(ext):
                    path = path[:-len(ext)]
                    t = self.split_filename(path, project_name)
                    if not t:
                        logger.debug('No match for project/version: %s', path)
                    else:
                        name, version, pyver = t
                        if not project_name or same_project(project_name, name):
                            result = {
                                'name': name,
                                'version': version,
                                'filename': filename,
                                'url': urlunparse((scheme, netloc, origpath,
                                                   params, query, '')),
                                #'packagetype': 'sdist',
                            }
                            if pyver:
                                result['python-version'] = pyver
                            m = MD5_HASH.match(frag)
                            if m:
                                result['md5_digest'] = m.group(1)
                    break
        return result

    def _update_version_data(self, result, info):
        """
        Update a result dictionary (the final result from _get_project) with a dictionary for a
        specific version, whih typically holds information gleaned from a filename or URL for an
        archive for the distribution.
        """
        name = info.pop('name')
        version = info.pop('version')
        if version in result:
            dist = result[version]
            md = dist.metadata
        else:
            dist = make_dist(name, version, scheme=self.scheme)
            md = dist.metadata
        dist.md5_digest = info.get('md5_digest')
        if 'python-version' in info:
            md['Requires-Python'] = info['python-version']
        if md['Download-URL'] != info['url']:
            md['Download-URL'] = self.prefer_url(md['Download-URL'],
                                                 info['url'])
        dist.locator = self
        result[version] = dist

    def locate(self, requirement, prereleases=False):
        """
        Find the most recent distribution which matches the given
        requirement.

        :param requirement: A requirement of the form 'foo (1.0)' or perhaps
                            'foo (>= 1.0, < 2.0, != 1.3)'
        :param prereleases: If ``True``, allow pre-release versions
                            to be located. Otherwise, pre-release versions
                            are not returned.
        :return: A :class:`Distribution` instance, or ``None`` if no such
                 distribution could be located.
        """
        result = None
        scheme = get_scheme(self.scheme)
        r = parse_requirement(requirement)
        if r is None:
            raise DistlibException('Not a valid requirement: %r' % requirement)
        if r.extras:
            # lose the extras part of the requirement
            requirement = r.requirement
        matcher = scheme.matcher(requirement)
        vcls = matcher.version_class
        logger.debug('matcher: %s (%s)', matcher, type(matcher).__name__)
        versions = self.get_project(matcher.name)
        if versions:
            # sometimes, versions are invalid
            slist = []
            for k in versions:
                try:
                    if not matcher.match(k):
                        logger.debug('%s did not match %r', matcher, k)
                    else:
                        if prereleases or not vcls(k).is_prerelease:
                            slist.append(k)
                        else:
                            logger.debug('skipping pre-release version %s', k)
                except Exception:
                    logger.warning('error matching %s with %r', matcher, k)
                    pass # slist.append(k)
            if len(slist) > 1:
                slist = sorted(slist, key=scheme.key)
            if slist:
                logger.debug('sorted list: %s', slist)
                result = versions[slist[-1]]
        if result and r.extras:
            result.extras = r.extras
        return result


class PyPIRPCLocator(Locator):
    """
    This locator uses XML-RPC to locate distributions. It therefore cannot be
    used with simple mirrors (that only mirror file content).
    """
    def __init__(self, url, **kwargs):
        """
        Initialise an instance.

        :param url: The URL to use for XML-RPC.
        :param kwargs: Passed to the superclass constructor.
        """
        super(PyPIRPCLocator, self).__init__(**kwargs)
        self.base_url = url
        self.client = ServerProxy(url, timeout=3.0)

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        return set(self.client.list_packages())

    def _get_project(self, name):
        result = {}
        versions = self.client.package_releases(name, True)
        for v in versions:
            urls = self.client.release_urls(name, v)
            data = self.client.release_data(name, v)
            metadata = Metadata(scheme=self.scheme)
            metadata.update(data)
            dist = Distribution(metadata)
            if urls:
                info = urls[0]
                metadata['Download-URL'] = info['url']
                dist.md5_digest = info.get('md5_digest')
                dist.locator = self
                result[v] = dist
        return result

class PyPIJSONLocator(Locator):
    """
    This locator uses PyPI's JSON interface. It's very limited in functionality
    nad probably not worth using.
    """
    def __init__(self, url, **kwargs):
        super(PyPIJSONLocator, self).__init__(**kwargs)
        self.base_url = ensure_slash(url)

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        raise NotImplementedError('Not available from this locator')

    def _get_project(self, name):
        result = {}
        url = urljoin(self.base_url, '%s/json' % quote(name))
        try:
            resp = self.opener.open(url)
            data = resp.read().decode() # for now
            d = json.loads(data)
            md = Metadata(scheme=self.scheme)
            md.update(d['info'])
            dist = Distribution(md)
            urls = d['urls']
            if urls:
                info = urls[0]
                md['Download-URL'] = info['url']
                dist.md5_digest = info.get('md5_digest')
                dist.locator = self
                result[md.version] = dist
        except Exception as e:
            logger.exception('JSON fetch failed: %s', e)
        return result


class Page(object):
    """
    This class represents a scraped HTML page.
    """
    # The following slightly hairy-looking regex just looks for the contents of
    # an anchor link, which has an attribute "href" either immediately preceded
    # or immediately followed by a "rel" attribute. The attribute values can be
    # declared with double quotes, single quotes or no quotes - which leads to
    # the length of the expression.
    _href = re.compile("""
(rel\s*=\s*(?:"(?P<rel1>[^"]*)"|'(?P<rel2>[^']*)'|(?P<rel3>[^>\s\n]*))\s+)?
href\s*=\s*(?:"(?P<url1>[^"]*)"|'(?P<url2>[^']*)'|(?P<url3>[^>\s\n]*))
(\s+rel\s*=\s*(?:"(?P<rel4>[^"]*)"|'(?P<rel5>[^']*)'|(?P<rel6>[^>\s\n]*)))?
""", re.I | re.S | re.X)
    _base = re.compile(r"""<base\s+href\s*=\s*['"]?([^'">]+)""", re.I | re.S)

    def __init__(self, data, url):
        """
        Initialise an instance with the Unicode page contents and the URL they
        came from.
        """
        self.data = data
        self.base_url = self.url = url
        m = self._base.search(self.data)
        if m:
            self.base_url = m.group(1)

    _clean_re = re.compile(r'[^a-z0-9$&+,/:;=?@.#%_\\|-]', re.I)

    @cached_property
    def links(self):
        """
        Return the URLs of all the links on a page together with information
        about their "rel" attribute, for determining which ones to treat as
        downloads and which ones to queue for further scraping.
        """
        def clean(url):
            "Tidy up an URL."
            scheme, netloc, path, params, query, frag = urlparse(url)
            return urlunparse((scheme, netloc, quote(path),
                               params, query, frag))

        result = set()
        for match in self._href.finditer(self.data):
            d = match.groupdict('')
            rel = (d['rel1'] or d['rel2'] or d['rel3'] or
                   d['rel4'] or d['rel5'] or d['rel6'])
            url = d['url1'] or d['url2'] or d['url3']
            url = urljoin(self.base_url, url)
            url = unescape(url)
            url = self._clean_re.sub(lambda m: '%%%2x' % ord(m.group(0)), url)
            result.add((url, rel))
        # We sort the result, hoping to bring the most recent versions
        # to the front
        result = sorted(result, key=lambda t: t[0], reverse=True)
        return result


class SimpleScrapingLocator(Locator):
    """
    A locator which scrapes HTML pages to locate downloads for a distribution.
    This runs multiple threads to do the I/O; performance is at least as good
    as pip's PackageFinder, which works in an analogous fashion.
    """

    # These are used to deal with various Content-Encoding schemes.
    decoders = {
        'deflate': zlib.decompress,
        'gzip': lambda b: gzip.GzipFile(fileobj=BytesIO(d)).read(),
        'none': lambda b: b,
    }

    def __init__(self, url, timeout=None, num_workers=10, **kwargs):
        """
        Initialise an instance.
        :param url: The root URL to use for scraping.
        :param timeout: The timeout, in seconds, to be applied to requests.
                        This defaults to ``None`` (no timeout specified).
        :param num_workers: The number of worker threads you want to do I/O,
                            This defaults to 10.
        :param kwargs: Passed to the superclass.
        """
        super(SimpleScrapingLocator, self).__init__(**kwargs)
        self.base_url = ensure_slash(url)
        self.timeout = timeout
        self._page_cache = {}
        self._seen = set()
        self._to_fetch = queue.Queue()
        self._bad_hosts = set()
        self.skip_externals = False
        self.num_workers = num_workers
        self._lock = threading.RLock()

    def _prepare_threads(self):
        """
        Threads are created only when get_project is called, and terminate
        before it returns. They are there primarily to parallelise I/O (i.e.
        fetching web pages).
        """
        self._threads = []
        for i in range(self.num_workers):
            t = threading.Thread(target=self._fetch)
            t.setDaemon(True)
            t.start()
            self._threads.append(t)

    def _wait_threads(self):
        """
        Tell all the threads to terminate (by sending a sentinel value) and
        wait for them to do so.
        """
        # Note that you need two loops, since you can't say which
        # thread will get each sentinel
        for t in self._threads:
            self._to_fetch.put(None)    # sentinel
        for t in self._threads:
            t.join()
        self._threads = []

    def _get_project(self, name):
        self.result = result = {}
        self.project_name = name
        url = urljoin(self.base_url, '%s/' % quote(name))
        self._seen.clear()
        self._page_cache.clear()
        self._prepare_threads()
        try:
            logger.debug('Queueing %s', url)
            self._to_fetch.put(url)
            self._to_fetch.join()
        finally:
            self._wait_threads()
        del self.result
        return result

    platform_dependent = re.compile(r'\b(linux-(i\d86|x86_64|arm\w+)|'
                                    r'win(32|-amd64)|macosx-?\d+)\b', re.I)

    def _is_platform_dependent(self, url):
        """
        Does an URL refer to a platform-specific download?
        """
        return self.platform_dependent.search(url)

    def _process_download(self, url):
        """
        See if an URL is a suitable download for a project.

        If it is, register information in the result dictionary (for
        _get_project) about the specific version it's for.

        Note that the return value isn't actually used other than as a boolean
        value.
        """
        if self._is_platform_dependent(url):
            info = None
        else:
            info = self.convert_url_to_download_info(url, self.project_name)
        logger.debug('process_download: %s -> %s', url, info)
        if info:
            with self._lock:    # needed because self.result is shared
                self._update_version_data(self.result, info)
        return info

    def _should_queue(self, link, referrer, rel):
        """
        Determine whether a link URL from a referring page and with a
        particular "rel" attribute should be queued for scraping.
        """
        scheme, netloc, path, _, _, _ = urlparse(link)
        if path.endswith(self.source_extensions + self.binary_extensions +
                         self.excluded_extensions):
            result = False
        elif self.skip_externals and not link.startswith(self.base_url):
            result = False
        elif not referrer.startswith(self.base_url):
            result = False
        elif rel not in ('homepage', 'download'):
            result = False
        elif scheme not in ('http', 'https', 'ftp'):
            result = False
        elif self._is_platform_dependent(link):
            result = False
        else:
            host = netloc.split(':', 1)[0]
            if host.lower() == 'localhost':
                result = False
            else:
                result = True
        logger.debug('should_queue: %s (%s) from %s -> %s', link, rel,
                     referrer, result)
        return result

    def _fetch(self):
        """
        Get a URL to fetch from the work queue, get the HTML page, examine its
        links for download candidates and candidates for further scraping.

        This is a handy method to run in a thread.
        """
        while True:
            url = self._to_fetch.get()
            try:
                if url:
                    page = self.get_page(url)
                    if page is None:    # e.g. after an error
                        continue
                    for link, rel in page.links:
                        if link not in self._seen:
                            self._seen.add(link)
                            if (not self._process_download(link) and
                                self._should_queue(link, url, rel)):
                                logger.debug('Queueing %s from %s', link, url)
                                self._to_fetch.put(link)
            finally:
                # always do this, to avoid hangs :-)
                self._to_fetch.task_done()
            if not url:
                #logger.debug('Sentinel seen, quitting.')
                break

    def get_page(self, url):
        """
        Get the HTML for an URL, possibly from an in-memory cache.

        XXX TODO Note: this cache is never actually cleared. It's assumed that
        the data won't get stale over the lifetime of a locator instance (not
        necessarily true for the default_locator).
        """
        # http://peak.telecommunity.com/DevCenter/EasyInstall#package-index-api
        scheme, netloc, path, _, _, _ = urlparse(url)
        if scheme == 'file' and os.path.isdir(url2pathname(path)):
            url = urljoin(ensure_slash(url), 'index.html')

        if url in self._page_cache:
            result = self._page_cache[url]
            logger.debug('Returning %s from cache: %s', url, result)
        else:
            host = netloc.split(':', 1)[0]
            result = None
            if host in self._bad_hosts:
                logger.debug('Skipping %s due to bad host %s', url, host)
            else:
                req = Request(url, headers={'Accept-encoding': 'identity'})
                try:
                    logger.debug('Fetching %s', url)
                    resp = self.opener.open(req, timeout=self.timeout)
                    logger.debug('Fetched %s', url)
                    headers = resp.info()
                    content_type = headers.get('Content-Type', '')
                    if HTML_CONTENT_TYPE.match(content_type):
                        final_url = resp.geturl()
                        data = resp.read()
                        encoding = headers.get('Content-Encoding')
                        if encoding:
                            decoder = self.decoders[encoding]   # fail if not found
                            data = decoder(data)
                        encoding = 'utf-8'
                        m = CHARSET.search(content_type)
                        if m:
                            encoding = m.group(1)
                        try:
                            data = data.decode(encoding)
                        except UnicodeError:
                            data = data.decode('latin-1')    # fallback
                        result = Page(data, final_url)
                        self._page_cache[final_url] = result
                except HTTPError as e:
                    if e.code != 404:
                        logger.exception('Fetch failed: %s: %s', url, e)
                except URLError as e:
                    logger.exception('Fetch failed: %s: %s', url, e)
                    with self._lock:
                        self._bad_hosts.add(host)
                except Exception as e:
                    logger.exception('Fetch failed: %s: %s', url, e)
                finally:
                    self._page_cache[url] = result   # even if None (failure)
        return result

    _distname_re = re.compile('<a href=[^>]*>([^<]+)<')

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        result = set()
        page = self.get_page(self.base_url)
        if not page:
            raise DistlibException('Unable to get %s' % self.base_url)
        for match in self._distname_re.finditer(page.data):
            result.add(match.group(1))
        return result

class DirectoryLocator(Locator):
    """
    This class locates distributions in a directory tree.
    """

    def __init__(self, path, **kwargs):
        """
        Initialise an instance.
        :param path: The root of the directory tree to search.
        :param kwargs: Passed to the superclass constructor,
                       except for:
                       * recursive - if True (the default), subdirectories are
                         recursed into. If False, only the top-level directory
                         is searched,
        """
        self.recursive = kwargs.pop('recursive', True)
        super(DirectoryLocator, self).__init__(**kwargs)
        path = os.path.abspath(path)
        if not os.path.isdir(path):
            raise DistlibException('Not a directory: %r' % path)
        self.base_dir = path

    def should_include(self, filename, parent):
        """
        Should a filename be considered as a candidate for a distribution
        archive? As well as the filename, the directory which contains it
        is provided, though not used by the current implementation.
        """
        return filename.endswith(self.downloadable_extensions)

    def _get_project(self, name):
        result = {}
        for root, dirs, files in os.walk(self.base_dir):
            for fn in files:
                if self.should_include(fn, root):
                    fn = os.path.join(root, fn)
                    url = urlunparse(('file', '',
                                      pathname2url(os.path.abspath(fn)),
                                      '', '', ''))
                    info = self.convert_url_to_download_info(url, name)
                    if info:
                        self._update_version_data(result, info)
            if not self.recursive:
                break
        return result

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        result = set()
        for root, dirs, files in os.walk(self.base_dir):
            for fn in files:
                if self.should_include(fn, root):
                    fn = os.path.join(root, fn)
                    url = urlunparse(('file', '',
                                      pathname2url(os.path.abspath(fn)),
                                      '', '', ''))
                    info = self.convert_url_to_download_info(url, None)
                    if info:
                        result.add(info['name'])
            if not self.recursive:
                break
        return result

class JSONLocator(Locator):
    """
    This locator uses special extended metadata (not available on PyPI) and is
    the basis of performant dependency resolution in distlib. Other locators
    require archive downloads before dependencies can be determined! As you
    might imagine, that can be slow.
    """
    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        raise NotImplementedError('Not available from this locator')

    def _get_project(self, name):
        result = {}
        data = get_project_data(name)
        if data:
            for info in data.get('files', []):
                if info['ptype'] != 'sdist' or info['pyversion'] != 'source':
                    continue
                dist = make_dist(data['name'], info['version'],
                                 scheme=self.scheme)
                md = dist.metadata
                md['Download-URL'] = info['url']
                dist.md5_digest = info.get('digest')
                md.dependencies = info.get('requirements', {})
                dist.exports = info.get('exports', {})
                result[dist.version] = dist
        return result

class DistPathLocator(Locator):
    """
    This locator finds installed distributions in a path. It can be useful for
    adding to an :class:`AggregatingLocator`.
    """
    def __init__(self, distpath, **kwargs):
        """
        Initialise an instance.

        :param distpath: A :class:`DistributionPath` instance to search.
        """
        super(DistPathLocator, self).__init__(**kwargs)
        assert isinstance(distpath, DistributionPath)
        self.distpath = distpath

    def _get_project(self, name):
        dist = self.distpath.get_distribution(name)
        if dist is None:
            result = {}
        else:
            result = { dist.version: dist }
        return result


class AggregatingLocator(Locator):
    """
    This class allows you to chain and/or merge a list of locators.
    """
    def __init__(self, *locators, **kwargs):
        """
        Initialise an instance.

        :param locators: The list of locators to search.
        :param kwargs: Passed to the superclass constructor,
                       except for:
                       * merge - if False (the default), the first successful
                         search from any of the locators is returned. If True,
                         the results from all locators are merged (this can be
                         slow).
        """
        self.merge = kwargs.pop('merge', False)
        self.locators = locators
        super(AggregatingLocator, self).__init__(**kwargs)

    def clear_cache(self):
        super(AggregatingLocator, self).clear_cache()
        for locator in self.locators:
            locator.clear_cache()

    def _set_scheme(self, value):
        self._scheme = value
        for locator in self.locators:
            locator.scheme = value

    scheme = property(Locator.scheme.fget, _set_scheme)

    def _get_project(self, name):
        result = {}
        for locator in self.locators:
            r = locator.get_project(name)
            if r:
                if self.merge:
                    result.update(r)
                else:
                    result = r
                    break
        return result

    def get_distribution_names(self):
        """
        Return all the distribution names known to this locator.
        """
        result = set()
        for locator in self.locators:
            try:
                result |= locator.get_distribution_names()
            except NotImplementedError:
                pass
        return result


default_locator = AggregatingLocator(
                    JSONLocator(),
                    SimpleScrapingLocator('https://pypi.python.org/simple/',
                                          timeout=3.0))

locate = default_locator.locate

class DependencyFinder(object):
    """
    Locate dependencies for distributions.
    """

    def __init__(self, locator=None):
        """
        Initialise an instance, using the specified locator
        to locate distributions.
        """
        self.locator = locator or default_locator
        self.scheme = get_scheme(self.locator.scheme)

    def _get_name_and_version(self, p):
        """
        A utility method used to get name and version from e.g. a Provides-Dist
        value.

        :param p: A value in a form foo (1.0)
        :return: The name and version as a tuple.
        """
        comps = p.strip().rsplit(' ', 1)
        name = comps[0]
        version = None
        if len(comps) == 2:
            version = comps[1]
            if len(version) < 3 or version[0] != '(' or version[-1] != ')':
                raise DistlibException('Ill-formed provides field: %r' % p)
            version = version[1:-1]  # trim off parentheses
        # Name in lower case for case-insensitivity
        return name.lower(), version

    def add_distribution(self, dist):
        """
        Add a distribution to the finder. This will update internal information
        about who provides what.
        :param dist: The distribution to add.
        """
        logger.debug('adding distribution %s', dist)
        name = dist.key
        self.dists_by_name[name] = dist
        self.dists[(name, dist.version)] = dist
        for p in dist.provides:
            name, version = self._get_name_and_version(p)
            logger.debug('Add to provided: %s, %s, %s', name, version, dist)
            self.provided.setdefault(name, set()).add((version, dist))

    def remove_distribution(self, dist):
        """
        Remove a distribution from the finder. This will update internal
        information about who provides what.
        :param dist: The distribution to remove.
        """
        logger.debug('removing distribution %s', dist)
        name = dist.key
        del self.dists_by_name[name]
        del self.dists[(name, dist.version)]
        for p in dist.provides:
            name, version = self._get_name_and_version(p)
            logger.debug('Remove from provided: %s, %s, %s', name, version, dist)
            s = self.provided[name]
            s.remove((version, dist))
            if not s:
                del self.provided[name]

    def get_matcher(self, reqt):
        """
        Get a version matcher for a requirement.
        :param reqt: The requirement
        :type reqt: str
        :return: A version matcher (an instance of
                 :class:`distlib.version.Matcher`).
        """
        try:
            matcher = self.scheme.matcher(reqt)
        except UnsupportedVersionError:
            # XXX compat-mode if cannot read the version
            name = reqt.split()[0]
            matcher = self.scheme.matcher(name)
        return matcher

    def find_providers(self, reqt):
        """
        Find the distributions which can fulfill a requirement.

        :param reqt: The requirement.
         :type reqt: str
        :return: A set of distribution which can fulfill the requirement.
        """
        matcher = self.get_matcher(reqt)
        name = matcher.key   # case-insensitive
        result = set()
        provided = self.provided
        if name in provided:
            for version, provider in provided[name]:
                try:
                    match = matcher.match(version)
                except UnsupportedVersionError:
                    match = False

                if match:
                    result.add(provider)
                    break
        return result

    def try_to_replace(self, provider, other, problems):
        """
        Attempt to replace one provider with another. This is typically used
        when resolving dependencies from multiple sources, e.g. A requires
        (B >= 1.0) while C requires (B >= 1.1).

        For successful replacement, ``provider`` must meet all the requirements
        which ``other`` fulfills.

        :param provider: The provider we are trying to replace with.
        :param other: The provider we're trying to replace.
        :param problems: If False is returned, this will contain what
                         problems prevented replacement. This is currently
                         a tuple of the literal string 'cantreplace',
                         ``provider``, ``other``  and the set of requirements
                         that ``provider`` couldn't fulfill.
        :return: True if we can replace ``other`` with ``provider``, else
                 False.
        """
        rlist = self.reqts[other]
        unmatched = set()
        for s in rlist:
            matcher = self.get_matcher(s)
            if not matcher.match(provider.version):
                unmatched.add(s)
        if unmatched:
            # can't replace other with provider
            problems.add(('cantreplace', provider, other, unmatched))
            result = False
        else:
            # can replace other with provider
            self.remove_distribution(other)
            del self.reqts[other]
            for s in rlist:
                self.reqts.setdefault(provider, set()).add(s)
            self.add_distribution(provider)
            result = True
        return result

    def find(self, requirement, tests=False, prereleases=False):
        """
        Find a distribution matching requirement and all distributions
        it depends on. Use the ``tests`` argument to determine whether
        distributions used only for testing should be included in the
        results. Allow ``requirement`` to be either a :class:`Distribution`
        instance or a string expressing a requirement. If ``prereleases``
        is True, allow pre-release versions to be returned - otherwise,
        don't.

        Return a set of :class:`Distribution` instances and a set of
        problems.

        The distributions returned should be such that they have the
        :attr:`required` attribute set to ``True`` if they were
        from the ``requirement`` passed to ``find()``, and they have the
        :attr:`build_time_dependency` attribute set to ``True`` unless they
        are post-installation dependencies of the ``requirement``.

        The problems should be a tuple consisting of the string
        ``'unsatisfied'`` and the requirement which couldn't be satisfied
        by any distribution known to the locator.
        """

        self.provided = {}
        self.dists = {}
        self.dists_by_name = {}
        self.reqts = {}

        if isinstance(requirement, Distribution):
            dist = odist = requirement
            logger.debug('passed %s as requirement', odist)
        else:
            dist = odist = self.locator.locate(requirement,
                                               prereleases=prereleases)
            if dist is None:
                raise DistlibException('Unable to locate %r' % requirement)
            logger.debug('located %s', odist)
        dist.requested = True
        problems = set()
        todo = set([dist])
        install_dists = set([odist])
        while todo:
            dist = todo.pop()
            name = dist.key # case-insensitive
            if name not in self.dists_by_name:
                self.add_distribution(dist)
            else:
                #import pdb; pdb.set_trace()
                other = self.dists_by_name[name]
                if other != dist:
                    self.try_to_replace(dist, other, problems)

            ireqts = dist.requires
            sreqts = dist.setup_requires
            ereqts = set()
            if not tests or dist not in install_dists:
                treqts = set()
            else:
                treqts = dist.test_requires
            all_reqts = ireqts | sreqts | treqts | ereqts
            for r in all_reqts:
                providers = self.find_providers(r)
                if not providers:
                    logger.debug('No providers found for %r', r)
                    provider = self.locator.locate(r, prereleases=prereleases)
                    if provider is None:
                        logger.debug('Cannot satisfy %r', r)
                        problems.add(('unsatisfied', r))
                    else:
                        n, v = provider.key, provider.version
                        if (n, v) not in self.dists:
                            todo.add(provider)
                        providers.add(provider)
                        if r in ireqts and dist in install_dists:
                            install_dists.add(provider)
                            logger.debug('Adding %s to install_dists',
                                         provider.name_and_version)
                for p in providers:
                    name = p.key
                    if name not in self.dists_by_name:
                        self.reqts.setdefault(p, set()).add(r)
                    else:
                        other = self.dists_by_name[name]
                        if other != p:
                            # see if other can be replaced by p
                            self.try_to_replace(p, other, problems)

        dists = set(self.dists.values())
        for dist in dists:
            dist.build_time_dependency = dist not in install_dists
            if dist.build_time_dependency:
                logger.debug('%s is a build-time dependency only.',
                             dist.name_and_version)
        logger.debug('find done for %s', odist)
        return dists, problems
