#
# Copyright (C) 2012-2013 The Python Software Foundation.
# See LICENSE.txt and CONTRIBUTORS.txt.
#
import codecs
from collections import deque
import contextlib
import csv
from glob import iglob as std_iglob
import io
import json
import logging
import os
import py_compile
import re
import shutil
import socket
import ssl
import sys
import tarfile
import tempfile
import time
import zipfile

from . import DistlibException
from .compat import (string_types, text_type, shutil, raw_input,
                     cache_from_source, urlopen, httplib, xmlrpclib, splittype,
                     HTTPHandler, HTTPSHandler as BaseHTTPSHandler,
                     URLError, match_hostname, CertificateError)

logger = logging.getLogger(__name__)

class Container(object):
    """
    A generic container for when multiple values need to be returned
    """
    def __init__(self, **kwargs):
        self.__dict__.update(kwargs)

#
# Requirement parsing code for name + optional constraints + optional extras
#
# e.g. 'foo >= 1.2, < 2.0 [bar, baz]'
#
# The regex can seem a bit hairy, so we build it up out of smaller pieces
# which are manageable.
#

COMMA = r'\s*,\s*'
COMMA_RE = re.compile(COMMA)

IDENT = r'(\w|[.-])+'
RELOP = '([<>=!]=)|[<>]'

#
# The first relop is optional - if absent, will be taken as '=='
#
BARE_CONSTRAINTS = ('(' + RELOP + r')?\s*(' + IDENT + ')(' + COMMA + '(' +
                    RELOP + r')\s*(' + IDENT + '))*')

#
# Either the bare constraints or the bare constraints in parentheses
#
CONSTRAINTS = (r'\(\s*(?P<c1>' + BARE_CONSTRAINTS + r')\s*\)|(?P<c2>' +
               BARE_CONSTRAINTS + '\s*)')

EXTRA_LIST = IDENT + '(' + COMMA + IDENT + ')*'
EXTRAS = r'\[\s*(?P<ex>' + EXTRA_LIST + r')?\s*\]'
REQUIREMENT = ('(?P<dn>'  + IDENT + r')\s*(' + EXTRAS + r'\s*)?(\s*' +
               CONSTRAINTS + ')?$')
REQUIREMENT_RE = re.compile(REQUIREMENT)

#
# Used to scan through the constraints
#
RELOP_IDENT = '(?P<op>' + RELOP + r')\s*(?P<vn>' + IDENT + ')'
RELOP_IDENT_RE = re.compile(RELOP_IDENT)

def parse_requirement(s):

    def get_constraint(m):
        d = m.groupdict()
        return d['op'], d['vn']

    result = None
    m = REQUIREMENT_RE.match(s)
    if m:
        d = m.groupdict()
        name = d['dn']
        cons = d['c1'] or d['c2']
        if not cons:
            cons = None
            constr = ''
            rs = d['dn']
        else:
            if cons[0] not in '<>!=':
                cons = '==' + cons
            iterator = RELOP_IDENT_RE.finditer(cons)
            cons = [get_constraint(m) for m in iterator]
            rs = '%s (%s)' % (name, ', '.join(['%s %s' % con for con in cons]))
        if not d['ex']:
            extras = None
        else:
            extras = COMMA_RE.split(d['ex'])
        result = Container(name=name, constraints=cons, extras=extras,
                           requirement=rs, source=s)
    return result


def get_resources_dests(resources_root, rules):
    """Find destinations for resources files"""

    def get_rel_path(base, path):
        # normalizes and returns a lstripped-/-separated path
        base = base.replace(os.path.sep, '/')
        path = path.replace(os.path.sep, '/')
        assert path.startswith(base)
        return path[len(base):].lstrip('/')


    destinations = {}
    for base, suffix, dest in rules:
        prefix = os.path.join(resources_root, base)
        for abs_base in iglob(prefix):
            abs_glob = os.path.join(abs_base, suffix)
            for abs_path in iglob(abs_glob):
                resource_file = get_rel_path(resources_root, abs_path)
                if dest is None:  # remove the entry if it was here
                    destinations.pop(resource_file, None)
                else:
                    rel_path = get_rel_path(abs_base, abs_path)
                    rel_dest = dest.replace(os.path.sep, '/').rstrip('/')
                    destinations[resource_file] = rel_dest + '/' + rel_path
    return destinations


def in_venv():
    if hasattr(sys, 'real_prefix'):
        # virtualenv venvs
        result = True
    else:
        # PEP 405 venvs
        result = sys.prefix != getattr(sys, 'base_prefix', sys.prefix)
    return result


def get_executable():
    if sys.platform == 'darwin' and ('__VENV_LAUNCHER__'
                                     in os.environ):
        result =  os.environ['__VENV_LAUNCHER__']
    else:
        result = sys.executable
    return result


