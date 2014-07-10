"""
Package resource API
--------------------

A resource is a logical file contained within a package, or a logical
subdirectory thereof.  The package resource API expects resource names
to have their path parts separated with ``/``, *not* whatever the local
path separator is.  Do not use os.path operations to manipulate resource
names being passed into the API.

The package resource API is designed to work with normal filesystem packages,
.egg files, and unpacked .egg files.  It can also work in a limited way with
.zip files and with custom PEP 302 loaders that support the ``get_data()``
method.
"""

import sys
import os
import time
import re
import imp
import zipfile
import zipimport
import warnings
import stat
import functools
import pkgutil
import token
import symbol
import operator
import platform
from pkgutil import get_importer

try:
    from urlparse import urlparse, urlunparse
except ImportError:
    from urllib.parse import urlparse, urlunparse

try:
    frozenset
except NameError:
    from sets import ImmutableSet as frozenset
try:
    basestring
    next = lambda o: o.next()
    from cStringIO import StringIO as BytesIO
except NameError:
    basestring = str
    from io import BytesIO
    def execfile(fn, globs=None, locs=None):
        if globs is None:
            globs = globals()
        if locs is None:
            locs = globs
        exec(compile(open(fn).read(), fn, 'exec'), globs, locs)

# capture these to bypass sandboxing
from os import utime
try:
    from os import mkdir, rename, unlink
    WRITE_SUPPORT = True
except ImportError:
    # no write support, probably under GAE
    WRITE_SUPPORT = False

from os import open as os_open
from os.path import isdir, split

# Avoid try/except due to potential problems with delayed import mechanisms.
if sys.version_info >= (3, 3) and sys.implementation.name == "cpython":
    import importlib._bootstrap as importlib_bootstrap
else:
    importlib_bootstrap = None

try:
    import parser
except ImportError:
    pass

def _bypass_ensure_directory(name, mode=0x1FF):  # 0777
    # Sandbox-bypassing version of ensure_directory()
    if not WRITE_SUPPORT:
        raise IOError('"os.mkdir" not supported on this platform.')
    dirname, filename = split(name)
    if dirname and filename and not isdir(dirname):
        _bypass_ensure_directory(dirname)
        mkdir(dirname, mode)


_state_vars = {}

def _declare_state(vartype, **kw):
    globals().update(kw)
    _state_vars.update(dict.fromkeys(kw, vartype))

def __getstate__():
    state = {}
    g = globals()
    for k, v in _state_vars.items():
        state[k] = g['_sget_'+v](g[k])
    return state

def __setstate__(state):
    g = globals()
    for k, v in state.items():
        g['_sset_'+_state_vars[k]](k, g[k], v)
    return state

def _sget_dict(val):
    return val.copy()

def _sset_dict(key, ob, state):
    ob.clear()
    ob.update(state)

def _sget_object(val):
    return val.__getstate__()

def _sset_object(key, ob, state):
    ob.__setstate__(state)

_sget_none = _sset_none = lambda *args: None


def get_supported_platform():
    """Return this platform's maximum compatible version.

    distutils.util.get_platform() normally reports the minimum version
    of Mac OS X that would be required to *use* extensions produced by
    distutils.  But what we want when checking compatibility is to know the
    version of Mac OS X that we are *running*.  To allow usage of packages that
    explicitly require a newer version of Mac OS X, we must also know the
    current version of the OS.

    If this condition occurs for any other platform with a version in its
    platform strings, this function should be extended accordingly.
    """
    plat = get_build_platform()
    m = macosVersionString.match(plat)
    if m is not None and sys.platform == "darwin":
        try:
            plat = 'macosx-%s-%s' % ('.'.join(_macosx_vers()[:2]), m.group(3))
        except ValueError:
            pass    # not Mac OS X
    return plat

__all__ = [
    # Basic resource access and distribution/entry point discovery
    'require', 'run_script', 'get_provider',  'get_distribution',
    'load_entry_point', 'get_entry_map', 'get_entry_info', 'iter_entry_points',
    'resource_string', 'resource_stream', 'resource_filename',
    'resource_listdir', 'resource_exists', 'resource_isdir',

    # Environmental control
    'declare_namespace', 'working_set', 'add_activation_listener',
    'find_distributions', 'set_extraction_path', 'cleanup_resources',
    'get_default_cache',

    # Primary implementation classes
    'Environment', 'WorkingSet', 'ResourceManager',
    'Distribution', 'Requirement', 'EntryPoint',

    # Exceptions
    'ResolutionError','VersionConflict','DistributionNotFound','UnknownExtra',
    'ExtractionError',

    # Parsing functions and string utilities
    'parse_requirements', 'parse_version', 'safe_name', 'safe_version',
    'get_platform', 'compatible_platforms', 'yield_lines', 'split_sections',
    'safe_extra', 'to_filename', 'invalid_marker', 'evaluate_marker',

    # filesystem utilities
    'ensure_directory', 'normalize_path',

    # Distribution "precedence" constants
    'EGG_DIST', 'BINARY_DIST', 'SOURCE_DIST', 'CHECKOUT_DIST', 'DEVELOP_DIST',

    # "Provider" interfaces, implementations, and registration/lookup APIs
    'IMetadataProvider', 'IResourceProvider', 'FileMetadata',
    'PathMetadata', 'EggMetadata', 'EmptyProvider', 'empty_provider',
    'NullProvider', 'EggProvider', 'DefaultProvider', 'ZipProvider',
    'register_finder', 'register_namespace_handler', 'register_loader_type',
    'fixup_namespace_packages', 'get_importer',

    # Deprecated/backward compatibility only
    'run_main', 'AvailableDistributions',
]

class ResolutionError(Exception):
    """Abstract base for dependency resolution errors"""
    def __repr__(self):
        return self.__class__.__name__+repr(self.args)

class VersionConflict(ResolutionError):
    """An already-installed version conflicts with the requested version"""

class DistributionNotFound(ResolutionError):
    """A requested distribution was not found"""

class UnknownExtra(ResolutionError):
    """Distribution doesn't have an "extra feature" of the given name"""
_provider_factories = {}

PY_MAJOR = sys.version[:3]
EGG_DIST = 3
BINARY_DIST = 2
SOURCE_DIST = 1
CHECKOUT_DIST = 0
DEVELOP_DIST = -1

def register_loader_type(loader_type, provider_factory):
    """Register `provider_factory` to make providers for `loader_type`

    `loader_type` is the type or class of a PEP 302 ``module.__loader__``,
    and `provider_factory` is a function that, passed a *module* object,
    returns an ``IResourceProvider`` for that module.
    """
    _provider_factories[loader_type] = provider_factory

def get_provider(moduleOrReq):
    """Return an IResourceProvider for the named module or requirement"""
    if isinstance(moduleOrReq,Requirement):
        return working_set.find(moduleOrReq) or require(str(moduleOrReq))[0]
    try:
        module = sys.modules[moduleOrReq]
    except KeyError:
        __import__(moduleOrReq)
        module = sys.modules[moduleOrReq]
    loader = getattr(module, '__loader__', None)
    return _find_adapter(_provider_factories, loader)(module)

def _macosx_vers(_cache=[]):
    if not _cache:
        import platform
        version = platform.mac_ver()[0]
        # fallback for MacPorts
        if version == '':
            import plistlib
            plist = '/System/Library/CoreServices/SystemVersion.plist'
            if os.path.exists(plist):
                if hasattr(plistlib, 'readPlist'):
                    plist_content = plistlib.readPlist(plist)
                    if 'ProductVersion' in plist_content:
                        version = plist_content['ProductVersion']

        _cache.append(version.split('.'))
    return _cache[0]

def _macosx_arch(machine):
    return {'PowerPC':'ppc', 'Power_Macintosh':'ppc'}.get(machine,machine)

def get_build_platform():
    """Return this platform's string for platform-specific distributions

    XXX Currently this is the same as ``distutils.util.get_platform()``, but it
    needs some hacks for Linux and Mac OS X.
    """
    try:
        # Python 2.7 or >=3.2
        from sysconfig import get_platform
    except ImportError:
        from distutils.util import get_platform

    plat = get_platform()
    if sys.platform == "darwin" and not plat.startswith('macosx-'):
        try:
            version = _macosx_vers()
            machine = os.uname()[4].replace(" ", "_")
            return "macosx-%d.%d-%s" % (int(version[0]), int(version[1]),
                _macosx_arch(machine))
        except ValueError:
            # if someone is running a non-Mac darwin system, this will fall
            # through to the default implementation
            pass
    return plat

macosVersionString = re.compile(r"macosx-(\d+)\.(\d+)-(.*)")
darwinVersionString = re.compile(r"darwin-(\d+)\.(\d+)\.(\d+)-(.*)")
get_platform = get_build_platform   # XXX backward compat


def compatible_platforms(provided,required):
    """Can code for the `provided` platform run on the `required` platform?

    Returns true if either platform is ``None``, or the platforms are equal.

    XXX Needs compatibility checks for Linux and other unixy OSes.
    """
    if provided is None or required is None or provided==required:
        return True     # easy case

    # Mac OS X special cases
    reqMac = macosVersionString.match(required)
    if reqMac:
        provMac = macosVersionString.match(provided)

        # is this a Mac package?
        if not provMac:
            # this is backwards compatibility for packages built before
            # setuptools 0.6. All packages built after this point will
            # use the new macosx designation.
            provDarwin = darwinVersionString.match(provided)
            if provDarwin:
                dversion = int(provDarwin.group(1))
                macosversion = "%s.%s" % (reqMac.group(1), reqMac.group(2))
                if dversion == 7 and macosversion >= "10.3" or \
                        dversion == 8 and macosversion >= "10.4":

                    #import warnings
                    #warnings.warn("Mac eggs should be rebuilt to "
                    #    "use the macosx designation instead of darwin.",
                    #    category=DeprecationWarning)
                    return True
            return False    # egg isn't macosx or legacy darwin

        # are they the same major version and machine type?
        if provMac.group(1) != reqMac.group(1) or \
                provMac.group(3) != reqMac.group(3):
            return False

        # is the required OS major update >= the provided one?
        if int(provMac.group(2)) > int(reqMac.group(2)):
            return False

        return True

    # XXX Linux and other platforms' special cases should go here
    return False


def run_script(dist_spec, script_name):
    """Locate distribution `dist_spec` and run its `script_name` script"""
    ns = sys._getframe(1).f_globals
    name = ns['__name__']
    ns.clear()
    ns['__name__'] = name
    require(dist_spec)[0].run_script(script_name, ns)

run_main = run_script   # backward compatibility

