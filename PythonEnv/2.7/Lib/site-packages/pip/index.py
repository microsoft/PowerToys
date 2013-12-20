"""Routines related to PyPI, indexes"""

import sys
import os
import re
import gzip
import mimetypes
import posixpath
import pkg_resources
import random
import socket
import ssl
import string
import zlib

try:
    import threading
except ImportError:
    import dummy_threading as threading

from pip.log import logger
from pip.util import Inf, normalize_name, splitext, is_prerelease
from pip.exceptions import DistributionNotFound, BestVersionAlreadyInstalled,\
    InstallationError
from pip.backwardcompat import (WindowsError, BytesIO,
                                Queue, urlparse,
                                URLError, HTTPError, u,
                                product, url2pathname,
                                Empty as QueueEmpty)
from pip.backwardcompat import CertificateError
from pip.download import urlopen, path_to_url2, url_to_path, geturl, Urllib2HeadRequest
from pip.wheel import Wheel, wheel_ext, wheel_setuptools_support, setuptools_requirement
from pip.pep425tags import supported_tags, supported_tags_noarch, get_platform
from pip.vendor import html5lib

__all__ = ['PackageFinder']


DEFAULT_MIRROR_HOSTNAME = "last.pypi.python.org"


class PackageFinder(object):
    """This finds packages.

    This is meant to match easy_install's technique for looking for
    packages, by reading pages and looking for appropriate links
    """

    def __init__(self, find_links, index_urls,
            use_mirrors=False, mirrors=None, main_mirror_url=None,
            use_wheel=False, allow_external=[], allow_insecure=[],
            allow_all_external=False, allow_all_insecure=False,
            allow_all_prereleases=False):
        self.find_links = find_links
        self.index_urls = index_urls
        self.dependency_links = []
        self.cache = PageCache()
        # These are boring links that have already been logged somehow:
        self.logged_links = set()
        if use_mirrors:
            self.mirror_urls = self._get_mirror_urls(mirrors, main_mirror_url)
            logger.info('Using PyPI mirrors: %s' % ', '.join(self.mirror_urls))
        else:
            self.mirror_urls = []
        self.use_wheel = use_wheel

        # Do we allow (safe and verifiable) externally hosted files?
        self.allow_external = set(normalize_name(n) for n in allow_external)

        # Which names are allowed to install insecure and unverifiable files?
        self.allow_insecure = set(normalize_name(n) for n in allow_insecure)

        # Do we allow all (safe and verifiable) externally hosted files?
        self.allow_all_external = allow_all_external

        # Do we allow unsafe and unverifiable files?
        self.allow_all_insecure = allow_all_insecure

        # Stores if we ignored any external links so that we can instruct
        #   end users how to install them if no distributions are available
        self.need_warn_external = False

        # Stores if we ignored any unsafe links so that we can instruct
        #   end users how to install them if no distributions are available
        self.need_warn_insecure = False

        # Do we want to allow _all_ pre-releases?
        self.allow_all_prereleases = allow_all_prereleases

    @property
    def use_wheel(self):
        return self._use_wheel

    @use_wheel.setter
    def use_wheel(self, value):
        self._use_wheel = value
        if self._use_wheel and not wheel_setuptools_support():
            raise InstallationError("pip's wheel support requires %s." % setuptools_requirement)

    def add_dependency_links(self, links):
        ## FIXME: this shouldn't be global list this, it should only
        ## apply to requirements of the package that specifies the
        ## dependency_links value
        ## FIXME: also, we should track comes_from (i.e., use Link)
        self.dependency_links.extend(links)

    def _sort_locations(self, locations):
        """
        Sort locations into "files" (archives) and "urls", and return
        a pair of lists (files,urls)
        """
        files = []
        urls = []

        # puts the url for the given file path into the appropriate list
        def sort_path(path):
            url = path_to_url2(path)
            if mimetypes.guess_type(url, strict=False)[0] == 'text/html':
                urls.append(url)
            else:
                files.append(url)

        for url in locations:

            is_local_path = os.path.exists(url)
            is_file_url = url.startswith('file:')
            is_find_link = url in self.find_links

            if is_local_path or is_file_url:
                if is_local_path:
                    path = url
                else:
                    path = url_to_path(url)
                if is_find_link and os.path.isdir(path):
                    path = os.path.realpath(path)
                    for item in os.listdir(path):
                        sort_path(os.path.join(path, item))
                elif is_file_url and os.path.isdir(path):
                    urls.append(url)
                elif os.path.isfile(path):
                    sort_path(path)
            else:
                urls.append(url)

        return files, urls

    def _link_sort_key(self, link_tuple):
        """
        Function used to generate link sort key for link tuples.
        The greater the return value, the more preferred it is.
        If not finding wheels, then sorted by version only.
        If finding wheels, then the sort order is by version, then:
          1. existing installs
          2. wheels ordered via Wheel.support_index_min()
          3. source archives
        Note: it was considered to embed this logic into the Link
              comparison operators, but then different sdist links
              with the same version, would have to be considered equal
        """
        parsed_version, link, _ = link_tuple
        if self.use_wheel:
            support_num = len(supported_tags)
            if link == InfLink: # existing install
                pri = 1
            elif link.wheel:
                # all wheel links are known to be supported at this stage
                pri = -(link.wheel.support_index_min())
            else: # sdist
                pri = -(support_num)
            return (parsed_version, pri)
        else:
            return parsed_version

    def _sort_versions(self, applicable_versions):
        """
        Bring the latest version (and wheels) to the front, but maintain the existing ordering as secondary.
        See the docstring for `_link_sort_key` for details.
        This function is isolated for easier unit testing.
        """
        return sorted(applicable_versions, key=self._link_sort_key, reverse=True)

    def find_requirement(self, req, upgrade):

        def mkurl_pypi_url(url):
            loc = posixpath.join(url, url_name)
            # For maximum compatibility with easy_install, ensure the path
            # ends in a trailing slash.  Although this isn't in the spec
            # (and PyPI can handle it without the slash) some other index
            # implementations might break if they relied on easy_install's behavior.
            if not loc.endswith('/'):
                loc = loc + '/'
            return loc

        url_name = req.url_name
        # Only check main index if index URL is given:
        main_index_url = None
        if self.index_urls:
            # Check that we have the url_name correctly spelled:
            main_index_url = Link(mkurl_pypi_url(self.index_urls[0]), trusted=True)
            # This will also cache the page, so it's okay that we get it again later:
            page = self._get_page(main_index_url, req)
            if page is None:
                url_name = self._find_url_name(Link(self.index_urls[0], trusted=True), url_name, req) or req.url_name

        # Combine index URLs with mirror URLs here to allow
        # adding more index URLs from requirements files
        all_index_urls = self.index_urls + self.mirror_urls

        if url_name is not None:
            locations = [
                mkurl_pypi_url(url)
                for url in all_index_urls] + self.find_links
        else:
            locations = list(self.find_links)
        for version in req.absolute_versions:
            if url_name is not None and main_index_url is not None:
                locations = [
                    posixpath.join(main_index_url.url, version)] + locations

        file_locations, url_locations = self._sort_locations(locations)
        _flocations, _ulocations = self._sort_locations(self.dependency_links)
        file_locations.extend(_flocations)

        # We trust every url that the user has given us whether it was given
        #   via --index-url, --user-mirrors/--mirror, or --find-links or a
        #   default option thereof
        locations = [Link(url, trusted=True) for url in url_locations]

        # We explicitly do not trust links that came from dependency_links
        locations.extend([Link(url) for url in _ulocations])

        logger.debug('URLs to search for versions for %s:' % req)
        for location in locations:
            logger.debug('* %s' % location)
        found_versions = []
        found_versions.extend(
            self._package_versions(
                # We trust every directly linked archive in find_links
                [Link(url, '-f', trusted=True) for url in self.find_links], req.name.lower()))
        page_versions = []
        for page in self._get_pages(locations, req):
            logger.debug('Analyzing links from page %s' % page.url)
            logger.indent += 2
            try:
                page_versions.extend(self._package_versions(page.links, req.name.lower()))
            finally:
                logger.indent -= 2
        dependency_versions = list(self._package_versions(
            [Link(url) for url in self.dependency_links], req.name.lower()))
        if dependency_versions:
            logger.info('dependency_links found: %s' % ', '.join([link.url for parsed, link, version in dependency_versions]))
        file_versions = list(self._package_versions(
                [Link(url) for url in file_locations], req.name.lower()))
        if not found_versions and not page_versions and not dependency_versions and not file_versions:
            logger.fatal('Could not find any downloads that satisfy the requirement %s' % req)

            if self.need_warn_external:
                logger.warn("Some externally hosted files were ignored (use "
                            "--allow-external %s to allow)." % req.name)

            if self.need_warn_insecure:
                logger.warn("Some insecure and unverifiable files were ignored"
                            " (use --allow-insecure %s to allow)." % req.name)

            raise DistributionNotFound('No distributions at all found for %s' % req)
        installed_version = []
        if req.satisfied_by is not None:
            installed_version = [(req.satisfied_by.parsed_version, InfLink, req.satisfied_by.version)]
        if file_versions:
            file_versions.sort(reverse=True)
            logger.info('Local files found: %s' % ', '.join([url_to_path(link.url) for parsed, link, version in file_versions]))
        #this is an intentional priority ordering
        all_versions = installed_version + file_versions + found_versions + page_versions + dependency_versions
        applicable_versions = []
        for (parsed_version, link, version) in all_versions:
            if version not in req.req:
                logger.info("Ignoring link %s, version %s doesn't match %s"
                            % (link, version, ','.join([''.join(s) for s in req.req.specs])))
                continue
            elif is_prerelease(version) and not (self.allow_all_prereleases or req.prereleases):
                # If this version isn't the already installed one, then
                #   ignore it if it's a pre-release.
                if link is not InfLink:
                    logger.info("Ignoring link %s, version %s is a pre-release (use --pre to allow)." % (link, version))
                    continue
            applicable_versions.append((parsed_version, link, version))
        applicable_versions = self._sort_versions(applicable_versions)
        existing_applicable = bool([link for parsed_version, link, version in applicable_versions if link is InfLink])
        if not upgrade and existing_applicable:
            if applicable_versions[0][1] is InfLink:
                logger.info('Existing installed version (%s) is most up-to-date and satisfies requirement'
                            % req.satisfied_by.version)
            else:
                logger.info('Existing installed version (%s) satisfies requirement (most up-to-date version is %s)'
                            % (req.satisfied_by.version, applicable_versions[0][2]))
            return None
        if not applicable_versions:
            logger.fatal('Could not find a version that satisfies the requirement %s (from versions: %s)'
                         % (req, ', '.join([version for parsed_version, link, version in all_versions])))

            if self.need_warn_external:
                logger.warn("Some externally hosted files were ignored (use "
                            "--allow-external to allow).")

            if self.need_warn_insecure:
                logger.warn("Some insecure and unverifiable files were ignored"
                            " (use --allow-insecure %s to allow)." % req.name)

            raise DistributionNotFound('No distributions matching the version for %s' % req)
        if applicable_versions[0][1] is InfLink:
            # We have an existing version, and its the best version
            logger.info('Installed version (%s) is most up-to-date (past versions: %s)'
                        % (req.satisfied_by.version, ', '.join([version for parsed_version, link, version in applicable_versions[1:]]) or 'none'))
            raise BestVersionAlreadyInstalled
        if len(applicable_versions) > 1:
            logger.info('Using version %s (newest of versions: %s)' %
                        (applicable_versions[0][2], ', '.join([version for parsed_version, link, version in applicable_versions])))

        selected_version = applicable_versions[0][1]

        # TODO: Remove after 1.4 has been released
        if (selected_version.internal is not None
                and not selected_version.internal):
            logger.warn("You are installing an externally hosted file. Future "
                        "versions of pip will default to disallowing "
                        "externally hosted files.")

        if (selected_version.verifiable is not None
                and not selected_version.verifiable):
            logger.warn("You are installing a potentially insecure and "
                        "unverifiable file. Future versions of pip will "
                        "default to disallowing insecure files.")

        return selected_version


    def _find_url_name(self, index_url, url_name, req):
        """Finds the true URL name of a package, when the given name isn't quite correct.
        This is usually used to implement case-insensitivity."""
        if not index_url.url.endswith('/'):
            # Vaguely part of the PyPI API... weird but true.
            ## FIXME: bad to modify this?
            index_url.url += '/'
        page = self._get_page(index_url, req)
        if page is None:
            logger.fatal('Cannot fetch index base URL %s' % index_url)
            return
        norm_name = normalize_name(req.url_name)
        for link in page.links:
            base = posixpath.basename(link.path.rstrip('/'))
            if norm_name == normalize_name(base):
                logger.notify('Real name of requirement %s is %s' % (url_name, base))
                return base
        return None

    def _get_pages(self, locations, req):
        """Yields (page, page_url) from the given locations, skipping
        locations that have errors, and adding download/homepage links"""
        pending_queue = Queue()
        for location in locations:
            pending_queue.put(location)
        done = []
        seen = set()
        threads = []
        for i in range(min(10, len(locations))):
            t = threading.Thread(target=self._get_queued_page, args=(req, pending_queue, done, seen))
            t.setDaemon(True)
            threads.append(t)
            t.start()
        for t in threads:
            t.join()
        return done

    _log_lock = threading.Lock()

    def _get_queued_page(self, req, pending_queue, done, seen):
        while 1:
            try:
                location = pending_queue.get(False)
            except QueueEmpty:
                return
            if location in seen:
                continue
            seen.add(location)
            page = self._get_page(location, req)
            if page is None:
                continue
            done.append(page)
            for link in page.rel_links():
                normalized = normalize_name(req.name).lower()

                if (not normalized in self.allow_external
                        and not self.allow_all_external):
                    self.need_warn_external = True
                    logger.debug("Not searching %s for files because external "
                                 "urls are disallowed." % link)
                    continue

                if (link.trusted is not None
                        and not link.trusted
                        and not normalized in self.allow_insecure
                        and not self.allow_all_insecure):  # TODO: Remove after release
                    logger.debug("Not searching %s for urls, it is an "
                                "untrusted link and cannot produce safe or "
                                "verifiable files." % link)
                    self.need_warn_insecure = True
                    continue

                pending_queue.put(link)

    _egg_fragment_re = re.compile(r'#egg=([^&]*)')
    _egg_info_re = re.compile(r'([a-z0-9_.]+)-([a-z0-9_.-]+)', re.I)
    _py_version_re = re.compile(r'-py([123]\.?[0-9]?)$')

    def _sort_links(self, links):
        "Returns elements of links in order, non-egg links first, egg links second, while eliminating duplicates"
        eggs, no_eggs = [], []
        seen = set()
        for link in links:
            if link not in seen:
                seen.add(link)
                if link.egg_fragment:
                    eggs.append(link)
                else:
                    no_eggs.append(link)
        return no_eggs + eggs

    def _package_versions(self, links, search_name):
        for link in self._sort_links(links):
            for v in self._link_package_versions(link, search_name):
                yield v

    def _known_extensions(self):
        extensions = ('.tar.gz', '.tar.bz2', '.tar', '.tgz', '.zip')
        if self.use_wheel:
            return extensions + (wheel_ext,)
        return extensions

    def _link_package_versions(self, link, search_name):
        """
        Return an iterable of triples (pkg_resources_version_key,
        link, python_version) that can be extracted from the given
        link.

        Meant to be overridden by subclasses, not called by clients.
        """
        platform = get_platform()

        version = None
        if link.egg_fragment:
            egg_info = link.egg_fragment
        else:
            egg_info, ext = link.splitext()
            if not ext:
                if link not in self.logged_links:
                    logger.debug('Skipping link %s; not a file' % link)
                    self.logged_links.add(link)
                return []
            if egg_info.endswith('.tar'):
                # Special double-extension case:
                egg_info = egg_info[:-4]
                ext = '.tar' + ext
            if ext not in self._known_extensions():
                if link not in self.logged_links:
                    logger.debug('Skipping link %s; unknown archive format: %s' % (link, ext))
                    self.logged_links.add(link)
                return []
            if "macosx10" in link.path and ext == '.zip':
                if link not in self.logged_links:
                    logger.debug('Skipping link %s; macosx10 one' % (link))
                    self.logged_links.add(link)
                return []
            if link.wheel and link.wheel.name.lower() == search_name.lower():
                version = link.wheel.version
                if not link.wheel.supported():
                    logger.debug('Skipping %s because it is not compatible with this Python' % link)
                    return []

                # This is a dirty hack to prevent installing Binary Wheels from
                #   PyPI or one of its mirrors unless it is a Windows Binary
                #   Wheel. This is paired with a change to PyPI disabling
                #   uploads for the same. Once we have a mechanism for enabling
                #   support for binary wheels on linux that deals with the
                #   inherent problems of binary distribution this can be
                #   removed.
                comes_from = getattr(link, "comes_from", None)
                if (not platform.startswith('win')
                    and comes_from is not None
                    and urlparse.urlparse(comes_from.url).netloc.endswith(
                                                        "pypi.python.org")):
                    if not link.wheel.supported(tags=supported_tags_noarch):
                        logger.debug(
                            "Skipping %s because it is a pypi-hosted binary "
                            "Wheel on an unsupported platform" % link
                        )
                        return []

        if not version:
            version = self._egg_info_matches(egg_info, search_name, link)
        if version is None:
            logger.debug('Skipping link %s; wrong project name (not %s)' % (link, search_name))
            return []

        if (link.internal is not None
                and not link.internal
                and not normalize_name(search_name).lower() in self.allow_external
                and not self.allow_all_external):
            # We have a link that we are sure is external, so we should skip
            #   it unless we are allowing externals
            logger.debug("Skipping %s because it is externally hosted." % link)
            self.need_warn_external = True
            return []

        if (link.verifiable is not None
                and not link.verifiable
                and not normalize_name(search_name).lower() in self.allow_insecure
                and not self.allow_all_insecure):  # TODO: Remove after release
            # We have a link that we are sure we cannot verify it's integrity,
            #   so we should skip it unless we are allowing unsafe installs
            #   for this requirement.
            logger.debug("Skipping %s because it is an insecure and "
                         "unverifiable file." % link)
            self.need_warn_insecure = True
            return []

        match = self._py_version_re.search(version)
        if match:
            version = version[:match.start()]
            py_version = match.group(1)
            if py_version != sys.version[:3]:
                logger.debug('Skipping %s because Python version is incorrect' % link)
                return []
        logger.debug('Found link %s, version: %s' % (link, version))
        return [(pkg_resources.parse_version(version),
               link,
               version)]

    def _egg_info_matches(self, egg_info, search_name, link):
        match = self._egg_info_re.search(egg_info)
        if not match:
            logger.debug('Could not parse version from link: %s' % link)
            return None
        name = match.group(0).lower()
        # To match the "safe" name that pkg_resources creates:
        name = name.replace('_', '-')
        # project name and version must be separated by a dash
        look_for = search_name.lower() + "-"
        if name.startswith(look_for):
            return match.group(0)[len(look_for):]
        else:
            return None

    def _get_page(self, link, req):
        return HTMLPage.get_page(link, req, cache=self.cache)

    def _get_mirror_urls(self, mirrors=None, main_mirror_url=None):
        """Retrieves a list of URLs from the main mirror DNS entry
        unless a list of mirror URLs are passed.
        """
        if not mirrors:
            mirrors = get_mirrors(main_mirror_url)
            # Should this be made "less random"? E.g. netselect like?
            random.shuffle(mirrors)

        mirror_urls = set()
        for mirror_url in mirrors:
            mirror_url = mirror_url.rstrip('/')
            # Make sure we have a valid URL
            if not any([mirror_url.startswith(scheme) for scheme in ["http://", "https://", "file://"]]):
                mirror_url = "http://%s" % mirror_url
            if not mirror_url.endswith("/simple"):
                mirror_url = "%s/simple" % mirror_url
            mirror_urls.add(mirror_url + '/')

        return list(mirror_urls)