def proceed(prompt, allowed_chars, error_prompt=None, default=None):
    p = prompt
    while True:
        s = raw_input(p)
        p = prompt
        if not s and default:
            s = default
        if s:
            c = s[0].lower()
            if c in allowed_chars:
                break
            if error_prompt:
                p = '%c: %s\n%s' % (c, error_prompt, prompt)
    return c

@contextlib.contextmanager
def tempdir():
    td = tempfile.mkdtemp()
    try:
        yield td
    finally:
        shutil.rmtree(td)

@contextlib.contextmanager
def chdir(d):
    cwd = os.getcwd()
    try:
        os.chdir(d)
        yield
    finally:
        os.chdir(cwd)


@contextlib.contextmanager
def socket_timeout(seconds=15):
    cto = socket.getdefaulttimeout()
    try:
        socket.setdefaulttimeout(seconds)
        yield
    finally:
        socket.setdefaulttimeout(cto)


class cached_property(object):
    def __init__(self, func):
        self.func = func
        #for attr in ('__name__', '__module__', '__doc__'):
        #    setattr(self, attr, getattr(func, attr, None))

    def __get__(self, obj, type=None):
        if obj is None:
            return self
        value = self.func(obj)
        object.__setattr__(obj, self.func.__name__, value)
        #obj.__dict__[self.func.__name__] = value = self.func(obj)
        return value

def convert_path(pathname):
    """Return 'pathname' as a name that will work on the native filesystem.

    The path is split on '/' and put back together again using the current
    directory separator.  Needed because filenames in the setup script are
    always supplied in Unix style, and have to be converted to the local
    convention before we can actually use them in the filesystem.  Raises
    ValueError on non-Unix-ish systems if 'pathname' either starts or
    ends with a slash.
    """
    if os.sep == '/':
        return pathname
    if not pathname:
        return pathname
    if pathname[0] == '/':
        raise ValueError("path '%s' cannot be absolute" % pathname)
    if pathname[-1] == '/':
        raise ValueError("path '%s' cannot end with '/'" % pathname)

    paths = pathname.split('/')
    while os.curdir in paths:
        paths.remove(os.curdir)
    if not paths:
        return os.curdir
    return os.path.join(*paths)