def get_distribution(dist):
    """Return a current distribution object for a Requirement or string"""
    if isinstance(dist,basestring): dist = Requirement.parse(dist)
    if isinstance(dist,Requirement): dist = get_provider(dist)
    if not isinstance(dist,Distribution):
        raise TypeError("Expected string, Requirement, or Distribution", dist)
    return dist

def load_entry_point(dist, group, name):
    """Return `name` entry point of `group` for `dist` or raise ImportError"""
    return get_distribution(dist).load_entry_point(group, name)

def get_entry_map(dist, group=None):
    """Return the entry point map for `group`, or the full entry map"""
    return get_distribution(dist).get_entry_map(group)

def get_entry_info(dist, group, name):
    """Return the EntryPoint object for `group`+`name`, or ``None``"""
    return get_distribution(dist).get_entry_info(group, name)


class IMetadataProvider:

    def has_metadata(name):
        """Does the package's distribution contain the named metadata?"""

    def get_metadata(name):
        """The named metadata resource as a string"""

    def get_metadata_lines(name):
        """Yield named metadata resource as list of non-blank non-comment lines

       Leading and trailing whitespace is stripped from each line, and lines
       with ``#`` as the first non-blank character are omitted."""

    def metadata_isdir(name):
        """Is the named metadata a directory?  (like ``os.path.isdir()``)"""

    def metadata_listdir(name):
        """List of metadata names in the directory (like ``os.listdir()``)"""

    def run_script(script_name, namespace):
        """Execute the named script in the supplied namespace dictionary"""


class IResourceProvider(IMetadataProvider):
    """An object that provides access to package resources"""

    def get_resource_filename(manager, resource_name):
        """Return a true filesystem path for `resource_name`

        `manager` must be an ``IResourceManager``"""

    def get_resource_stream(manager, resource_name):
        """Return a readable file-like object for `resource_name`

        `manager` must be an ``IResourceManager``"""

    def get_resource_string(manager, resource_name):
        """Return a string containing the contents of `resource_name`

        `manager` must be an ``IResourceManager``"""

    def has_resource(resource_name):
        """Does the package contain the named resource?"""

    def resource_isdir(resource_name):
        """Is the named resource a directory?  (like ``os.path.isdir()``)"""

    def resource_listdir(resource_name):
        """List of resource names in the directory (like ``os.listdir()``)"""


class WorkingSet(object):
    """A collection of active distributions on sys.path (or a similar list)"""

    def __init__(self, entries=None):
        """Create working set from list of path entries (default=sys.path)"""
        self.entries = []
        self.entry_keys = {}
        self.by_key = {}
        self.callbacks = []

        if entries is None:
            entries = sys.path

        for entry in entries:
            self.add_entry(entry)

    @classmethod
    def _build_master(cls):
        """
        Prepare the master working set.
        """
        ws = cls()
        try:
            from __main__ import __requires__
        except ImportError:
            # The main program does not list any requirements
            return ws

        # ensure the requirements are met
        try:
            ws.require(__requires__)
        except VersionConflict:
            return cls._build_from_requirements(__requires__)

        return ws

    @classmethod
    def _build_from_requirements(cls, req_spec):
        """
        Build a working set from a requirement spec. Rewrites sys.path.
        """
        # try it without defaults already on sys.path
        # by starting with an empty path
        ws = cls([])
        reqs = parse_requirements(req_spec)
        dists = ws.resolve(reqs, Environment())
        for dist in dists:
            ws.add(dist)

        # add any missing entries from sys.path
        for entry in sys.path:
            if entry not in ws.entries:
                ws.add_entry(entry)

        # then copy back to sys.path
        sys.path[:] = ws.entries
        return ws

    def add_entry(self, entry):
        """Add a path item to ``.entries``, finding any distributions on it

        ``find_distributions(entry, True)`` is used to find distributions
        corresponding to the path entry, and they are added.  `entry` is
        always appended to ``.entries``, even if it is already present.
        (This is because ``sys.path`` can contain the same value more than
        once, and the ``.entries`` of the ``sys.path`` WorkingSet should always
        equal ``sys.path``.)
        """
        self.entry_keys.setdefault(entry, [])
        self.entries.append(entry)
        for dist in find_distributions(entry, True):
            self.add(dist, entry, False)

    def __contains__(self,dist):
        """True if `dist` is the active distribution for its project"""
        return self.by_key.get(dist.key) == dist

    def find(self, req):
        """Find a distribution matching requirement `req`

        If there is an active distribution for the requested project, this
        returns it as long as it meets the version requirement specified by
        `req`.  But, if there is an active distribution for the project and it
        does *not* meet the `req` requirement, ``VersionConflict`` is raised.
        If there is no active distribution for the requested project, ``None``
        is returned.
        """
        dist = self.by_key.get(req.key)
        if dist is not None and dist not in req:
            raise VersionConflict(dist,req)     # XXX add more info
        else:
            return dist

    def iter_entry_points(self, group, name=None):
        """Yield entry point objects from `group` matching `name`

        If `name` is None, yields all entry points in `group` from all
        distributions in the working set, otherwise only ones matching
        both `group` and `name` are yielded (in distribution order).
        """
        for dist in self:
            entries = dist.get_entry_map(group)
            if name is None:
                for ep in entries.values():
                    yield ep
            elif name in entries:
                yield entries[name]

    def run_script(self, requires, script_name):
        """Locate distribution for `requires` and run `script_name` script"""
        ns = sys._getframe(1).f_globals
        name = ns['__name__']
        ns.clear()
        ns['__name__'] = name
        self.require(requires)[0].run_script(script_name, ns)

    def __iter__(self):
        """Yield distributions for non-duplicate projects in the working set

        The yield order is the order in which the items' path entries were
        added to the working set.
        """
        seen = {}
        for item in self.entries:
            if item not in self.entry_keys:
                # workaround a cache issue
                continue

            for key in self.entry_keys[item]:
                if key not in seen:
                    seen[key]=1
                    yield self.by_key[key]

    def add(self, dist, entry=None, insert=True, replace=False):
        """Add `dist` to working set, associated with `entry`

        If `entry` is unspecified, it defaults to the ``.location`` of `dist`.
        On exit from this routine, `entry` is added to the end of the working
        set's ``.entries`` (if it wasn't already present).

        `dist` is only added to the working set if it's for a project that
        doesn't already have a distribution in the set, unless `replace=True`.
        If it's added, any callbacks registered with the ``subscribe()`` method
        will be called.
        """
        if insert:
            dist.insert_on(self.entries, entry)

        if entry is None:
            entry = dist.location
        keys = self.entry_keys.setdefault(entry,[])
        keys2 = self.entry_keys.setdefault(dist.location,[])
        if not replace and dist.key in self.by_key:
            return      # ignore hidden distros

        self.by_key[dist.key] = dist
        if dist.key not in keys:
            keys.append(dist.key)
        if dist.key not in keys2:
            keys2.append(dist.key)
        self._added_new(dist)

    def resolve(self, requirements, env=None, installer=None,
            replace_conflicting=False):
        """List all distributions needed to (recursively) meet `requirements`

        `requirements` must be a sequence of ``Requirement`` objects.  `env`,
        if supplied, should be an ``Environment`` instance.  If
        not supplied, it defaults to all distributions available within any
        entry or distribution in the working set.  `installer`, if supplied,
        will be invoked with each requirement that cannot be met by an
        already-installed distribution; it should return a ``Distribution`` or
        ``None``.

        Unless `replace_conflicting=True`, raises a VersionConflict exception if
        any requirements are found on the path that have the correct name but
        the wrong version.  Otherwise, if an `installer` is supplied it will be
        invoked to obtain the correct version of the requirement and activate
        it.
        """

        requirements = list(requirements)[::-1]  # set up the stack
        processed = {}  # set of processed requirements
        best = {}  # key -> dist
        to_activate = []

        while requirements:
            req = requirements.pop(0)   # process dependencies breadth-first
            if req in processed:
                # Ignore cyclic or redundant dependencies
                continue
            dist = best.get(req.key)
            if dist is None:
                # Find the best distribution and add it to the map
                dist = self.by_key.get(req.key)
                if dist is None or (dist not in req and replace_conflicting):
                    ws = self
                    if env is None:
                        if dist is None:
                            env = Environment(self.entries)
                        else:
                            # Use an empty environment and workingset to avoid
                            # any further conflicts with the conflicting
                            # distribution
                            env = Environment([])
                            ws = WorkingSet([])
                    dist = best[req.key] = env.best_match(req, ws, installer)
                    if dist is None:
                        #msg = ("The '%s' distribution was not found on this "
                        #       "system, and is required by this application.")
                        #raise DistributionNotFound(msg % req)

                        # unfortunately, zc.buildout uses a str(err)
                        # to get the name of the distribution here..
                        raise DistributionNotFound(req)
                to_activate.append(dist)
            if dist not in req:
                # Oops, the "best" so far conflicts with a dependency
                raise VersionConflict(dist,req) # XXX put more info here
            requirements.extend(dist.requires(req.extras)[::-1])
            processed[req] = True

        return to_activate    # return list of distros to activate

    def find_plugins(self, plugin_env, full_env=None, installer=None,
            fallback=True):
        """Find all activatable distributions in `plugin_env`

        Example usage::

            distributions, errors = working_set.find_plugins(
                Environment(plugin_dirlist)
            )
            map(working_set.add, distributions)  # add plugins+libs to sys.path
            print 'Could not load', errors        # display errors

        The `plugin_env` should be an ``Environment`` instance that contains
        only distributions that are in the project's "plugin directory" or
        directories. The `full_env`, if supplied, should be an ``Environment``
        contains all currently-available distributions.  If `full_env` is not
        supplied, one is created automatically from the ``WorkingSet`` this
        method is called on, which will typically mean that every directory on
        ``sys.path`` will be scanned for distributions.

        `installer` is a standard installer callback as used by the
        ``resolve()`` method. The `fallback` flag indicates whether we should
        attempt to resolve older versions of a plugin if the newest version
        cannot be resolved.

        This method returns a 2-tuple: (`distributions`, `error_info`), where
        `distributions` is a list of the distributions found in `plugin_env`
        that were loadable, along with any other distributions that are needed
        to resolve their dependencies.  `error_info` is a dictionary mapping
        unloadable plugin distributions to an exception instance describing the
        error that occurred. Usually this will be a ``DistributionNotFound`` or
        ``VersionConflict`` instance.
        """

        plugin_projects = list(plugin_env)
        plugin_projects.sort()  # scan project names in alphabetic order

        error_info = {}
        distributions = {}

        if full_env is None:
            env = Environment(self.entries)
            env += plugin_env
        else:
            env = full_env + plugin_env

        shadow_set = self.__class__([])
        list(map(shadow_set.add, self))   # put all our entries in shadow_set

        for project_name in plugin_projects:

            for dist in plugin_env[project_name]:

                req = [dist.as_requirement()]

                try:
                    resolvees = shadow_set.resolve(req, env, installer)

                except ResolutionError:
                    v = sys.exc_info()[1]
                    error_info[dist] = v    # save error info
                    if fallback:
                        continue    # try the next older version of project
                    else:
                        break       # give up on this project, keep going

                else:
                    list(map(shadow_set.add, resolvees))
                    distributions.update(dict.fromkeys(resolvees))

                    # success, no need to try any more versions of this project
                    break

        distributions = list(distributions)
        distributions.sort()

        return distributions, error_info

    def require(self, *requirements):
        """Ensure that distributions matching `requirements` are activated

        `requirements` must be a string or a (possibly-nested) sequence
        thereof, specifying the distributions and versions required.  The
        return value is a sequence of the distributions that needed to be
        activated to fulfill the requirements; all relevant distributions are
        included, even if they were already activated in this working set.
        """
        needed = self.resolve(parse_requirements(requirements))

        for dist in needed:
            self.add(dist)

        return needed

    def subscribe(self, callback):
        """Invoke `callback` for all distributions (including existing ones)"""
        if callback in self.callbacks:
            return
        self.callbacks.append(callback)
        for dist in self:
            callback(dist)

    def _added_new(self, dist):
        for callback in self.callbacks:
            callback(dist)

    def __getstate__(self):
        return (
            self.entries[:], self.entry_keys.copy(), self.by_key.copy(),
            self.callbacks[:]
        )

    def __setstate__(self, e_k_b_c):
        entries, keys, by_key, callbacks = e_k_b_c
        self.entries = entries[:]
        self.entry_keys = keys.copy()
        self.by_key = by_key.copy()
        self.callbacks = callbacks[:]


