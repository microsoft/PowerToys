"""Handles all VCS (version control) support"""

import os
import shutil

from pip.backwardcompat import urlparse, urllib
from pip.log import logger
from pip.util import (display_path, backup_dir, find_command,
                      rmtree, ask_path_exists)


__all__ = ['vcs', 'get_src_requirement']


class VcsSupport(object):
    _registry = {}
    schemes = ['ssh', 'git', 'hg', 'bzr', 'sftp', 'svn']

    def __init__(self):
        # Register more schemes with urlparse for various version control systems
        urlparse.uses_netloc.extend(self.schemes)
        # Python >= 2.7.4, 3.3 doesn't have uses_fragment
        if getattr(urlparse, 'uses_fragment', None):
            urlparse.uses_fragment.extend(self.schemes)
        super(VcsSupport, self).__init__()

    def __iter__(self):
        return self._registry.__iter__()

    @property
    def backends(self):
        return list(self._registry.values())

    @property
    def dirnames(self):
        return [backend.dirname for backend in self.backends]

    @property
    def all_schemes(self):
        schemes = []
        for backend in self.backends:
            schemes.extend(backend.schemes)
        return schemes

    def register(self, cls):
        if not hasattr(cls, 'name'):
            logger.warn('Cannot register VCS %s' % cls.__name__)
            return
        if cls.name not in self._registry:
            self._registry[cls.name] = cls

    def unregister(self, cls=None, name=None):
        if name in self._registry:
            del self._registry[name]
        elif cls in self._registry.values():
            del self._registry[cls.name]
        else:
            logger.warn('Cannot unregister because no class or name given')

    def get_backend_name(self, location):
        """
        Return the name of the version control backend if found at given
        location, e.g. vcs.get_backend_name('/path/to/vcs/checkout')
        """
        for vc_type in self._registry.values():
            path = os.path.join(location, vc_type.dirname)
            if os.path.exists(path):
                return vc_type.name
        return None

    def get_backend(self, name):
        name = name.lower()
        if name in self._registry:
            return self._registry[name]

    def get_backend_from_location(self, location):
        vc_type = self.get_backend_name(location)
        if vc_type:
            return self.get_backend(vc_type)
        return None


vcs = VcsSupport()


class VersionControl(object):
    name = ''
    dirname = ''

    def __init__(self, url=None, *args, **kwargs):
        self.url = url
        self._cmd = None
        super(VersionControl, self).__init__(*args, **kwargs)

    def _filter(self, line):
        return (logger.INFO, line)

    def _is_local_repository(self, repo):
        """
           posix absolute paths start with os.path.sep,
           win32 ones ones start with drive (like c:\\folder)
        """
        drive, tail = os.path.splitdrive(repo)
        return repo.startswith(os.path.sep) or drive

    @property
    def cmd(self):
        if self._cmd is not None:
            return self._cmd
        command = find_command(self.name)
        logger.info('Found command %r at %r' % (self.name, command))
        self._cmd = command
        return command

    def get_url_rev(self):
        """
        Returns the correct repository URL and revision by parsing the given
        repository URL
        """
        error_message = (
           "Sorry, '%s' is a malformed VCS url. "
           "The format is <vcs>+<protocol>://<url>, "
           "e.g. svn+http://myrepo/svn/MyApp#egg=MyApp")
        assert '+' in self.url, error_message % self.url
        url = self.url.split('+', 1)[1]
        scheme, netloc, path, query, frag = urlparse.urlsplit(url)
        rev = None
        if '@' in path:
            path, rev = path.rsplit('@', 1)
        url = urlparse.urlunsplit((scheme, netloc, path, query, ''))
        return url, rev

    def get_info(self, location):
        """
        Returns (url, revision), where both are strings
        """
        assert not location.rstrip('/').endswith(self.dirname), 'Bad directory: %s' % location
        return self.get_url(location), self.get_revision(location)

    def normalize_url(self, url):
        """
        Normalize a URL for comparison by unquoting it and removing any trailing slash.
        """
        return urllib.unquote(url).rstrip('/')

    def compare_urls(self, url1, url2):
        """
        Compare two repo URLs for identity, ignoring incidental differences.
        """
        return (self.normalize_url(url1) == self.normalize_url(url2))

    def parse_vcs_bundle_file(self, content):
        """
        Takes the contents of the bundled text file that explains how to revert
        the stripped off version control data of the given package and returns
        the URL and revision of it.
        """
        raise NotImplementedError

    def obtain(self, dest):
        """
        Called when installing or updating an editable package, takes the
        source path of the checkout.
        """
        raise NotImplementedError

    def switch(self, dest, url, rev_options):
        """
        Switch the repo at ``dest`` to point to ``URL``.
        """
        raise NotImplemented

    def update(self, dest, rev_options):
        """
        Update an already-existing repo to the given ``rev_options``.
        """
        raise NotImplementedError

    def check_destination(self, dest, url, rev_options, rev_display):
        """
        Prepare a location to receive a checkout/clone.

        Return True if the location is ready for (and requires) a
        checkout/clone, False otherwise.
        """
        checkout = True
        prompt = False
        if os.path.exists(dest):
            checkout = False
            if os.path.exists(os.path.join(dest, self.dirname)):
                existing_url = self.get_url(dest)
                if self.compare_urls(existing_url, url):
                    logger.info('%s in %s exists, and has correct URL (%s)' %
                                (self.repo_name.title(), display_path(dest),
                                 url))
                    logger.notify('Updating %s %s%s' %
                                  (display_path(dest), self.repo_name,
                                   rev_display))
                    self.update(dest, rev_options)
                else:
                    logger.warn('%s %s in %s exists with URL %s' %
                                (self.name, self.repo_name,
                                 display_path(dest), existing_url))
                    prompt = ('(s)witch, (i)gnore, (w)ipe, (b)ackup ',
                              ('s', 'i', 'w', 'b'))
            else:
                logger.warn('Directory %s already exists, '
                            'and is not a %s %s.' %
                            (dest, self.name, self.repo_name))
                prompt = ('(i)gnore, (w)ipe, (b)ackup ', ('i', 'w', 'b'))
        if prompt:
            logger.warn('The plan is to install the %s repository %s' %
                        (self.name, url))
            response = ask_path_exists('What to do?  %s' % prompt[0],
                                       prompt[1])

            if response == 's':
                logger.notify('Switching %s %s to %s%s' %
                              (self.repo_name, display_path(dest), url,
                               rev_display))
                self.switch(dest, url, rev_options)
            elif response == 'i':
                # do nothing
                pass
            elif response == 'w':
                logger.warn('Deleting %s' % display_path(dest))
                rmtree(dest)
                checkout = True
            elif response == 'b':
                dest_dir = backup_dir(dest)
                logger.warn('Backing up %s to %s'
                            % (display_path(dest), dest_dir))
                shutil.move(dest, dest_dir)
                checkout = True
        return checkout

    def unpack(self, location):
        if os.path.exists(location):
            rmtree(location)
        self.obtain(location)

    def get_src_requirement(self, dist, location, find_tags=False):
        raise NotImplementedError


def get_src_requirement(dist, location, find_tags):
    version_control = vcs.get_backend_from_location(location)
    if version_control:
        return version_control().get_src_requirement(dist, location, find_tags)
    logger.warn('cannot determine version of editable source in %s (is not SVN checkout, Git clone, Mercurial clone or Bazaar branch)' % location)
    return dist.as_requirement()