class FileOperator(object):
    def __init__(self, dry_run=False):
        self.dry_run = dry_run
        self.ensured = set()
        self._init_record()

    def _init_record(self):
        self.record = False
        self.files_written = set()
        self.dirs_created = set()

    def record_as_written(self, path):
        if self.record:
            self.files_written.add(path)

    def newer(self, source, target):
        """Tell if the target is newer than the source.

        Returns true if 'source' exists and is more recently modified than
        'target', or if 'source' exists and 'target' doesn't.

        Returns false if both exist and 'target' is the same age or younger
        than 'source'. Raise PackagingFileError if 'source' does not exist.

        Note that this test is not very accurate: files created in the same
        second will have the same "age".
        """
        if not os.path.exists(source):
            raise DistlibException("file '%r' does not exist" %
                                   os.path.abspath(source))
        if not os.path.exists(target):
            return True

        return os.stat(source).st_mtime > os.stat(target).st_mtime

    def copy_file(self, infile, outfile):
        """Copy a file respecting dry-run and force flags.
        """
        assert not os.path.isdir(outfile)
        self.ensure_dir(os.path.dirname(outfile))
        logger.info('Copying %s to %s', infile, outfile)
        if not self.dry_run:
            shutil.copyfile(infile, outfile)
        if self.record:
            self.files_written.add(outfile)

    def copy_stream(self, instream, outfile, encoding=None):
        assert not os.path.isdir(outfile)
        self.ensure_dir(os.path.dirname(outfile))
        logger.info('Copying stream %s to %s', instream, outfile)
        if not self.dry_run:
            if encoding is None:
                outstream = open(outfile, 'wb')
            else:
                outstream = codecs.open(outfile, 'w', encoding=encoding)
            try:
                shutil.copyfileobj(instream, outstream)
            finally:
                outstream.close()
        if self.record:
            self.files_written.add(outfile)

    def write_binary_file(self, path, data):
        self.ensure_dir(os.path.dirname(path))
        if not self.dry_run:
            with open(path, 'wb') as f:
                f.write(data)
        if self.record:
            self.files_written.add(path)

    def write_text_file(self, path, data, encoding):
        self.ensure_dir(os.path.dirname(path))
        if not self.dry_run:
            with open(path, 'wb') as f:
                f.write(data.encode(encoding))
        if self.record:
            self.files_written.add(path)

    def set_mode(self, bits, mask, files):
        if os.name == 'posix':
            # Set the executable bits (owner, group, and world) on
            # all the files specified.
            for f in files:
                if self.dry_run:
                    logger.info("changing mode of %s", f)
                else:
                    mode = (os.stat(f).st_mode | bits) & mask
                    logger.info("changing mode of %s to %o", f, mode)
                    os.chmod(f, mode)

    set_executable_mode = lambda s, f: s.set_mode(0o555, 0o7777, f)

    def ensure_dir(self, path):
        path = os.path.abspath(path)
        if path not in self.ensured and not os.path.exists(path):
            self.ensured.add(path)
            d, f = os.path.split(path)
            self.ensure_dir(d)
            logger.info('Creating %s' % path)
            if not self.dry_run:
                os.mkdir(path)
            if self.record:
                self.dirs_created.add(path)

    def byte_compile(self, path, optimize=False, force=False, prefix=None):
        dpath = cache_from_source(path, not optimize)
        logger.info('Byte-compiling %s to %s', path, dpath)
        if not self.dry_run:
            if force or self.newer(path, dpath):
                if not prefix:
                    diagpath = None
                else:
                    assert path.startswith(prefix)
                    diagpath = path[len(prefix):]
            py_compile.compile(path, dpath, diagpath, True) # raise on error
        if self.record:
            self.files_written.add(dpath)
        return dpath

    def ensure_removed(self, path):
        if os.path.exists(path):
            if os.path.isdir(path) and not os.path.islink(path):
                logger.debug('Removing directory tree at %s', path)
                if not self.dry_run:
                    shutil.rmtree(path)
                if self.record:
                    if path in self.dirs_created:
                        self.dirs_created.remove(path)
            else:
                if os.path.islink(path):
                    s = 'link'
                else:
                    s = 'file'
                logger.debug('Removing %s %s', s, path)
                if not self.dry_run:
                    os.remove(path)
                if self.record:
                    if path in self.files_written:
                        self.files_written.remove(path)

    def is_writable(self, path):
        result = False
        while not result:
            if os.path.exists(path):
                result = os.access(path, os.W_OK)
                break
            parent = os.path.dirname(path)
            if parent == path:
                break
            path = parent
        return result

    def commit(self):
        """
        Commit recorded changes, turn off recording, return
        changes.
        """
        assert self.record
        result = self.files_written, self.dirs_created
        self._init_record()
        return result

    def rollback(self):
        if not self.dry_run:
            for f in list(self.files_written):
                if os.path.exists(f):
                    os.remove(f)
            # dirs should all be empty now, except perhaps for
            # __pycache__ subdirs
            # reverse so that subdirs appear before their parents
            dirs = sorted(self.dirs_created, reverse=True)
            for d in dirs:
                flist = os.listdir(d)
                if flist:
                    assert flist == ['__pycache__']
                    sd = os.path.join(d, flist[0])
                    os.rmdir(sd)
                os.rmdir(d) # should fail if non-empty
        self._init_record()

def resolve(module_name, dotted_path):
    if module_name in sys.modules:
        mod = sys.modules[module_name]
    else:
        mod = __import__(module_name)
    if dotted_path is None:
        result = mod
    else:
        parts = dotted_path.split('.')
        result = getattr(mod, parts.pop(0))
        for p in parts:
            result = getattr(result, p)
    return result


class ExportEntry(object):
    def __init__(self, name, prefix, suffix, flags):
        self.name = name
        self.prefix = prefix
        self.suffix = suffix
        self.flags = flags

    @cached_property
    def value(self):
        return resolve(self.prefix, self.suffix)

    def __repr__(self):
        return '<ExportEntry %s = %s:%s %s>' % (self.name, self.prefix,
                                                self.suffix, self.flags)

    def __eq__(self, other):
        if not isinstance(other, ExportEntry):
            result = False
        else:
            result = (self.name == other.name and
                      self.prefix == other.prefix and
                      self.suffix == other.suffix and
                      self.flags == other.flags)
        return result

    __hash__ = object.__hash__


ENTRY_RE = re.compile(r'''(?P<name>(\w|[-.])+)
                      \s*=\s*(?P<callable>(\w+)([:\.]\w+)*)
                      \s*(\[\s*(?P<flags>\w+(=\w+)?(,\s*\w+(=\w+)?)*)\s*\])?
                      ''', re.VERBOSE)


def get_export_entry(specification):
    m = ENTRY_RE.search(specification)
    if not m:
        result = None
        if '[' in specification or ']' in specification:
            raise DistlibException('Invalid specification '
                                   '%r' % specification)
    else:
        d = m.groupdict()
        name = d['name']
        path = d['callable']
        colons = path.count(':')
        if colons == 0:
            prefix, suffix = path, None
        else:
            if colons != 1:
                raise DistlibException('Invalid specification '
                                       '%r' % specification)
            prefix, suffix = path.split(':')
        flags = d['flags']
        if flags is None:
            if '[' in specification or ']' in specification:
                raise DistlibException('Invalid specification '
                                       '%r' % specification)
            flags = []
        else:
            flags = [f.strip() for f in flags.split(',')]
        result = ExportEntry(name, prefix, suffix, flags)
    return result