class Environment(object):
    """Searchable snapshot of distributions on a search path"""

    def __init__(self, search_path=None, platform=get_supported_platform(), python=PY_MAJOR):
        """Snapshot distributions available on a search path

        Any distributions found on `search_path` are added to the environment.
        `search_path` should be a sequence of ``sys.path`` items.  If not
        supplied, ``sys.path`` is used.

        `platform` is an optional string specifying the name of the platform
        that platform-specific distributions must be compatible with.  If
        unspecified, it defaults to the current platform.  `python` is an
        optional string naming the desired version of Python (e.g. ``'3.3'``);
        it defaults to the current version.

        You may explicitly set `platform` (and/or `python`) to ``None`` if you
        wish to map *all* distributions, not just those compatible with the
        running platform or Python version.
        """
        self._distmap = {}
        self._cache = {}
        self.platform = platform
        self.python = python
        self.scan(search_path)

    def can_add(self, dist):
        """Is distribution `dist` acceptable for this environment?

        The distribution must match the platform and python version
        requirements specified when this environment was created, or False
        is returned.
        """
        return (self.python is None or dist.py_version is None
            or dist.py_version==self.python) \
            and compatible_platforms(dist.platform,self.platform)

    def remove(self, dist):
        """Remove `dist` from the environment"""
        self._distmap[dist.key].remove(dist)

    def scan(self, search_path=None):
        """Scan `search_path` for distributions usable in this environment

        Any distributions found are added to the environment.
        `search_path` should be a sequence of ``sys.path`` items.  If not
        supplied, ``sys.path`` is used.  Only distributions conforming to
        the platform/python version defined at initialization are added.
        """
        if search_path is None:
            search_path = sys.path

        for item in search_path:
            for dist in find_distributions(item):
                self.add(dist)

    def __getitem__(self,project_name):
        """Return a newest-to-oldest list of distributions for `project_name`
        """
        try:
            return self._cache[project_name]
        except KeyError:
            project_name = project_name.lower()
            if project_name not in self._distmap:
                return []

        if project_name not in self._cache:
            dists = self._cache[project_name] = self._distmap[project_name]
            _sort_dists(dists)

        return self._cache[project_name]

    def add(self,dist):
        """Add `dist` if we ``can_add()`` it and it isn't already added"""
        if self.can_add(dist) and dist.has_version():
            dists = self._distmap.setdefault(dist.key,[])
            if dist not in dists:
                dists.append(dist)
                if dist.key in self._cache:
                    _sort_dists(self._cache[dist.key])

    def best_match(self, req, working_set, installer=None):
        """Find distribution best matching `req` and usable on `working_set`

        This calls the ``find(req)`` method of the `working_set` to see if a
        suitable distribution is already active.  (This may raise
        ``VersionConflict`` if an unsuitable version of the project is already
        active in the specified `working_set`.)  If a suitable distribution
        isn't active, this method returns the newest distribution in the
        environment that meets the ``Requirement`` in `req`.  If no suitable
        distribution is found, and `installer` is supplied, then the result of
        calling the environment's ``obtain(req, installer)`` method will be
        returned.
        """
        dist = working_set.find(req)
        if dist is not None:
            return dist
        for dist in self[req.key]:
            if dist in req:
                return dist
        return self.obtain(req, installer) # try and download/install

    def obtain(self, requirement, installer=None):
        """Obtain a distribution matching `requirement` (e.g. via download)

        Obtain a distro that matches requirement (e.g. via download).  In the
        base ``Environment`` class, this routine just returns
        ``installer(requirement)``, unless `installer` is None, in which case
        None is returned instead.  This method is a hook that allows subclasses
        to attempt other ways of obtaining a distribution before falling back
        to the `installer` argument."""
        if installer is not None:
            return installer(requirement)

    def __iter__(self):
        """Yield the unique project names of the available distributions"""
        for key in self._distmap.keys():
            if self[key]: yield key

    def __iadd__(self, other):
        """In-place addition of a distribution or environment"""
        if isinstance(other,Distribution):
            self.add(other)
        elif isinstance(other,Environment):
            for project in other:
                for dist in other[project]:
                    self.add(dist)
        else:
            raise TypeError("Can't add %r to environment" % (other,))
        return self

    def __add__(self, other):
        """Add an environment or distribution to an environment"""
        new = self.__class__([], platform=None, python=None)
        for env in self, other:
            new += env
        return new


AvailableDistributions = Environment    # XXX backward compatibility


class ExtractionError(RuntimeError):
    """An error occurred extracting a resource

    The following attributes are available from instances of this exception:

    manager
        The resource manager that raised this exception

    cache_path
        The base directory for resource extraction

    original_error
        The exception instance that caused extraction to fail
    """


class ResourceManager:
    """Manage resource extraction and packages"""
    extraction_path = None

    def __init__(self):
        self.cached_files = {}

    def resource_exists(self, package_or_requirement, resource_name):
        """Does the named resource exist?"""
        return get_provider(package_or_requirement).has_resource(resource_name)

    def resource_isdir(self, package_or_requirement, resource_name):
        """Is the named resource an existing directory?"""
        return get_provider(package_or_requirement).resource_isdir(
            resource_name
        )

    def resource_filename(self, package_or_requirement, resource_name):
        """Return a true filesystem path for specified resource"""
        return get_provider(package_or_requirement).get_resource_filename(
            self, resource_name
        )

    def resource_stream(self, package_or_requirement, resource_name):
        """Return a readable file-like object for specified resource"""
        return get_provider(package_or_requirement).get_resource_stream(
            self, resource_name
        )

    def resource_string(self, package_or_requirement, resource_name):
        """Return specified resource as a string"""
        return get_provider(package_or_requirement).get_resource_string(
            self, resource_name
        )

    def resource_listdir(self, package_or_requirement, resource_name):
        """List the contents of the named resource directory"""
        return get_provider(package_or_requirement).resource_listdir(
            resource_name
        )

    def extraction_error(self):
        """Give an error message for problems extracting file(s)"""

        old_exc = sys.exc_info()[1]
        cache_path = self.extraction_path or get_default_cache()

        err = ExtractionError("""Can't extract file(s) to egg cache

The following error occurred while trying to extract file(s) to the Python egg
cache:

  %s

The Python egg cache directory is currently set to:

  %s

Perhaps your account does not have write access to this directory?  You can
change the cache directory by setting the PYTHON_EGG_CACHE environment
variable to point to an accessible directory.
""" % (old_exc, cache_path)
        )
        err.manager = self
        err.cache_path = cache_path
        err.original_error = old_exc
        raise err

    def get_cache_path(self, archive_name, names=()):
        """Return absolute location in cache for `archive_name` and `names`

        The parent directory of the resulting path will be created if it does
        not already exist.  `archive_name` should be the base filename of the
        enclosing egg (which may not be the name of the enclosing zipfile!),
        including its ".egg" extension.  `names`, if provided, should be a
        sequence of path name parts "under" the egg's extraction location.

        This method should only be called by resource providers that need to
        obtain an extraction location, and only for names they intend to
        extract, as it tracks the generated names for possible cleanup later.
        """
        extract_path = self.extraction_path or get_default_cache()
        target_path = os.path.join(extract_path, archive_name+'-tmp', *names)
        try:
            _bypass_ensure_directory(target_path)
        except:
            self.extraction_error()

        self._warn_unsafe_extraction_path(extract_path)

        self.cached_files[target_path] = 1
        return target_path

    @staticmethod
    def _warn_unsafe_extraction_path(path):
        """
        If the default extraction path is overridden and set to an insecure
        location, such as /tmp, it opens up an opportunity for an attacker to
        replace an extracted file with an unauthorized payload. Warn the user
        if a known insecure location is used.

        See Distribute #375 for more details.
        """
        if os.name == 'nt' and not path.startswith(os.environ['windir']):
            # On Windows, permissions are generally restrictive by default
            #  and temp directories are not writable by other users, so
            #  bypass the warning.
            return
        mode = os.stat(path).st_mode
        if mode & stat.S_IWOTH or mode & stat.S_IWGRP:
            msg = ("%s is writable by group/others and vulnerable to attack "
                "when "
                "used with get_resource_filename. Consider a more secure "
                "location (set with .set_extraction_path or the "
                "PYTHON_EGG_CACHE environment variable)." % path)
            warnings.warn(msg, UserWarning)

    def postprocess(self, tempname, filename):
        """Perform any platform-specific postprocessing of `tempname`

        This is where Mac header rewrites should be done; other platforms don't
        have anything special they should do.

        Resource providers should call this method ONLY after successfully
        extracting a compressed resource.  They must NOT call it on resources
        that are already in the filesystem.

        `tempname` is the current (temporary) name of the file, and `filename`
        is the name it will be renamed to by the caller after this routine
        returns.
        """

        if os.name == 'posix':
            # Make the resource executable
            mode = ((os.stat(tempname).st_mode) | 0x16D) & 0xFFF # 0555, 07777
            os.chmod(tempname, mode)

    def set_extraction_path(self, path):
        """Set the base path where resources will be extracted to, if needed.

        If you do not call this routine before any extractions take place, the
        path defaults to the return value of ``get_default_cache()``.  (Which
        is based on the ``PYTHON_EGG_CACHE`` environment variable, with various
        platform-specific fallbacks.  See that routine's documentation for more
        details.)

        Resources are extracted to subdirectories of this path based upon
        information given by the ``IResourceProvider``.  You may set this to a
        temporary directory, but then you must call ``cleanup_resources()`` to
        delete the extracted files when done.  There is no guarantee that
        ``cleanup_resources()`` will be able to remove all extracted files.

        (Note: you may not change the extraction path for a given resource
        manager once resources have been extracted, unless you first call
        ``cleanup_resources()``.)
        """
        if self.cached_files:
            raise ValueError(
                "Can't change extraction path, files already extracted"
            )

        self.extraction_path = path

    def cleanup_resources(self, force=False):
        """
        Delete all extracted resource files and directories, returning a list
        of the file and directory names that could not be successfully removed.
        This function does not have any concurrency protection, so it should
        generally only be called when the extraction path is a temporary
        directory exclusive to a single process.  This method is not
        automatically called; you must call it explicitly or register it as an
        ``atexit`` function if you wish to ensure cleanup of a temporary
        directory used for extractions.
        """
        # XXX