class PageCache(object):
    """Cache of HTML pages"""

    failure_limit = 3

    def __init__(self):
        self._failures = {}
        self._pages = {}
        self._archives = {}

    def too_many_failures(self, url):
        return self._failures.get(url, 0) >= self.failure_limit

    def get_page(self, url):
        return self._pages.get(url)

    def is_archive(self, url):
        return self._archives.get(url, False)

    def set_is_archive(self, url, value=True):
        self._archives[url] = value

    def add_page_failure(self, url, level):
        self._failures[url] = self._failures.get(url, 0)+level

    def add_page(self, urls, page):
        for url in urls:
            self._pages[url] = page


class HTMLPage(object):
    """Represents one page, along with its URL"""

    ## FIXME: these regexes are horrible hacks:
    _homepage_re = re.compile(r'<th>\s*home\s*page', re.I)
    _download_re = re.compile(r'<th>\s*download\s+url', re.I)
    _href_re = re.compile('href=(?:"([^"]*)"|\'([^\']*)\'|([^>\\s\\n]*))', re.I|re.S)

    def __init__(self, content, url, headers=None, trusted=None):
        self.content = content
        self.parsed = html5lib.parse(self.content, namespaceHTMLElements=False)
        self.url = url
        self.headers = headers
        self.trusted = trusted

    def __str__(self):
        return self.url

    @classmethod
    def get_page(cls, link, req, cache=None, skip_archives=True):
        url = link.url
        url = url.split('#', 1)[0]
        if cache.too_many_failures(url):
            return None

        # Check for VCS schemes that do not support lookup as web pages.
        from pip.vcs import VcsSupport
        for scheme in VcsSupport.schemes:
            if url.lower().startswith(scheme) and url[len(scheme)] in '+:':
                logger.debug('Cannot look at %(scheme)s URL %(link)s' % locals())
                return None

        if cache is not None:
            inst = cache.get_page(url)
            if inst is not None:
                return inst
        try:
            if skip_archives:
                if cache is not None:
                    if cache.is_archive(url):
                        return None
                filename = link.filename
                for bad_ext in ['.tar', '.tar.gz', '.tar.bz2', '.tgz', '.zip']:
                    if filename.endswith(bad_ext):
                        content_type = cls._get_content_type(url)
                        if content_type.lower().startswith('text/html'):
                            break
                        else:
                            logger.debug('Skipping page %s because of Content-Type: %s' % (link, content_type))
                            if cache is not None:
                                cache.set_is_archive(url)
                            return None
            logger.debug('Getting page %s' % url)

            # Tack index.html onto file:// URLs that point to directories
            (scheme, netloc, path, params, query, fragment) = urlparse.urlparse(url)
            if scheme == 'file' and os.path.isdir(url2pathname(path)):
                # add trailing slash if not present so urljoin doesn't trim final segment
                if not url.endswith('/'):
                    url += '/'
                url = urlparse.urljoin(url, 'index.html')
                logger.debug(' file: URL is directory, getting %s' % url)

            resp = urlopen(url)

            real_url = geturl(resp)
            headers = resp.info()
            contents = resp.read()
            encoding = headers.get('Content-Encoding', None)
            #XXX need to handle exceptions and add testing for this
            if encoding is not None:
                if encoding == 'gzip':
                    contents = gzip.GzipFile(fileobj=BytesIO(contents)).read()
                if encoding == 'deflate':
                    contents = zlib.decompress(contents)

            # The check for archives above only works if the url ends with
            #   something that looks like an archive. However that is not a
            #   requirement. For instance http://sourceforge.net/projects/docutils/files/docutils/0.8.1/docutils-0.8.1.tar.gz/download
            #   redirects to http://superb-dca3.dl.sourceforge.net/project/docutils/docutils/0.8.1/docutils-0.8.1.tar.gz
            #   Unless we issue a HEAD request on every url we cannot know
            #   ahead of time for sure if something is HTML or not. However we
            #   can check after we've downloaded it.
            content_type = headers.get('Content-Type', 'unknown')
            if not content_type.lower().startswith("text/html"):
                logger.debug('Skipping page %s because of Content-Type: %s' %
                                            (link, content_type))
                if cache is not None:
                    cache.set_is_archive(url)
                return None

            inst = cls(u(contents), real_url, headers, trusted=link.trusted)
        except (HTTPError, URLError, socket.timeout, socket.error, OSError, WindowsError):
            e = sys.exc_info()[1]
            desc = str(e)
            if isinstance(e, socket.timeout):
                log_meth = logger.info
                level =1
                desc = 'timed out'
            elif isinstance(e, URLError):
                #ssl/certificate error
                if hasattr(e, 'reason') and (isinstance(e.reason, ssl.SSLError) or isinstance(e.reason, CertificateError)):
                    desc = 'There was a problem confirming the ssl certificate: %s' % e
                    log_meth = logger.notify
                else:
                    log_meth = logger.info
                if hasattr(e, 'reason') and isinstance(e.reason, socket.timeout):
                    desc = 'timed out'
                    level = 1
                else:
                    level = 2
            elif isinstance(e, HTTPError) and e.code == 404:
                ## FIXME: notify?
                log_meth = logger.info
                level = 2
            else:
                log_meth = logger.info
                level = 1
            log_meth('Could not fetch URL %s: %s' % (link, desc))
            log_meth('Will skip URL %s when looking for download links for %s' % (link.url, req))
            if cache is not None:
                cache.add_page_failure(url, level)
            return None
        if cache is not None:
            cache.add_page([url, real_url], inst)
        return inst

    @staticmethod
    def _get_content_type(url):
        """Get the Content-Type of the given url, using a HEAD request"""
        scheme, netloc, path, query, fragment = urlparse.urlsplit(url)
        if not scheme in ('http', 'https', 'ftp', 'ftps'):
            ## FIXME: some warning or something?
            ## assertion error?
            return ''
        req = Urllib2HeadRequest(url, headers={'Host': netloc})
        resp = urlopen(req)
        try:
            if hasattr(resp, 'code') and resp.code != 200 and scheme not in ('ftp', 'ftps'):
                ## FIXME: doesn't handle redirects
                return ''
            return resp.info().get('content-type', '')
        finally:
            resp.close()

    @property
    def api_version(self):
        if not hasattr(self, "_api_version"):
            _api_version = None

            metas = [x for x in self.parsed.findall(".//meta")
                        if x.get("name", "").lower() == "api-version"]
            if metas:
                try:
                    _api_version = int(metas[0].get("value", None))
                except (TypeError, ValueError):
                    _api_version = None
            self._api_version = _api_version
        return self._api_version

    @property
    def base_url(self):
        if not hasattr(self, "_base_url"):
            base = self.parsed.find(".//base")
            if base is not None and base.get("href"):
                self._base_url = base.get("href")
            else:
                self._base_url = self.url
        return self._base_url

    @property
    def links(self):
        """Yields all links in the page"""
        for anchor in self.parsed.findall(".//a"):
            if anchor.get("href"):
                href = anchor.get("href")
                url = self.clean_link(urlparse.urljoin(self.base_url, href))

                # Determine if this link is internal. If that distinction
                #   doesn't make sense in this context, then we don't make
                #   any distinction.
                internal = None
                if self.api_version and self.api_version >= 2:
                    # Only api_versions >= 2 have a distinction between
                    #   external and internal links
                    internal = bool(anchor.get("rel")
                                and "internal" in anchor.get("rel").split())

                yield Link(url, self, internal=internal)

    def rel_links(self):
        for url in self.explicit_rel_links():
            yield url
        for url in self.scraped_rel_links():
            yield url

    def explicit_rel_links(self, rels=('homepage', 'download')):
        """Yields all links with the given relations"""
        rels = set(rels)

        for anchor in self.parsed.findall(".//a"):
            if anchor.get("rel") and anchor.get("href"):
                found_rels = set(anchor.get("rel").split())
                # Determine the intersection between what rels were found and
                #   what rels were being looked for
                if found_rels & rels:
                    href = anchor.get("href")
                    url = self.clean_link(urlparse.urljoin(self.base_url, href))
                    yield Link(url, self, trusted=False)

    def scraped_rel_links(self):
        # Can we get rid of this horrible horrible method?
        for regex in (self._homepage_re, self._download_re):
            match = regex.search(self.content)
            if not match:
                continue
            href_match = self._href_re.search(self.content, pos=match.end())
            if not href_match:
                continue
            url = href_match.group(1) or href_match.group(2) or href_match.group(3)
            if not url:
                continue
            url = self.clean_link(urlparse.urljoin(self.base_url, url))
            yield Link(url, self, trusted=False)

    _clean_re = re.compile(r'[^a-z0-9$&+,/:;=?@.#%_\\|-]', re.I)

    def clean_link(self, url):
        """Makes sure a link is fully encoded.  That is, if a ' ' shows up in
        the link, it will be rewritten to %20 (while not over-quoting
        % or other characters)."""
        return self._clean_re.sub(
            lambda match: '%%%2x' % ord(match.group(0)), url)