def get_cache_base(suffix=None):
    """
    Return the default base location for distlib caches. If the directory does
    not exist, it is created. Use the suffix provided for the base directory,
    and default to '.distlib' if it isn't provided.

    On Windows, if LOCALAPPDATA is defined in the environment, then it is
    assumed to be a directory, and will be the parent directory of the result.
    On POSIX, and on Windows if LOCALAPPDATA is not defined, the user's home
    directory - using os.expanduser('~') - will be the parent directory of
    the result.

    The result is just the directory '.distlib' in the parent directory as
    determined above, or with the name specified with ``suffix``.
    """
    if suffix is None:
        suffix = '.distlib'
    if os.name == 'nt' and 'LOCALAPPDATA' in os.environ:
        result = os.path.expandvars('$localappdata')
    else:
        # Assume posix, or old Windows
        result = os.path.expanduser('~')
    result = os.path.join(result, suffix)
    # we use 'isdir' instead of 'exists', because we want to
    # fail if there's a file with that name
    if not os.path.isdir(result):
        os.makedirs(result)
    return result


def path_to_cache_dir(path):
    """
    Convert an absolute path to a directory name for use in a cache.

    The algorithm used is:

    #. On Windows, any ``':'`` in the drive is replaced with ``'---'``.
    #. Any occurrence of ``os.sep`` is replaced with ``'--'``.
    #. ``'.cache'`` is appended.
    """
    d, p = os.path.splitdrive(os.path.abspath(path))
    if d:
        d = d.replace(':', '---')
    p = p.replace(os.sep, '--')
    return d + p + '.cache'


def ensure_slash(s):
    if not s.endswith('/'):
        return s + '/'
    return s


def parse_credentials(netloc):
    username = password = None
    if '@' in netloc:
        prefix, netloc = netloc.split('@', 1)
        if ':' not in prefix:
            username = prefix
        else:
            username, password = prefix.split(':', 1)
    return username, password, netloc


def get_process_umask():
    result = os.umask(0o22)
    os.umask(result)
    return result

def is_string_sequence(seq):
    result = True
    i = None
    for i, s in enumerate(seq):
        if not isinstance(s, string_types):
            result = False
            break
    assert i is not None
    return result

PROJECT_NAME_AND_VERSION = re.compile('([a-z0-9_]+([.-][a-z_][a-z0-9_]*)*)-'
                                      '([0-9][a-z0-9_.+-]*)', re.I)
PYTHON_VERSION = re.compile(r'-py(\d\.?\d?)$')


def split_filename(filename, project_name=None):
    """
    Extract name, version, python version from a filename (no extension)

    Return name, version, pyver or None
    """
    result = None
    pyver = None
    m = PYTHON_VERSION.search(filename)
    if m:
        pyver = m.group(1)
        filename = filename[:m.start()]
    if project_name and len(filename) > len(project_name) + 1:
        m = re.match(re.escape(project_name) + r'\b', filename)
        if m:
            n = m.end()
            result = filename[:n], filename[n + 1:], pyver
    if result is None:
        m = PROJECT_NAME_AND_VERSION.match(filename)
        if m:
            result = m.group(1), m.group(3), pyver
    return result

#
# Extended metadata functionality
#

def _get_external_data(url):
    result = {}
    try:
        # urlopen might fail if it runs into redirections,
        # because of Python issue #13696. Fixed in locators
        # using a custom redirect handler.
        resp = urlopen(url)
        headers = resp.info()
        if headers.get('Content-Type') != 'application/json':
            logger.debug('Unexpected response for JSON request')
        else:
            reader = codecs.getreader('utf-8')(resp)
            #data = reader.read().decode('utf-8')
            #result = json.loads(data)
            result = json.load(reader)
    except Exception as e:
        logger.exception('Failed to get external data for %s: %s', url, e)
    return result


def get_project_data(name):
    url = ('https://www.red-dove.com/pypi/projects/'
           '%s/%s/project.json' % (name[0].upper(), name))
    result = _get_external_data(url)
    return result

def get_package_data(dist):
    name, version = dist.name, dist.version
    url = ('https://www.red-dove.com/pypi/projects/'
           '%s/%s/package-%s.json' % (name[0].upper(), name, version))
    result = _get_external_data(url)
    if 'metadata' in result and dist.metadata:
        update_metadata(dist.metadata, result)
    return result

RENAMES = { # Temporary
    'classifiers': 'Classifier',
    'use_2to3': None,
    'use_2to3_fixers': None,
    'test_suite': None,
}