def get_default_cache():
    """Determine the default cache location

    This returns the ``PYTHON_EGG_CACHE`` environment variable, if set.
    Otherwise, on Windows, it returns a "Python-Eggs" subdirectory of the
    "Application Data" directory.  On all other systems, it's "~/.python-eggs".
    """
    try:
        return os.environ['PYTHON_EGG_CACHE']
    except KeyError:
        pass

    if os.name!='nt':
        return os.path.expanduser('~/.python-eggs')

    app_data = 'Application Data'   # XXX this may be locale-specific!
    app_homes = [
        (('APPDATA',), None),       # best option, should be locale-safe
        (('USERPROFILE',), app_data),
        (('HOMEDRIVE','HOMEPATH'), app_data),
        (('HOMEPATH',), app_data),
        (('HOME',), None),
        (('WINDIR',), app_data),    # 95/98/ME
    ]

    for keys, subdir in app_homes:
        dirname = ''
        for key in keys:
            if key in os.environ:
                dirname = os.path.join(dirname, os.environ[key])
            else:
                break
        else:
            if subdir:
                dirname = os.path.join(dirname,subdir)
            return os.path.join(dirname, 'Python-Eggs')
    else:
        raise RuntimeError(
            "Please set the PYTHON_EGG_CACHE enviroment variable"
        )

def safe_name(name):
    """Convert an arbitrary string to a standard distribution name

    Any runs of non-alphanumeric/. characters are replaced with a single '-'.
    """
    return re.sub('[^A-Za-z0-9.]+', '-', name)


def safe_version(version):
    """Convert an arbitrary string to a standard version string

    Spaces become dots, and all other non-alphanumeric characters become
    dashes, with runs of multiple dashes condensed to a single dash.
    """
    version = version.replace(' ','.')
    return re.sub('[^A-Za-z0-9.]+', '-', version)


def safe_extra(extra):
    """Convert an arbitrary string to a standard 'extra' name

    Any runs of non-alphanumeric characters are replaced with a single '_',
    and the result is always lowercased.
    """
    return re.sub('[^A-Za-z0-9.]+', '_', extra).lower()


def to_filename(name):
    """Convert a project or version name to its filename-escaped form

    Any '-' characters are currently replaced with '_'.
    """
    return name.replace('-','_')


class MarkerEvaluation(object):
    values = {
        'os_name': lambda: os.name,
        'sys_platform': lambda: sys.platform,
        'python_full_version': lambda: sys.version.split()[0],
        'python_version': lambda:'%s.%s' % (sys.version_info[0], sys.version_info[1]),
        'platform_version': platform.version,
        'platform_machine': platform.machine,
        'python_implementation': platform.python_implementation,
    }

    @classmethod
    def is_invalid_marker(cls, text):
        """
        Validate text as a PEP 426 environment marker; return an exception
        if invalid or False otherwise.
        """
        try:
            cls.evaluate_marker(text)
        except SyntaxError:
            return cls.normalize_exception(sys.exc_info()[1])
        return False

    @staticmethod
    def normalize_exception(exc):
        """
        Given a SyntaxError from a marker evaluation, normalize the error message:
         - Remove indications of filename and line number.
         - Replace platform-specific error messages with standard error messages.
        """
        subs = {
            'unexpected EOF while parsing': 'invalid syntax',
            'parenthesis is never closed': 'invalid syntax',
        }
        exc.filename = None
        exc.lineno = None
        exc.msg = subs.get(exc.msg, exc.msg)
        return exc

    @classmethod
    def and_test(cls, nodelist):
        # MUST NOT short-circuit evaluation, or invalid syntax can be skipped!
        return functools.reduce(operator.and_, [cls.interpret(nodelist[i]) for i in range(1,len(nodelist),2)])

    @classmethod
    def test(cls, nodelist):
        # MUST NOT short-circuit evaluation, or invalid syntax can be skipped!
        return functools.reduce(operator.or_, [cls.interpret(nodelist[i]) for i in range(1,len(nodelist),2)])

    @classmethod
    def atom(cls, nodelist):
        t = nodelist[1][0]
        if t == token.LPAR:
            if nodelist[2][0] == token.RPAR:
                raise SyntaxError("Empty parentheses")
            return cls.interpret(nodelist[2])
        raise SyntaxError("Language feature not supported in environment markers")

    @classmethod
    def comparison(cls, nodelist):
        if len(nodelist)>4:
            raise SyntaxError("Chained comparison not allowed in environment markers")
        comp = nodelist[2][1]
        cop = comp[1]
        if comp[0] == token.NAME:
            if len(nodelist[2]) == 3:
                if cop == 'not':
                    cop = 'not in'
                else:
                    cop = 'is not'
        try:
            cop = cls.get_op(cop)
        except KeyError:
            raise SyntaxError(repr(cop)+" operator not allowed in environment markers")
        return cop(cls.evaluate(nodelist[1]), cls.evaluate(nodelist[3]))

    @classmethod
    def get_op(cls, op):
        ops = {
            symbol.test: cls.test,
            symbol.and_test: cls.and_test,
            symbol.atom: cls.atom,
            symbol.comparison: cls.comparison,
            'not in': lambda x, y: x not in y,
            'in': lambda x, y: x in y,
            '==': operator.eq,
            '!=': operator.ne,
        }
        if hasattr(symbol, 'or_test'):
            ops[symbol.or_test] = cls.test
        return ops[op]

    @classmethod
    def evaluate_marker(cls, text, extra=None):
        """
        Evaluate a PEP 426 environment marker on CPython 2.4+.
        Return a boolean indicating the marker result in this environment.
        Raise SyntaxError if marker is invalid.

        This implementation uses the 'parser' module, which is not implemented on
        Jython and has been superseded by the 'ast' module in Python 2.6 and
        later.
        """
        return cls.interpret(parser.expr(text).totuple(1)[1])

    @classmethod
    def _markerlib_evaluate(cls, text):
        """
        Evaluate a PEP 426 environment marker using markerlib.
        Return a boolean indicating the marker result in this environment.
        Raise SyntaxError if marker is invalid.
        """
        from pip._vendor import _markerlib
        # markerlib implements Metadata 1.2 (PEP 345) environment markers.
        # Translate the variables to Metadata 2.0 (PEP 426).
        env = _markerlib.default_environment()
        for key in env.keys():
            new_key = key.replace('.', '_')
            env[new_key] = env.pop(key)
        try:
            result = _markerlib.interpret(text, env)
        except NameError:
            e = sys.exc_info()[1]
            raise SyntaxError(e.args[0])
        return result

    if 'parser' not in globals():
        # Fall back to less-complete _markerlib implementation if 'parser' module
        # is not available.
        evaluate_marker = _markerlib_evaluate

    @classmethod
    def interpret(cls, nodelist):
        while len(nodelist)==2: nodelist = nodelist[1]
        try:
            op = cls.get_op(nodelist[0])
        except KeyError:
            raise SyntaxError("Comparison or logical expression expected")
        return op(nodelist)

    @classmethod
    def evaluate(cls, nodelist):
        while len(nodelist)==2: nodelist = nodelist[1]
        kind = nodelist[0]
        name = nodelist[1]
        if kind==token.NAME:
            try:
                op = cls.values[name]
            except KeyError:
                raise SyntaxError("Unknown name %r" % name)
            return op()
        if kind==token.STRING:
            s = nodelist[1]
            if s[:1] not in "'\"" or s.startswith('"""') or s.startswith("'''") \
                    or '\\' in s:
                raise SyntaxError(
                    "Only plain strings allowed in environment markers")
            return s[1:-1]
        raise SyntaxError("Language feature not supported in environment markers")

invalid_marker = MarkerEvaluation.is_invalid_marker
evaluate_marker = MarkerEvaluation.evaluate_marker

class NullProvider:
    """Try to implement resources and metadata for arbitrary PEP 302 loaders"""

    egg_name = None
    egg_info = None
    loader = None

    def __init__(self, module):
        self.loader = getattr(module, '__loader__', None)
        self.module_path = os.path.dirname(getattr(module, '__file__', ''))

    def get_resource_filename(self, manager, resource_name):
        return self._fn(self.module_path, resource_name)

    def get_resource_stream(self, manager, resource_name):
        return BytesIO(self.get_resource_string(manager, resource_name))

    def get_resource_string(self, manager, resource_name):
        return self._get(self._fn(self.module_path, resource_name))

    def has_resource(self, resource_name):
        return self._has(self._fn(self.module_path, resource_name))

    def has_metadata(self, name):
        return self.egg_info and self._has(self._fn(self.egg_info,name))

    if sys.version_info <= (3,):
        def get_metadata(self, name):
            if not self.egg_info:
                return ""
            return self._get(self._fn(self.egg_info,name))
    else:
        def get_metadata(self, name):
            if not self.egg_info:
                return ""
            return self._get(self._fn(self.egg_info,name)).decode("utf-8")

    def get_metadata_lines(self, name):
        return yield_lines(self.get_metadata(name))

    def resource_isdir(self,resource_name):
        return self._isdir(self._fn(self.module_path, resource_name))

    def metadata_isdir(self,name):
        return self.egg_info and self._isdir(self._fn(self.egg_info,name))

    def resource_listdir(self,resource_name):
        return self._listdir(self._fn(self.module_path,resource_name))

    def metadata_listdir(self,name):
        if self.egg_info:
            return self._listdir(self._fn(self.egg_info,name))
        return []

    def run_script(self,script_name,namespace):
        script = 'scripts/'+script_name
        if not self.has_metadata(script):
            raise ResolutionError("No script named %r" % script_name)
        script_text = self.get_metadata(script).replace('\r\n','\n')
        script_text = script_text.replace('\r','\n')
        script_filename = self._fn(self.egg_info,script)
        namespace['__file__'] = script_filename
        if os.path.exists(script_filename):
            execfile(script_filename, namespace, namespace)
        else:
            from linecache import cache
            cache[script_filename] = (
                len(script_text), 0, script_text.split('\n'), script_filename
            )
            script_code = compile(script_text,script_filename,'exec')
            exec(script_code, namespace, namespace)

    def _has(self, path):
        raise NotImplementedError(
            "Can't perform this operation for unregistered loader type"
        )

    def _isdir(self, path):
        raise NotImplementedError(
            "Can't perform this operation for unregistered loader type"
        )

    def _listdir(self, path):
        raise NotImplementedError(
            "Can't perform this operation for unregistered loader type"
        )

    def _fn(self, base, resource_name):
        if resource_name:
            return os.path.join(base, *resource_name.split('/'))
        return base

    def _get(self, path):
        if hasattr(self.loader, 'get_data'):
            return self.loader.get_data(path)
        raise NotImplementedError(
            "Can't perform this operation for loaders without 'get_data()'"
        )