class Link(object):

    def __init__(self, url, comes_from=None, internal=None, trusted=None):
        self.url = url
        self.comes_from = comes_from
        self.internal = internal
        self.trusted = trusted

        # Set whether it's a wheel
        self.wheel = None
        if url != Inf and self.splitext()[1] == wheel_ext:
            self.wheel = Wheel(self.filename)

    def __str__(self):
        if self.comes_from:
            return '%s (from %s)' % (self.url, self.comes_from)
        else:
            return str(self.url)

    def __repr__(self):
        return '<Link %s>' % self

    def __eq__(self, other):
        return self.url == other.url

    def __ne__(self, other):
        return self.url != other.url

    def __lt__(self, other):
        return self.url < other.url

    def __le__(self, other):
        return self.url <= other.url

    def __gt__(self, other):
        return self.url > other.url

    def __ge__(self, other):
        return self.url >= other.url

    def __hash__(self):
        return hash(self.url)

    @property
    def filename(self):
        _, netloc, path, _, _ = urlparse.urlsplit(self.url)
        name = posixpath.basename(path.rstrip('/')) or netloc
        assert name, ('URL %r produced no filename' % self.url)
        return name

    @property
    def scheme(self):
        return urlparse.urlsplit(self.url)[0]

    @property
    def path(self):
        return urlparse.urlsplit(self.url)[2]

    def splitext(self):
        return splitext(posixpath.basename(self.path.rstrip('/')))

    @property
    def url_without_fragment(self):
        scheme, netloc, path, query, fragment = urlparse.urlsplit(self.url)
        return urlparse.urlunsplit((scheme, netloc, path, query, None))

    _egg_fragment_re = re.compile(r'#egg=([^&]*)')

    @property
    def egg_fragment(self):
        match = self._egg_fragment_re.search(self.url)
        if not match:
            return None
        return match.group(1)

    _hash_re = re.compile(r'(sha1|sha224|sha384|sha256|sha512|md5)=([a-f0-9]+)')

    @property
    def hash(self):
        match = self._hash_re.search(self.url)
        if match:
            return match.group(2)
        return None

    @property
    def hash_name(self):
        match = self._hash_re.search(self.url)
        if match:
            return match.group(1)
        return None

    @property
    def show_url(self):
        return posixpath.basename(self.url.split('#', 1)[0].split('?', 1)[0])

    @property
    def verifiable(self):
        """
        Returns True if this link can be verified after download, False if it
        cannot, and None if we cannot determine.
        """
        trusted = self.trusted or getattr(self.comes_from, "trusted", None)
        if trusted is not None and trusted:
            # This link came from a trusted source. It *may* be verifiable but
            #   first we need to see if this page is operating under the new
            #   API version.
            try:
                api_version = getattr(self.comes_from, "api_version", None)
                api_version = int(api_version)
            except (ValueError, TypeError):
                api_version = None

            if api_version is None or api_version <= 1:
                # This link is either trusted, or it came from a trusted,
                #   however it is not operating under the API version 2 so
                #   we can't make any claims about if it's safe or not
                return

            if self.hash:
                # This link came from a trusted source and it has a hash, so we
                #   can consider it safe.
                return True
            else:
                # This link came from a trusted source, using the new API
                #   version, and it does not have a hash. It is NOT verifiable
                return False
        elif trusted is not None:
            # This link came from an untrusted source and we cannot trust it
            return False