def update_metadata(metadata, pkginfo):
    # update dist's metadata from received package data
    assert metadata
    assert 'metadata' in pkginfo
    for k, v in pkginfo['metadata'].items():
        k = k.replace('-', '_')
        k = RENAMES.get(k, k)
        if k is not None:
            metadata[k] = v
    metadata.set_metadata_version()
    if 'requirements' in pkginfo:
        metadata.dependencies = pkginfo['requirements']


#
# Simple event pub/sub
#

class EventMixin(object):
    """
    A very simple publish/subscribe system.
    """
    def __init__(self):
        self._subscribers = {}

    def add(self, event, subscriber, append=True):
        """
        Add a subscriber for an event.

        :param event: The name of an event.
        :param subscriber: The subscriber to be added (and called when the
                           event is published).
        :param append: Whether to append or prepend the subscriber to an
                       existing subscriber list for the event.
        """
        subs = self._subscribers
        if event not in subs:
            subs[event] = deque([subscriber])
        else:
            sq = subs[event]
            if append:
                sq.append(subscriber)
            else:
                sq.appendleft(subscriber)

    def remove(self, event, subscriber):
        """
        Remove a subscriber for an event.

        :param event: The name of an event.
        :param subscriber: The subscriber to be removed.
        """
        subs = self._subscribers
        if event not in subs:
            raise ValueError('No subscribers: %r' % event)
        subs[event].remove(subscriber)

    def get_subscribers(self, event):
        """
        Return an iterator for the subscribers for an event.
        :param event: The event to return subscribers for.
        """
        return iter(self._subscribers.get(event, ()))

    def publish(self, event, *args, **kwargs):
        """
        Publish a event and return a list of values returned by its
        subscribers.

        :param event: The event to publish.
        :param args: The positional arguments to pass to the event's
                     subscribers.
        :param kwargs: The keyword arguments to pass to the event's
                       subscribers.
        """
        result = []
        for subscriber in self.get_subscribers(event):
            try:
                value = subscriber(event, *args, **kwargs)
            except Exception:
                logger.exception('Exception during event publication')
                value = None
            result.append(value)
        logger.debug('publish %s: args = %s, kwargs = %s, result = %s',
                     event, args, kwargs, result)
        return result

#
# Simple sequencing
#
class Sequencer(object):
    def __init__(self):
        self._preds = {}
        self._succs = {}
        self._nodes = set() # nodes with no preds/succs

    def add_node(self, node):
        self._nodes.add(node)

    def remove_node(self, node):
        self._nodes.remove(node)

    def add(self, pred, succ):
        assert pred != succ
        self._preds.setdefault(succ, set()).add(pred)
        self._succs.setdefault(pred, set()).add(succ)

    def remove(self, pred, succ):
        assert pred != succ
        try:
            preds = self._preds[succ]
            succs = self._succs[pred]
        except KeyError:
            raise ValueError('%r not a successor of anything' % succ)
        try:
            preds.remove(pred)
            succs.remove(succ)
        except KeyError:
            raise ValueError('%r not a successor of %r' % (succ, pred))

    def is_step(self, step):
        return (step in self._preds or step in self._succs or
                step in self._nodes)

    def get_steps(self, final):
        if not self.is_step(final):
            raise ValueError('Unknown: %r' % final)
        result = []
        todo = []
        seen = set()
        todo.append(final)
        while todo:
            step = todo.pop(0)
            if step in seen:
                # if a step was already seen,
                # move it to the end (so it will appear earlier
                # when reversed on return) ... but not for the
                # final step, as that would be confusing for
                # users
                if step != final:
                    result.remove(step)
                    result.append(step)
            else:
                seen.add(step)
                result.append(step)
                preds = self._preds.get(step, ())
                todo.extend(preds)
        return reversed(result)

    @property
    def strong_connections(self):
        #http://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
        index_counter = [0]
        stack = []
        lowlinks = {}
        index = {}
        result = []

        graph = self._succs

        def strongconnect(node):
            # set the depth index for this node to the smallest unused index
            index[node] = index_counter[0]
            lowlinks[node] = index_counter[0]
            index_counter[0] += 1
            stack.append(node)

            # Consider successors
            try:
                successors = graph[node]
            except Exception:
                successors = []
            for successor in successors:
                if successor not in lowlinks:
                    # Successor has not yet been visited
                    strongconnect(successor)
                    lowlinks[node] = min(lowlinks[node],lowlinks[successor])
                elif successor in stack:
                    # the successor is in the stack and hence in the current
                    # strongly connected component (SCC)
                    lowlinks[node] = min(lowlinks[node],index[successor])

            # If `node` is a root node, pop the stack and generate an SCC
            if lowlinks[node] == index[node]:
                connected_component = []

                while True:
                    successor = stack.pop()
                    connected_component.append(successor)
                    if successor == node: break
                component = tuple(connected_component)
                # storing the result
                result.append(component)

        for node in graph:
            if node not in lowlinks:
                strongconnect(node)

        return result

    @property
    def dot(self):
        result = ['digraph G {']
        for succ in self._preds:
            preds = self._preds[succ]
            for pred in preds:
                result.append('  %s -> %s;' % (pred, succ))
        for node in self._nodes:
            result.append('  %s;' % node)
        result.append('}')
        return '\n'.join(result)