register_loader_type(object, NullProvider)


class EggProvider(NullProvider):
    """Provider based on a virtual filesystem"""

    def __init__(self,module):
        NullProvider.__init__(self,module)
        self._setup_prefix()

    def _setup_prefix(self):
        # we assume here that our metadata may be nested inside a "basket"
        # of multiple eggs; that's why we use module_path instead of .archive
        path = self.module_path
        old = None
        while path!=old:
            if path.lower().endswith('.egg'):
                self.egg_name = os.path.basename(path)
                self.egg_info = os.path.join(path, 'EGG-INFO')
                self.egg_root = path
                break
            old = path
            path, base = os.path.split(path)

class DefaultProvider(EggProvider):
    """Provides access to package resources in the filesystem"""

    def _has(self, path):
        return os.path.exists(path)

    def _isdir(self,path):
        return os.path.isdir(path)

    def _listdir(self,path):
        return os.listdir(path)

    def get_resource_stream(self, manager, resource_name):
        return open(self._fn(self.module_path, resource_name), 'rb')

    def _get(self, path):
        stream = open(path, 'rb')
        try:
            return stream.read()
        finally:
            stream.close()

register_loader_type(type(None), DefaultProvider)

if importlib_bootstrap is not None:
    register_loader_type(importlib_bootstrap.SourceFileLoader, DefaultProvider)


class EmptyProvider(NullProvider):
    """Provider that returns nothing for all requests"""

    _isdir = _has = lambda self,path: False
    _get = lambda self,path: ''
    _listdir = lambda self,path: []
    module_path = None

    def __init__(self):
        pass

empty_provider = EmptyProvider()


def build_zipmanifest(path):
    """
    This builds a similar dictionary to the zipimport directory
    caches.  However instead of tuples, ZipInfo objects are stored.

    The translation of the tuple is as follows:
      * [0] - zipinfo.filename on stock pythons this needs "/" --> os.sep
              on pypy it is the same (one reason why distribute did work
              in some cases on pypy and win32).
      * [1] - zipinfo.compress_type
      * [2] - zipinfo.compress_size
      * [3] - zipinfo.file_size
      * [4] - len(utf-8 encoding of filename) if zipinfo & 0x800
              len(ascii encoding of filename) otherwise
      * [5] - (zipinfo.date_time[0] - 1980) << 9 |
               zipinfo.date_time[1] << 5 | zipinfo.date_time[2]
      * [6] - (zipinfo.date_time[3] - 1980) << 11 |
               zipinfo.date_time[4] << 5 | (zipinfo.date_time[5] // 2)
      * [7] - zipinfo.CRC
    """
    zipinfo = dict()
    zfile = zipfile.ZipFile(path)
    #Got ZipFile has not __exit__ on python 3.1
    try:
        for zitem in zfile.namelist():
            zpath = zitem.replace('/', os.sep)
            zipinfo[zpath] = zfile.getinfo(zitem)
            assert zipinfo[zpath] is not None
    finally:
        zfile.close()
    return zipinfo


class ZipProvider(EggProvider):
    """Resource support for zips and eggs"""

    eagers = None

    def __init__(self, module):
        EggProvider.__init__(self,module)
        self.zipinfo = build_zipmanifest(self.loader.archive)
        self.zip_pre = self.loader.archive+os.sep

    def _zipinfo_name(self, fspath):
        # Convert a virtual filename (full path to file) into a zipfile subpath
        # usable with the zipimport directory cache for our target archive
        if fspath.startswith(self.zip_pre):
            return fspath[len(self.zip_pre):]
        raise AssertionError(
            "%s is not a subpath of %s" % (fspath,self.zip_pre)
        )

    def _parts(self,zip_path):
        # Convert a zipfile subpath into an egg-relative path part list
        fspath = self.zip_pre+zip_path  # pseudo-fs path
        if fspath.startswith(self.egg_root+os.sep):
            return fspath[len(self.egg_root)+1:].split(os.sep)
        raise AssertionError(
            "%s is not a subpath of %s" % (fspath,self.egg_root)
        )

    def get_resource_filename(self, manager, resource_name):
        if not self.egg_name:
            raise NotImplementedError(
                "resource_filename() only supported for .egg, not .zip"
            )
        # no need to lock for extraction, since we use temp names
        zip_path = self._resource_to_zip(resource_name)
        eagers = self._get_eager_resources()
        if '/'.join(self._parts(zip_path)) in eagers:
            for name in eagers:
                self._extract_resource(manager, self._eager_to_zip(name))
        return self._extract_resource(manager, zip_path)

    @staticmethod
    def _get_date_and_size(zip_stat):
        size = zip_stat.file_size
        date_time = zip_stat.date_time + (0, 0, -1)  # ymdhms+wday, yday, dst
        #1980 offset already done
        timestamp = time.mktime(date_time)
        return timestamp, size

    def _extract_resource(self, manager, zip_path):

        if zip_path in self._index():
            for name in self._index()[zip_path]:
                last = self._extract_resource(
                    manager, os.path.join(zip_path, name)
                )
            return os.path.dirname(last)  # return the extracted directory name

        timestamp, size = self._get_date_and_size(self.zipinfo[zip_path])

        if not WRITE_SUPPORT:
            raise IOError('"os.rename" and "os.unlink" are not supported '
                          'on this platform')
        try:

            real_path = manager.get_cache_path(
                self.egg_name, self._parts(zip_path)
            )

            if self._is_current(real_path, zip_path):
                return real_path

            outf, tmpnam = _mkstemp(".$extract", dir=os.path.dirname(real_path))
            os.write(outf, self.loader.get_data(zip_path))
            os.close(outf)
            utime(tmpnam, (timestamp,timestamp))
            manager.postprocess(tmpnam, real_path)

            try:
                rename(tmpnam, real_path)

            except os.error:
                if os.path.isfile(real_path):
                    if self._is_current(real_path, zip_path):
                        # the file became current since it was checked above,
                        #  so proceed.
                        return real_path
                    elif os.name=='nt':     # Windows, del old file and retry
                        unlink(real_path)
                        rename(tmpnam, real_path)
                        return real_path
                raise

        except os.error:
            manager.extraction_error()  # report a user-friendly error

        return real_path

    def _is_current(self, file_path, zip_path):
        """
        Return True if the file_path is current for this zip_path
        """
        timestamp, size = self._get_date_and_size(self.zipinfo[zip_path])
        if not os.path.isfile(file_path):
            return False
        stat = os.stat(file_path)
        if stat.st_size!=size or stat.st_mtime!=timestamp:
            return False
        # check that the contents match
        zip_contents = self.loader.get_data(zip_path)
        f = open(file_path, 'rb')
        file_contents = f.read()
        f.close()
        return zip_contents == file_contents

    def _get_eager_resources(self):
        if self.eagers is None:
            eagers = []
            for name in ('native_libs.txt', 'eager_resources.txt'):
                if self.has_metadata(name):
                    eagers.extend(self.get_metadata_lines(name))
            self.eagers = eagers
        return self.eagers

    def _index(self):
        try:
            return self._dirindex
        except AttributeError:
            ind = {}
            for path in self.zipinfo:
                parts = path.split(os.sep)
                while parts:
                    parent = os.sep.join(parts[:-1])
                    if parent in ind:
                        ind[parent].append(parts[-1])
                        break
                    else:
                        ind[parent] = [parts.pop()]
            self._dirindex = ind
            return ind

    def _has(self, fspath):
        zip_path = self._zipinfo_name(fspath)
        return zip_path in self.zipinfo or zip_path in self._index()

    def _isdir(self,fspath):
        return self._zipinfo_name(fspath) in self._index()

    def _listdir(self,fspath):
        return list(self._index().get(self._zipinfo_name(fspath), ()))

    def _eager_to_zip(self,resource_name):
        return self._zipinfo_name(self._fn(self.egg_root,resource_name))

    def _resource_to_zip(self,resource_name):
        return self._zipinfo_name(self._fn(self.module_path,resource_name))

register_loader_type(zipimport.zipimporter, ZipProvider)


class FileMetadata(EmptyProvider):
    """Metadata handler for standalone PKG-INFO files

    Usage::

        metadata = FileMetadata("/path/to/PKG-INFO")

    This provider rejects all data and metadata requests except for PKG-INFO,
    which is treated as existing, and will be the contents of the file at
    the provided location.
    """

    def __init__(self,path):
        self.path = path

    def has_metadata(self,name):
        return name=='PKG-INFO'

    def get_metadata(self,name):
        if name=='PKG-INFO':
            f = open(self.path,'rU')
            metadata = f.read()
            f.close()
            return metadata
        raise KeyError("No metadata except PKG-INFO is available")

    def get_metadata_lines(self,name):
        return yield_lines(self.get_metadata(name))


class PathMetadata(DefaultProvider):
    """Metadata provider for egg directories

    Usage::

        # Development eggs:

        egg_info = "/path/to/PackageName.egg-info"
        base_dir = os.path.dirname(egg_info)
        metadata = PathMetadata(base_dir, egg_info)
        dist_name = os.path.splitext(os.path.basename(egg_info))[0]
        dist = Distribution(basedir,project_name=dist_name,metadata=metadata)

        # Unpacked egg directories:

        egg_path = "/path/to/PackageName-ver-pyver-etc.egg"
        metadata = PathMetadata(egg_path, os.path.join(egg_path,'EGG-INFO'))
        dist = Distribution.from_filename(egg_path, metadata=metadata)
    """

    def __init__(self, path, egg_info):
        self.module_path = path
        self.egg_info = egg_info