#An "Infinite Link" that compares greater than other links
InfLink = Link(Inf) #this object is not currently used as a sortable


def get_requirement_from_url(url):
    """Get a requirement from the URL, if possible.  This looks for #egg
    in the URL"""
    link = Link(url)
    egg_info = link.egg_fragment
    if not egg_info:
        egg_info = splitext(link.filename)[0]
    return package_to_requirement(egg_info)


def package_to_requirement(package_name):
    """Translate a name like Foo-1.2 to Foo==1.3"""
    match = re.search(r'^(.*?)-(dev|\d.*)', package_name)
    if match:
        name = match.group(1)
        version = match.group(2)
    else:
        name = package_name
        version = ''
    if version:
        return '%s==%s' % (name, version)
    else:
        return name


def get_mirrors(hostname=None):
    """Return the list of mirrors from the last record found on the DNS
    entry::

    >>> from pip.index import get_mirrors
    >>> get_mirrors()
    ['a.pypi.python.org', 'b.pypi.python.org', 'c.pypi.python.org',
    'd.pypi.python.org']

    Originally written for the distutils2 project by Alexis Metaireau.
    """
    if hostname is None:
        hostname = DEFAULT_MIRROR_HOSTNAME

    # return the last mirror registered on PyPI.
    last_mirror_hostname = None
    try:
        last_mirror_hostname = socket.gethostbyname_ex(hostname)[0]
    except socket.gaierror:
        return []
    if not last_mirror_hostname or last_mirror_hostname == DEFAULT_MIRROR_HOSTNAME:
        last_mirror_hostname = "z.pypi.python.org"
    end_letter = last_mirror_hostname.split(".", 1)

    # determine the list from the last one.
    return ["%s.%s" % (s, end_letter[1]) for s in string_range(end_letter[0])]


def string_range(last):
    """Compute the range of string between "a" and last.

    This works for simple "a to z" lists, but also for "a to zz" lists.
    """
    for k in range(len(last)):
        for x in product(string.ascii_lowercase, repeat=k+1):
            result = ''.join(x)
            yield result
            if result == last:
                return