#
# Unarchiving functionality for zip, tar, tgz, tbz, whl
#

ARCHIVE_EXTENSIONS = ('.tar.gz', '.tar.bz2', '.tar', '.zip',
                      '.tgz', '.tbz', '.whl')

def unarchive(archive_filename, dest_dir, format=None, check=True):

    def check_path(path):
        if not isinstance(path, text_type):
            path = path.decode('utf-8')
        p = os.path.abspath(os.path.join(dest_dir, path))
        if not p.startswith(dest_dir) or p[plen] != os.sep:
            raise ValueError('path outside destination: %r' % p)

    dest_dir = os.path.abspath(dest_dir)
    plen = len(dest_dir)
    archive = None
    if format is None:
        if archive_filename.endswith(('.zip', '.whl')):
            format = 'zip'
        elif archive_filename.endswith(('.tar.gz', '.tgz')):
            format = 'tgz'
            mode = 'r:gz'
        elif archive_filename.endswith(('.tar.bz2', '.tbz')):
            format = 'tbz'
            mode = 'r:bz2'
        elif archive_filename.endswith('.tar'):
            format = 'tar'
            mode = 'r'
        else:
            raise ValueError('Unknown format for %r' % archive_filename)
    try:
        if format == 'zip':
            archive = zipfile.ZipFile(archive_filename, 'r')
            if check:
                names = archive.namelist()
                for name in names:
                    check_path(name)
        else:
            archive = tarfile.open(archive_filename, mode)
            if check:
                names = archive.getnames()
                for name in names:
                    check_path(name)
        if format != 'zip' and sys.version_info[0] < 3:
            # See Python issue 17153. If the dest path contains Unicode,
            # tarfile extraction fails on Python 2.x if a member path name
            # contains non-ASCII characters - it leads to an implicit
            # bytes -> unicode conversion using ASCII to decode.
            for tarinfo in archive.getmembers():
                if not isinstance(tarinfo.name, text_type):
                    tarinfo.name = tarinfo.name.decode('utf-8')
        archive.extractall(dest_dir)

    finally:
        if archive:
            archive.close()


def zip_dir(directory):
    """zip a directory tree into a BytesIO object"""
    result = io.BytesIO()
    dlen = len(directory)
    with zipfile.ZipFile(result, "w") as zf:
        for root, dirs, files in os.walk(directory):
            for name in files:
                full = os.path.join(root, name)
                rel = root[dlen:]
                dest = os.path.join(rel, name)
                zf.write(full, dest)
    return result

#
# Simple progress bar
#

UNITS = ('', 'K', 'M', 'G','T','P')

class Progress(object):
    unknown = 'UNKNOWN'

    def __init__(self, minval=0, maxval=100):
        assert maxval is None or maxval >= minval
        self.min = self.cur = minval
        self.max = maxval
        self.started = None
        self.elapsed = 0
        self.done = False

    def update(self, curval):
        assert self.min <= curval
        assert self.max is None or curval <= self.max
        self.cur = curval
        now = time.time()
        if self.started is None:
            self.started = now
        else:
            self.elapsed = now - self.started

    def increment(self, incr):
        assert incr >= 0
        self.update(self.cur + incr)

    def start(self):
        self.update(self.min)
        return self

    def stop(self):
        if self.max is not None:
            self.update(self.max)
        self.done = True

    @property
    def maximum(self):
        return self.unknown if self.max is None else self.max

    @property
    def percentage(self):
        if self.done:
            result = '100 %'
        elif self.max is None:
            result = ' ?? %'
        else:
            v = 100.0 * (self.cur - self.min) / (self.max - self.min)
            result = '%3d %%' % v
        return result

    def format_duration(self, duration):
        if (duration <= 0) and self.max is None or self.cur == self.min:
            result = '??:??:??'
        #elif duration < 1:
        #    result = '--:--:--'
        else:
            result = time.strftime('%H:%M:%S', time.gmtime(duration))
        return result

    @property
    def ETA(self):
        if self.done:
            prefix = 'Done'
            t = self.elapsed
            #import pdb; pdb.set_trace()
        else:
            prefix = 'ETA '
            if self.max is None:
                t = -1
            elif self.elapsed == 0 or (self.cur == self.min):
                t = 0
            else:
                #import pdb; pdb.set_trace()
                t = float(self.max - self.min)
                t /= self.cur - self.min
                t = (t - 1) * self.elapsed
        return '%s: %s' % (prefix, self.format_duration(t))

    @property
    def speed(self):
        if self.elapsed == 0:
            result = 0.0
        else:
            result = (self.cur - self.min) / self.elapsed
        for unit in UNITS:
            if result < 1000:
                break
            result /= 1000.0
        return '%d %sB/s' % (result, unit)