class EggMetadata(ZipProvider):
    """Metadata provider for .egg files"""

    def __init__(self, importer):
        """Create a metadata provider from a zipimporter"""

        self.zipinfo = build_zipmanifest(importer.archive)
        self.zip_pre = importer.archive+os.sep
        self.loader = importer
        if importer.prefix:
            self.module_path = os.path.join(importer.archive, importer.prefix)
        else:
            self.module_path = importer.archive
        self._setup_prefix()

_declare_state('dict', _distribution_finders = {})

def register_finder(importer_type, distribution_finder):
    """Register `distribution_finder` to find distributions in sys.path items

    `importer_type` is the type or class of a PEP 302 "Importer" (sys.path item
    handler), and `distribution_finder` is a callable that, passed a path
    item and the importer instance, yields ``Distribution`` instances found on
    that path item.  See ``pkg_resources.find_on_path`` for an example."""
    _distribution_finders[importer_type] = distribution_finder


def find_distributions(path_item, only=False):
    """Yield distributions accessible via `path_item`"""
    importer = get_importer(path_item)
    finder = _find_adapter(_distribution_finders, importer)
    return finder(importer, path_item, only)

def find_eggs_in_zip(importer, path_item, only=False):
    """
    Find eggs in zip files; possibly multiple nested eggs.
    """
    if importer.archive.endswith('.whl'):
        # wheels are not supported with this finder
        # they don't have PKG-INFO metadata, and won't ever contain eggs
        return
    metadata = EggMetadata(importer)
    if metadata.has_metadata('PKG-INFO'):
        yield Distribution.from_filename(path_item, metadata=metadata)
    if only:
        return  # don't yield nested distros
    for subitem in metadata.resource_listdir('/'):
        if subitem.endswith('.egg'):
            subpath = os.path.join(path_item, subitem)
            for dist in find_eggs_in_zip(zipimport.zipimporter(subpath), subpath):
                yield dist

register_finder(zipimport.zipimporter, find_eggs_in_zip)

def find_nothing(importer, path_item, only=False):
    return ()
register_finder(object,find_nothing)

def find_on_path(importer, path_item, only=False):
    """Yield distributions accessible on a sys.path directory"""
    path_item = _normalize_cached(path_item)

    if os.path.isdir(path_item) and os.access(path_item, os.R_OK):
        if path_item.lower().endswith('.egg'):
            # unpacked egg
            yield Distribution.from_filename(
                path_item, metadata=PathMetadata(
                    path_item, os.path.join(path_item,'EGG-INFO')
                )
            )
        else:
            # scan for .egg and .egg-info in directory
            for entry in os.listdir(path_item):
                lower = entry.lower()
                if lower.endswith('.egg-info') or lower.endswith('.dist-info'):
                    fullpath = os.path.join(path_item, entry)
                    if os.path.isdir(fullpath):
                        # egg-info directory, allow getting metadata
                        metadata = PathMetadata(path_item, fullpath)
                    else:
                        metadata = FileMetadata(fullpath)
                    yield Distribution.from_location(
                        path_item,entry,metadata,precedence=DEVELOP_DIST
                    )
                elif not only and lower.endswith('.egg'):
                    for dist in find_distributions(os.path.join(path_item, entry)):
                        yield dist
                elif not only and lower.endswith('.egg-link'):
                    entry_file = open(os.path.join(path_item, entry))
                    try:
                        entry_lines = entry_file.readlines()
                    finally:
                        entry_file.close()
                    for line in entry_lines:
                        if not line.strip(): continue
                        for item in find_distributions(os.path.join(path_item,line.rstrip())):
                            yield item
                        break
register_finder(pkgutil.ImpImporter,find_on_path)

if importlib_bootstrap is not None:
    register_finder(importlib_bootstrap.FileFinder, find_on_path)

_declare_state('dict', _namespace_handlers={})
_declare_state('dict', _namespace_packages={})


def register_namespace_handler(importer_type, namespace_handler):
    """Register `namespace_handler` to declare namespace packages

    `importer_type` is the type or class of a PEP 302 "Importer" (sys.path item
    handler), and `namespace_handler` is a callable like this::

        def namespace_handler(importer,path_entry,moduleName,module):
            # return a path_entry to use for child packages

    Namespace handlers are only called if the importer object has already
    agreed that it can handle the relevant path item, and they should only
    return a subpath if the module __path__ does not already contain an
    equivalent subpath.  For an example namespace handler, see
    ``pkg_resources.file_ns_handler``.
    """
    _namespace_handlers[importer_type] = namespace_handler

def _handle_ns(packageName, path_item):
    """Ensure that named package includes a subpath of path_item (if needed)"""

    importer = get_importer(path_item)
    if importer is None:
        return None
    loader = importer.find_module(packageName)
    if loader is None:
        return None
    module = sys.modules.get(packageName)
    if module is None:
        module = sys.modules[packageName] = imp.new_module(packageName)
        module.__path__ = []
        _set_parent_ns(packageName)
    elif not hasattr(module,'__path__'):
        raise TypeError("Not a package:", packageName)
    handler = _find_adapter(_namespace_handlers, importer)
    subpath = handler(importer, path_item, packageName, module)
    if subpath is not None:
        path = module.__path__
        path.append(subpath)
        loader.load_module(packageName)
        for path_item in path:
            if path_item not in module.__path__:
                module.__path__.append(path_item)
    return subpath

def declare_namespace(packageName):
    """Declare that package 'packageName' is a namespace package"""

    imp.acquire_lock()
    try:
        if packageName in _namespace_packages:
            return

        path, parent = sys.path, None
        if '.' in packageName:
            parent = '.'.join(packageName.split('.')[:-1])
            declare_namespace(parent)
            if parent not in _namespace_packages:
                __import__(parent)
            try:
                path = sys.modules[parent].__path__
            except AttributeError:
                raise TypeError("Not a package:", parent)

        # Track what packages are namespaces, so when new path items are added,
        # they can be updated
        _namespace_packages.setdefault(parent,[]).append(packageName)
        _namespace_packages.setdefault(packageName,[])

        for path_item in path:
            # Ensure all the parent's path items are reflected in the child,
            # if they apply
            _handle_ns(packageName, path_item)

    finally:
        imp.release_lock()

def fixup_namespace_packages(path_item, parent=None):
    """Ensure that previously-declared namespace packages include path_item"""
    imp.acquire_lock()
    try:
        for package in _namespace_packages.get(parent,()):
            subpath = _handle_ns(package, path_item)
            if subpath: fixup_namespace_packages(subpath,package)
    finally:
        imp.release_lock()

def file_ns_handler(importer, path_item, packageName, module):
    """Compute an ns-package subpath for a filesystem or zipfile importer"""

    subpath = os.path.join(path_item, packageName.split('.')[-1])
    normalized = _normalize_cached(subpath)
    for item in module.__path__:
        if _normalize_cached(item)==normalized:
            break
    else:
        # Only return the path if it's not already there
        return subpath

register_namespace_handler(pkgutil.ImpImporter,file_ns_handler)
register_namespace_handler(zipimport.zipimporter,file_ns_handler)

if importlib_bootstrap is not None:
    register_namespace_handler(importlib_bootstrap.FileFinder, file_ns_handler)


def null_ns_handler(importer, path_item, packageName, module):
    return None

register_namespace_handler(object,null_ns_handler)


def normalize_path(filename):
    """Normalize a file/dir name for comparison purposes"""
    return os.path.normcase(os.path.realpath(filename))

def _normalize_cached(filename,_cache={}):
    try:
        return _cache[filename]
    except KeyError:
        _cache[filename] = result = normalize_path(filename)
        return result

def _set_parent_ns(packageName):
    parts = packageName.split('.')
    name = parts.pop()
    if parts:
        parent = '.'.join(parts)
        setattr(sys.modules[parent], name, sys.modules[packageName])


def yield_lines(strs):
    """Yield non-empty/non-comment lines of a ``basestring`` or sequence"""
    if isinstance(strs,basestring):
        for s in strs.splitlines():
            s = s.strip()
            if s and not s.startswith('#'):     # skip blank lines/comments
                yield s
    else:
        for ss in strs:
            for s in yield_lines(ss):
                yield s

LINE_END = re.compile(r"\s*(#.*)?$").match         # whitespace and comment
CONTINUE = re.compile(r"\s*\\\s*(#.*)?$").match    # line continuation
DISTRO = re.compile(r"\s*((\w|[-.])+)").match    # Distribution or extra
VERSION = re.compile(r"\s*(<=?|>=?|==|!=)\s*((\w|[-.])+)").match  # ver. info
COMMA = re.compile(r"\s*,").match               # comma between items
OBRACKET = re.compile(r"\s*\[").match
CBRACKET = re.compile(r"\s*\]").match
MODULE = re.compile(r"\w+(\.\w+)*$").match
EGG_NAME = re.compile(
    r"(?P<name>[^-]+)"
    r"( -(?P<ver>[^-]+) (-py(?P<pyver>[^-]+) (-(?P<plat>.+))? )? )?",
    re.VERBOSE | re.IGNORECASE
).match

component_re = re.compile(r'(\d+ | [a-z]+ | \.| -)', re.VERBOSE)
replace = {'pre':'c', 'preview':'c','-':'final-','rc':'c','dev':'@'}.get

def _parse_version_parts(s):
    for part in component_re.split(s):
        part = replace(part,part)
        if not part or part=='.':
            continue
        if part[:1] in '0123456789':
            yield part.zfill(8)    # pad for numeric comparison
        else:
            yield '*'+part

    yield '*final'  # ensure that alpha/beta/candidate are before final

def parse_version(s):
    """Convert a version string to a chronologically-sortable key

    This is a rough cross between distutils' StrictVersion and LooseVersion;
    if you give it versions that would work with StrictVersion, then it behaves
    the same; otherwise it acts like a slightly-smarter LooseVersion. It is
    *possible* to create pathological version coding schemes that will fool
    this parser, but they should be very rare in practice.

    The returned value will be a tuple of strings.  Numeric portions of the
    version are padded to 8 digits so they will compare numerically, but
    without relying on how numbers compare relative to strings.  Dots are
    dropped, but dashes are retained.  Trailing zeros between alpha segments
    or dashes are suppressed, so that e.g. "2.4.0" is considered the same as
    "2.4". Alphanumeric parts are lower-cased.

    The algorithm assumes that strings like "-" and any alpha string that
    alphabetically follows "final"  represents a "patch level".  So, "2.4-1"
    is assumed to be a branch or patch of "2.4", and therefore "2.4.1" is
    considered newer than "2.4-1", which in turn is newer than "2.4".

    Strings like "a", "b", "c", "alpha", "beta", "candidate" and so on (that
    come before "final" alphabetically) are assumed to be pre-release versions,
    so that the version "2.4" is considered newer than "2.4a1".

    Finally, to handle miscellaneous cases, the strings "pre", "preview", and
    "rc" are treated as if they were "c", i.e. as though they were release
    candidates, and therefore are not as new as a version string that does not
    contain them, and "dev" is replaced with an '@' so that it sorts lower than
    than any other pre-release tag.
    """
    parts = []
    for part in _parse_version_parts(s.lower()):
        if part.startswith('*'):
            if part<'*final':   # remove '-' before a prerelease tag
                while parts and parts[-1]=='*final-': parts.pop()
            # remove trailing zeros from each series of numeric parts
            while parts and parts[-1]=='00000000':
                parts.pop()
        parts.append(part)
    return tuple(parts)