#
# Glob functionality
#

RICH_GLOB = re.compile(r'\{([^}]*)\}')
_CHECK_RECURSIVE_GLOB = re.compile(r'[^/\\,{]\*\*|\*\*[^/\\,}]')
_CHECK_MISMATCH_SET = re.compile(r'^[^{]*\}|\{[^}]*$')


def iglob(path_glob):
    """Extended globbing function that supports ** and {opt1,opt2,opt3}."""
    if _CHECK_RECURSIVE_GLOB.search(path_glob):
        msg = """invalid glob %r: recursive glob "**" must be used alone"""
        raise ValueError(msg % path_glob)
    if _CHECK_MISMATCH_SET.search(path_glob):
        msg = """invalid glob %r: mismatching set marker '{' or '}'"""
        raise ValueError(msg % path_glob)
    return _iglob(path_glob)


def _iglob(path_glob):
    rich_path_glob = RICH_GLOB.split(path_glob, 1)
    if len(rich_path_glob) > 1:
        assert len(rich_path_glob) == 3, rich_path_glob
        prefix, set, suffix = rich_path_glob
        for item in set.split(','):
            for path in _iglob(''.join((prefix, item, suffix))):
                yield path
    else:
        if '**' not in path_glob:
            for item in std_iglob(path_glob):
                yield item
        else:
            prefix, radical = path_glob.split('**', 1)
            if prefix == '':
                prefix = '.'
            if radical == '':
                radical = '*'
            else:
                # we support both
                radical = radical.lstrip('/')
                radical = radical.lstrip('\\')
            for path, dir, files in os.walk(prefix):
                path = os.path.normpath(path)
                for file in _iglob(os.path.join(path, radical)):
                    yield file



#
# HTTPSConnection which verifies certificates/matches domains
#

class HTTPSConnection(httplib.HTTPSConnection):
    ca_certs = None # set this to the path to the certs file (.pem)
    check_domain = True # only used if ca_certs is not None

    # noinspection PyPropertyAccess
    def connect(self):
        sock = socket.create_connection((self.host, self.port), self.timeout)
        if getattr(self, '_tunnel_host', False):
            self.sock = sock
            self._tunnel()

        if not hasattr(ssl, 'SSLContext'):
            # For 2.x
            if self.ca_certs:
                cert_reqs = ssl.CERT_REQUIRED
            else:
                cert_reqs = ssl.CERT_NONE
            self.sock = ssl.wrap_socket(sock, self.key_file, self.cert_file,
                                        cert_reqs=cert_reqs,
                                        ssl_version=ssl.PROTOCOL_SSLv23,
                                        ca_certs=self.ca_certs)
        else:
            context = ssl.SSLContext(ssl.PROTOCOL_SSLv23)
            context.options |= ssl.OP_NO_SSLv2
            if self.cert_file:
                context.load_cert_chain(self.cert_file, self.key_file)
            kwargs = {}
            if self.ca_certs:
                context.verify_mode = ssl.CERT_REQUIRED
                context.load_verify_locations(cafile=self.ca_certs)
                if getattr(ssl, 'HAS_SNI', False):
                    kwargs['server_hostname'] = self.host
            self.sock = context.wrap_socket(sock, **kwargs)
        if self.ca_certs and self.check_domain:
            try:
                match_hostname(self.sock.getpeercert(), self.host)
            except CertificateError:
                self.sock.shutdown(socket.SHUT_RDWR)
                self.sock.close()
                raise

class HTTPSHandler(BaseHTTPSHandler):
    def __init__(self, ca_certs, check_domain=True):
        BaseHTTPSHandler.__init__(self)
        self.ca_certs = ca_certs
        self.check_domain = check_domain

    def _conn_maker(self, *args, **kwargs):
        """
        This is called to create a connection instance. Normally you'd
        pass a connection class to do_open, but it doesn't actually check for
        a class, and just expects a callable. As long as we behave just as a
        constructor would have, we should be OK. If it ever changes so that
        we *must* pass a class, we'll create an UnsafeHTTPSConnection class
        which just sets check_domain to False in the class definition, and
        choose which one to pass to do_open.
        """
        result = HTTPSConnection(*args, **kwargs)
        if self.ca_certs:
            result.ca_certs = self.ca_certs
            result.check_domain = self.check_domain
        return result

    def https_open(self, req):
        try:
            return self.do_open(self._conn_maker, req)
        except URLError as e:
            if 'certificate verify failed' in str(e.reason):
                raise CertificateError('Unable to verify server certificate '
                                       'for %s' % req.host)
            else:
                raise