class EntryPoint(object):
    """Object representing an advertised importable object"""

    def __init__(self, name, module_name, attrs=(), extras=(), dist=None):
        if not MODULE(module_name):
            raise ValueError("Invalid module name", module_name)
        self.name = name
        self.module_name = module_name
        self.attrs = tuple(attrs)
        self.extras = Requirement.parse(("x[%s]" % ','.join(extras))).extras
        self.dist = dist

    def __str__(self):
        s = "%s = %s" % (self.name, self.module_name)
        if self.attrs:
            s += ':' + '.'.join(self.attrs)
        if self.extras:
            s += ' [%s]' % ','.join(self.extras)
        return s

    def __repr__(self):
        return "EntryPoint.parse(%r)" % str(self)

    def load(self, require=True, env=None, installer=None):
        if require: self.require(env, installer)
        entry = __import__(self.module_name, globals(),globals(), ['__name__'])
        for attr in self.attrs:
            try:
                entry = getattr(entry,attr)
            except AttributeError:
                raise ImportError("%r has no %r attribute" % (entry,attr))
        return entry

    def require(self, env=None, installer=None):
        if self.extras and not self.dist:
            raise UnknownExtra("Can't require() without a distribution", self)
        list(map(working_set.add,
            working_set.resolve(self.dist.requires(self.extras),env,installer)))

    @classmethod
    def parse(cls, src, dist=None):
        """Parse a single entry point from string `src`

        Entry point syntax follows the form::

            name = some.module:some.attr [extra1,extra2]

        The entry name and module name are required, but the ``:attrs`` and
        ``[extras]`` parts are optional
        """
        try:
            attrs = extras = ()
            name,value = src.split('=',1)
            if '[' in value:
                value,extras = value.split('[',1)
                req = Requirement.parse("x["+extras)
                if req.specs: raise ValueError
                extras = req.extras
            if ':' in value:
                value,attrs = value.split(':',1)
                if not MODULE(attrs.rstrip()):
                    raise ValueError
                attrs = attrs.rstrip().split('.')
        except ValueError:
            raise ValueError(
                "EntryPoint must be in 'name=module:attrs [extras]' format",
                src
            )
        else:
            return cls(name.strip(), value.strip(), attrs, extras, dist)

    @classmethod
    def parse_group(cls, group, lines, dist=None):
        """Parse an entry point group"""
        if not MODULE(group):
            raise ValueError("Invalid group name", group)
        this = {}
        for line in yield_lines(lines):
            ep = cls.parse(line, dist)
            if ep.name in this:
                raise ValueError("Duplicate entry point", group, ep.name)
            this[ep.name]=ep
        return this

    @classmethod
    def parse_map(cls, data, dist=None):
        """Parse a map of entry point groups"""
        if isinstance(data,dict):
            data = data.items()
        else:
            data = split_sections(data)
        maps = {}
        for group, lines in data:
            if group is None:
                if not lines:
                    continue
                raise ValueError("Entry points must be listed in groups")
            group = group.strip()
            if group in maps:
                raise ValueError("Duplicate group name", group)
            maps[group] = cls.parse_group(group, lines, dist)
        return maps


def _remove_md5_fragment(location):
    if not location:
        return ''
    parsed = urlparse(location)
    if parsed[-1].startswith('md5='):
        return urlunparse(parsed[:-1] + ('',))
    return location


class Distribution(object):
    """Wrap an actual or potential sys.path entry w/metadata"""
    PKG_INFO = 'PKG-INFO'

    def __init__(self, location=None, metadata=None, project_name=None,
            version=None, py_version=PY_MAJOR, platform=None,
            precedence=EGG_DIST):
        self.project_name = safe_name(project_name or 'Unknown')
        if version is not None:
            self._version = safe_version(version)
        self.py_version = py_version
        self.platform = platform
        self.location = location
        self.precedence = precedence
        self._provider = metadata or empty_provider

    @classmethod
    def from_location(cls,location,basename,metadata=None,**kw):
        project_name, version, py_version, platform = [None]*4
        basename, ext = os.path.splitext(basename)
        if ext.lower() in _distributionImpl:
            # .dist-info gets much metadata differently
            match = EGG_NAME(basename)
            if match:
                project_name, version, py_version, platform = match.group(
                    'name','ver','pyver','plat'
                )
            cls = _distributionImpl[ext.lower()]
        return cls(
            location, metadata, project_name=project_name, version=version,
            py_version=py_version, platform=platform, **kw
        )

    hashcmp = property(
        lambda self: (
            getattr(self,'parsed_version',()),
            self.precedence,
            self.key,
            _remove_md5_fragment(self.location),
            self.py_version,
            self.platform
        )
    )
    def __hash__(self): return hash(self.hashcmp)
    def __lt__(self, other):
        return self.hashcmp < other.hashcmp
    def __le__(self, other):
        return self.hashcmp <= other.hashcmp
    def __gt__(self, other):
        return self.hashcmp > other.hashcmp
    def __ge__(self, other):
        return self.hashcmp >= other.hashcmp
    def __eq__(self, other):
        if not isinstance(other, self.__class__):
            # It's not a Distribution, so they are not equal
            return False
        return self.hashcmp == other.hashcmp
    def __ne__(self, other):
        return not self == other

    # These properties have to be lazy so that we don't have to load any
    # metadata until/unless it's actually needed.  (i.e., some distributions
    # may not know their name or version without loading PKG-INFO)

    @property
    def key(self):
        try:
            return self._key
        except AttributeError:
            self._key = key = self.project_name.lower()
            return key

    @property
    def parsed_version(self):
        try:
            return self._parsed_version
        except AttributeError:
            self._parsed_version = pv = parse_version(self.version)
            return pv

    @property
    def version(self):
        try:
            return self._version
        except AttributeError:
            for line in self._get_metadata(self.PKG_INFO):
                if line.lower().startswith('version:'):
                    self._version = safe_version(line.split(':',1)[1].strip())
                    return self._version
            else:
                raise ValueError(
                    "Missing 'Version:' header and/or %s file" % self.PKG_INFO, self
                )

    @property
    def _dep_map(self):
        try:
            return self.__dep_map
        except AttributeError:
            dm = self.__dep_map = {None: []}
            for name in 'requires.txt', 'depends.txt':
                for extra,reqs in split_sections(self._get_metadata(name)):
                    if extra:
                        if ':' in extra:
                            extra, marker = extra.split(':',1)
                            if invalid_marker(marker):
                                reqs=[] # XXX warn
                            elif not evaluate_marker(marker):
                                reqs=[]
                        extra = safe_extra(extra) or None
                    dm.setdefault(extra,[]).extend(parse_requirements(reqs))
            return dm

    def requires(self,extras=()):
        """List of Requirements needed for this distro if `extras` are used"""
        dm = self._dep_map
        deps = []
        deps.extend(dm.get(None,()))
        for ext in extras:
            try:
                deps.extend(dm[safe_extra(ext)])
            except KeyError:
                raise UnknownExtra(
                    "%s has no such extra feature %r" % (self, ext)
                )
        return deps

    def _get_metadata(self,name):
        if self.has_metadata(name):
            for line in self.get_metadata_lines(name):
                yield line

    def activate(self,path=None):
        """Ensure distribution is importable on `path` (default=sys.path)"""
        if path is None: path = sys.path
        self.insert_on(path)
        if path is sys.path:
            fixup_namespace_packages(self.location)
            for pkg in self._get_metadata('namespace_packages.txt'):
                if pkg in sys.modules:
                    declare_namespace(pkg)

    def egg_name(self):
        """Return what this distribution's standard .egg filename should be"""
        filename = "%s-%s-py%s" % (
            to_filename(self.project_name), to_filename(self.version),
            self.py_version or PY_MAJOR
        )

        if self.platform:
            filename += '-'+self.platform
        return filename

    def __repr__(self):
        if self.location:
            return "%s (%s)" % (self,self.location)
        else:
            return str(self)

    def __str__(self):
        try: version = getattr(self,'version',None)
        except ValueError: version = None
        version = version or "[unknown version]"
        return "%s %s" % (self.project_name,version)

    def __getattr__(self,attr):
        """Delegate all unrecognized public attributes to .metadata provider"""
        if attr.startswith('_'):
            raise AttributeError(attr)
        return getattr(self._provider, attr)

    @classmethod
    def from_filename(cls,filename,metadata=None, **kw):
        return cls.from_location(
            _normalize_cached(filename), os.path.basename(filename), metadata,
            **kw
        )

    def as_requirement(self):
        """Return a ``Requirement`` that matches this distribution exactly"""
        return Requirement.parse('%s==%s' % (self.project_name, self.version))

    def load_entry_point(self, group, name):
        """Return the `name` entry point of `group` or raise ImportError"""
        ep = self.get_entry_info(group,name)
        if ep is None:
            raise ImportError("Entry point %r not found" % ((group,name),))
        return ep.load()

    def get_entry_map(self, group=None):
        """Return the entry point map for `group`, or the full entry map"""
        try:
            ep_map = self._ep_map
        except AttributeError:
            ep_map = self._ep_map = EntryPoint.parse_map(
                self._get_metadata('entry_points.txt'), self
            )
        if group is not None:
            return ep_map.get(group,{})
        return ep_map

    def get_entry_info(self, group, name):
        """Return the EntryPoint object for `group`+`name`, or ``None``"""
        return self.get_entry_map(group).get(name)

    def insert_on(self, path, loc = None):
        """Insert self.location in path before its nearest parent directory"""

        loc = loc or self.location
        if not loc:
            return

        nloc = _normalize_cached(loc)
        bdir = os.path.dirname(nloc)
        npath= [(p and _normalize_cached(p) or p) for p in path]

        for p, item in enumerate(npath):
            if item==nloc:
                break
            elif item==bdir and self.precedence==EGG_DIST:
                # if it's an .egg, give it precedence over its directory
                if path is sys.path:
                    self.check_version_conflict()
                path.insert(p, loc)
                npath.insert(p, nloc)
                break
        else:
            if path is sys.path:
                self.check_version_conflict()
            path.append(loc)
            return

        # p is the spot where we found or inserted loc; now remove duplicates
        while 1:
            try:
                np = npath.index(nloc, p+1)
            except ValueError:
                break
            else:
                del npath[np], path[np]
                p = np  # ha!

        return

    def check_version_conflict(self):
        if self.key=='setuptools':
            return      # ignore the inevitable setuptools self-conflicts  :(

        nsp = dict.fromkeys(self._get_metadata('namespace_packages.txt'))
        loc = normalize_path(self.location)
        for modname in self._get_metadata('top_level.txt'):
            if (modname not in sys.modules or modname in nsp
                    or modname in _namespace_packages):
                continue
            if modname in ('pkg_resources', 'setuptools', 'site'):
                continue
            fn = getattr(sys.modules[modname], '__file__', None)
            if fn and (normalize_path(fn).startswith(loc) or
                       fn.startswith(self.location)):
                continue
            issue_warning(
                "Module %s was already imported from %s, but %s is being added"
                " to sys.path" % (modname, fn, self.location),
            )

    def has_version(self):
        try:
            self.version
        except ValueError:
            issue_warning("Unbuilt egg for "+repr(self))
            return False
        return True

    def clone(self,**kw):
        """Copy this distribution, substituting in any changed keyword args"""
        for attr in (
            'project_name', 'version', 'py_version', 'platform', 'location',
            'precedence'
        ):
            kw.setdefault(attr, getattr(self,attr,None))
        kw.setdefault('metadata', self._provider)
        return self.__class__(**kw)

    @property
    def extras(self):
        return [dep for dep in self._dep_map if dep]


class DistInfoDistribution(Distribution):
    """Wrap an actual or potential sys.path entry w/metadata, .dist-info style"""
    PKG_INFO = 'METADATA'
    EQEQ = re.compile(r"([\(,])\s*(\d.*?)\s*([,\)])")

    @property
    def _parsed_pkg_info(self):
        """Parse and cache metadata"""
        try:
            return self._pkg_info
        except AttributeError:
            from email.parser import Parser
            self._pkg_info = Parser().parsestr(self.get_metadata(self.PKG_INFO))
            return self._pkg_info

    @property
    def _dep_map(self):
        try:
            return self.__dep_map
        except AttributeError:
            self.__dep_map = self._compute_dependencies()
            return self.__dep_map

    def _preparse_requirement(self, requires_dist):
        """Convert 'Foobar (1); baz' to ('Foobar ==1', 'baz')
        Split environment marker, add == prefix to version specifiers as
        necessary, and remove parenthesis.
        """
        parts = requires_dist.split(';', 1) + ['']
        distvers = parts[0].strip()
        mark = parts[1].strip()
        distvers = re.sub(self.EQEQ, r"\1==\2\3", distvers)
        distvers = distvers.replace('(', '').replace(')', '')
        return (distvers, mark)

    def _compute_dependencies(self):
        """Recompute this distribution's dependencies."""
        from pip._vendor._markerlib import compile as compile_marker
        dm = self.__dep_map = {None: []}

        reqs = []
        # Including any condition expressions
        for req in self._parsed_pkg_info.get_all('Requires-Dist') or []:
            distvers, mark = self._preparse_requirement(req)
            parsed = next(parse_requirements(distvers))
            parsed.marker_fn = compile_marker(mark)
            reqs.append(parsed)

        def reqs_for_extra(extra):
            for req in reqs:
                if req.marker_fn(override={'extra':extra}):
                    yield req

        common = frozenset(reqs_for_extra(None))
        dm[None].extend(common)

        for extra in self._parsed_pkg_info.get_all('Provides-Extra') or []:
            extra = safe_extra(extra.strip())
            dm[extra] = list(frozenset(reqs_for_extra(extra)) - common)

        return dm


_distributionImpl = {
    '.egg': Distribution,
    '.egg-info': Distribution,
    '.dist-info': DistInfoDistribution,
    }


def issue_warning(*args,**kw):
    level = 1
    g = globals()
    try:
        # find the first stack frame that is *not* code in
        # the pkg_resources module, to use for the warning
        while sys._getframe(level).f_globals is g:
            level += 1
    except ValueError:
        pass
    from warnings import warn
    warn(stacklevel = level+1, *args, **kw)


def parse_requirements(strs):
    """Yield ``Requirement`` objects for each specification in `strs`

    `strs` must be an instance of ``basestring``, or a (possibly-nested)
    iterable thereof.
    """
    # create a steppable iterator, so we can handle \-continuations
    lines = iter(yield_lines(strs))

    def scan_list(ITEM,TERMINATOR,line,p,groups,item_name):

        items = []

        while not TERMINATOR(line,p):
            if CONTINUE(line,p):
                try:
                    line = next(lines)
                    p = 0
                except StopIteration:
                    raise ValueError(
                        "\\ must not appear on the last nonblank line"
                    )

            match = ITEM(line,p)
            if not match:
                raise ValueError("Expected "+item_name+" in",line,"at",line[p:])

            items.append(match.group(*groups))
            p = match.end()

            match = COMMA(line,p)
            if match:
                p = match.end() # skip the comma
            elif not TERMINATOR(line,p):
                raise ValueError(
                    "Expected ',' or end-of-list in",line,"at",line[p:]
                )

        match = TERMINATOR(line,p)
        if match: p = match.end()   # skip the terminator, if any
        return line, p, items

    for line in lines:
        match = DISTRO(line)
        if not match:
            raise ValueError("Missing distribution spec", line)
        project_name = match.group(1)
        p = match.end()
        extras = []

        match = OBRACKET(line,p)
        if match:
            p = match.end()
            line, p, extras = scan_list(
                DISTRO, CBRACKET, line, p, (1,), "'extra' name"
            )

        line, p, specs = scan_list(VERSION,LINE_END,line,p,(1,2),"version spec")
        specs = [(op,safe_version(val)) for op,val in specs]
        yield Requirement(project_name, specs, extras)


def _sort_dists(dists):
    tmp = [(dist.hashcmp,dist) for dist in dists]
    tmp.sort()
    dists[::-1] = [d for hc,d in tmp]


class Requirement:
    def __init__(self, project_name, specs, extras):
        """DO NOT CALL THIS UNDOCUMENTED METHOD; use Requirement.parse()!"""
        self.unsafe_name, project_name = project_name, safe_name(project_name)
        self.project_name, self.key = project_name, project_name.lower()
        index = [(parse_version(v),state_machine[op],op,v) for op,v in specs]
        index.sort()
        self.specs = [(op,ver) for parsed,trans,op,ver in index]
        self.index, self.extras = index, tuple(map(safe_extra,extras))
        self.hashCmp = (
            self.key, tuple([(op,parsed) for parsed,trans,op,ver in index]),
            frozenset(self.extras)
        )
        self.__hash = hash(self.hashCmp)

    def __str__(self):
        specs = ','.join([''.join(s) for s in self.specs])
        extras = ','.join(self.extras)
        if extras: extras = '[%s]' % extras
        return '%s%s%s' % (self.project_name, extras, specs)

    def __eq__(self,other):
        return isinstance(other,Requirement) and self.hashCmp==other.hashCmp

    def __contains__(self,item):
        if isinstance(item,Distribution):
            if item.key != self.key: return False
            if self.index: item = item.parsed_version  # only get if we need it
        elif isinstance(item,basestring):
            item = parse_version(item)
        last = None
        compare = lambda a, b: (a > b) - (a < b) # -1, 0, 1
        for parsed,trans,op,ver in self.index:
            action = trans[compare(item,parsed)] # Indexing: 0, 1, -1
            if action=='F':
                return False
            elif action=='T':
                return True
            elif action=='+':
                last = True
            elif action=='-' or last is None:   last = False
        if last is None: last = True    # no rules encountered
        return last

    def __hash__(self):
        return self.__hash

    def __repr__(self): return "Requirement.parse(%r)" % str(self)

    @staticmethod
    def parse(s):
        reqs = list(parse_requirements(s))
        if reqs:
            if len(reqs)==1:
                return reqs[0]
            raise ValueError("Expected only one requirement", s)
        raise ValueError("No requirements found", s)

state_machine = {
    #       =><
    '<': '--T',
    '<=': 'T-T',
    '>': 'F+F',
    '>=': 'T+F',
    '==': 'T..',
    '!=': 'F++',
}


def _get_mro(cls):
    """Get an mro for a type or classic class"""
    if not isinstance(cls,type):
        class cls(cls,object): pass
        return cls.__mro__[1:]
    return cls.__mro__

def _find_adapter(registry, ob):
    """Return an adapter factory for `ob` from `registry`"""
    for t in _get_mro(getattr(ob, '__class__', type(ob))):
        if t in registry:
            return registry[t]


def ensure_directory(path):
    """Ensure that the parent directory of `path` exists"""
    dirname = os.path.dirname(path)
    if not os.path.isdir(dirname):
        os.makedirs(dirname)

def split_sections(s):
    """Split a string or iterable thereof into (section,content) pairs

    Each ``section`` is a stripped version of the section header ("[section]")
    and each ``content`` is a list of stripped lines excluding blank lines and
    comment-only lines.  If there are any such lines before the first section
    header, they're returned in a first ``section`` of ``None``.
    """
    section = None
    content = []
    for line in yield_lines(s):
        if line.startswith("["):
            if line.endswith("]"):
                if section or content:
                    yield section, content
                section = line[1:-1].strip()
                content = []
            else:
                raise ValueError("Invalid section heading", line)
        else:
            content.append(line)

    # wrap up last segment
    yield section, content

def _mkstemp(*args,**kw):
    from tempfile import mkstemp
    old_open = os.open
    try:
        os.open = os_open   # temporarily bypass sandboxing
        return mkstemp(*args,**kw)
    finally:
        os.open = old_open  # and then put it back


# Set up global resource manager (deliberately not state-saved)
_manager = ResourceManager()
def _initialize(g):
    for name in dir(_manager):
        if not name.startswith('_'):
            g[name] = getattr(_manager, name)
_initialize(globals())

# Prepare the master working set and make the ``require()`` API available
working_set = WorkingSet._build_master()
_declare_state('object', working_set=working_set)

require = working_set.require
iter_entry_points = working_set.iter_entry_points
add_activation_listener = working_set.subscribe
run_script = working_set.run_script
run_main = run_script   # backward compatibility
# Activate all distributions already on sys.path, and ensure that
# all distributions added to the working set in the future (e.g. by
# calling ``require()``) will get activated as well.
add_activation_listener(lambda dist: dist.activate())
working_set.entries=[]
list(map(working_set.add_entry,sys.path)) # match order