#
# To prevent against mixing HTTP traffic with HTTPS (examples: A Man-In-The-
# Middle proxy using HTTP listens on port 443, or an index mistakenly serves
# HTML containing a http://xyz link when it should be https://xyz),
# you can use the following handler class, which does not allow HTTP traffic.
#
# It works by inheriting from HTTPHandler - so build_opener won't add a
# handler for HTTP itself.
#
class HTTPSOnlyHandler(HTTPSHandler, HTTPHandler):
    def http_open(self, req):
        raise URLError('Unexpected HTTP request on what should be a secure '
                       'connection: %s' % req)

#
# XML-RPC with timeouts
#

_ver_info = sys.version_info[:2]

if _ver_info == (2, 6):
    class HTTP(httplib.HTTP):
        def __init__(self, host='', port=None, **kwargs):
            if port == 0:   # 0 means use port 0, not the default port
                port = None
            self._setup(self._connection_class(host, port, **kwargs))


    class HTTPS(httplib.HTTPS):
        def __init__(self, host='', port=None, **kwargs):
            if port == 0:   # 0 means use port 0, not the default port
                port = None
            self._setup(self._connection_class(host, port, **kwargs))


class Transport(xmlrpclib.Transport):
    def __init__(self, timeout, use_datetime=0):
        self.timeout = timeout
        xmlrpclib.Transport.__init__(self, use_datetime)

    def make_connection(self, host):
        h, eh, x509 = self.get_host_info(host)
        if _ver_info == (2, 6):
            result = HTTP(h, timeout=self.timeout)
        else:
            if not self._connection or host != self._connection[0]:
                self._extra_headers = eh
                self._connection = host, httplib.HTTPConnection(h)
            result = self._connection[1]
        return result

class SafeTransport(xmlrpclib.SafeTransport):
    def __init__(self, timeout, use_datetime=0):
        self.timeout = timeout
        xmlrpclib.SafeTransport.__init__(self, use_datetime)

    def make_connection(self, host):
        h, eh, kwargs = self.get_host_info(host)
        if not kwargs:
            kwargs = {}
        kwargs['timeout'] = self.timeout
        if _ver_info == (2, 6):
            result = HTTPS(host, None, **kwargs)
        else:
            if not self._connection or host != self._connection[0]:
                self._extra_headers = eh
                self._connection = host, httplib.HTTPSConnection(h, None,
                                                                 **kwargs)
            result = self._connection[1]
        return result


class ServerProxy(xmlrpclib.ServerProxy):
    def __init__(self, uri, **kwargs):
        self.timeout = timeout = kwargs.pop('timeout', None)
        # The above classes only come into play if a timeout
        # is specified
        if timeout is not None:
            scheme, _ = splittype(uri)
            use_datetime = kwargs.get('use_datetime', 0)
            if scheme == 'https':
                tcls = SafeTransport
            else:
                tcls = Transport
            kwargs['transport'] = t = tcls(timeout, use_datetime=use_datetime)
            self.transport = t
        xmlrpclib.ServerProxy.__init__(self, uri, **kwargs)

#
# CSV functionality. This is provided because on 2.x, the csv module can't
# handle Unicode. However, we need to deal with Unicode in e.g. RECORD files.
#

def _csv_open(fn, mode, **kwargs):
    if sys.version_info[0] < 3:
        mode += 'b'
    else:
        kwargs['newline'] = ''
    return open(fn, mode, **kwargs)


class CSVBase(object):
    defaults = {
        'delimiter': str(','),      # The strs are used because we need native
        'quotechar': str('"'),      # str in the csv API (2.x won't take
        'lineterminator': str('\n') # Unicode)
    }

    def __enter__(self):
        return self

    def __exit__(self, *exc_info):
        self.stream.close()


class CSVReader(CSVBase):
    def __init__(self, fn, **kwargs):
        if 'stream' in kwargs:
            stream = kwargs['stream']
            if sys.version_info[0] >= 3:
                # needs to be a text stream
                stream = codecs.getreader('utf-8')(stream)
            self.stream = stream
        else:
            self.stream = _csv_open(fn, 'r')
        self.reader = csv.reader(self.stream, **self.defaults)

    def __iter__(self):
        return self

    def next(self):
        result = next(self.reader)
        if sys.version_info[0] < 3:
            for i, item in enumerate(result):
                if not isinstance(item, text_type):
                    result[i] = item.decode('utf-8')
        return result

    __next__ = next

class CSVWriter(CSVBase):
    def __init__(self, fn, **kwargs):
        self.stream = _csv_open(fn, 'w')
        self.writer = csv.writer(self.stream, **self.defaults)

    def writerow(self, row):
        if sys.version_info[0] < 3:
            r = []
            for item in row:
                if isinstance(item, text_type):
                    item = item.encode('utf-8')
                r.append(item)
            row = r
        self.writer.writerow(row)
