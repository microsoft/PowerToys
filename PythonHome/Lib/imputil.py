"""
Import utilities

Exported classes:
    ImportManager   Manage the import process

    Importer        Base class for replacing standard import functions
    BuiltinImporter Emulate the import mechanism for builtin and frozen modules

    DynLoadSuffixImporter
"""
from warnings import warnpy3k
warnpy3k("the imputil module has been removed in Python 3.0", stacklevel=2)
del warnpy3k

# note: avoid importing non-builtin modules
import imp                      ### not available in Jython?
import sys
import __builtin__

# for the DirectoryImporter
import struct
import marshal

__all__ = ["ImportManager","Importer","BuiltinImporter"]

_StringType = type('')
_ModuleType = type(sys)         ### doesn't work in Jython...

class ImportManager:
    "Manage the import process."

    def install(self, namespace=vars(__builtin__)):
        "Install this ImportManager into the specified namespace."

        if isinstance(namespace, _ModuleType):
            namespace = vars(namespace)

        # Note: we have no notion of "chaining"

        # Record the previous import hook, then install our own.
        self.previous_importer = namespace['__import__']
        self.namespace = namespace
        namespace['__import__'] = self._import_hook

        ### fix this
        #namespace['reload'] = self._reload_hook

    def uninstall(self):
        "Restore the previous import mechanism."
        self.namespace['__import__'] = self.previous_importer

    def add_suffix(self, suffix, importFunc):
        assert hasattr(importFunc, '__call__')
        self.fs_imp.add_suffix(suffix, importFunc)

    ######################################################################
    #
    # PRIVATE METHODS
    #

    clsFilesystemImporter = None

    def __init__(self, fs_imp=None):
        # we're definitely going to be importing something in the future,
        # so let's just load the OS-related facilities.
        if not _os_stat:
            _os_bootstrap()

        # This is the Importer that we use for grabbing stuff from the
        # filesystem. It defines one more method (import_from_dir) for our use.
        if fs_imp is None:
            cls = self.clsFilesystemImporter or _FilesystemImporter
            fs_imp = cls()
        self.fs_imp = fs_imp

        # Initialize the set of suffixes that we recognize and import.
        # The default will import dynamic-load modules first, followed by
        # .py files (or a .py file's cached bytecode)
        for desc in imp.get_suffixes():
            if desc[2] == imp.C_EXTENSION:
                self.add_suffix(desc[0],
                                DynLoadSuffixImporter(desc).import_file)
        self.add_suffix('.py', py_suffix_importer)

    def _import_hook(self, fqname, globals=None, locals=None, fromlist=None):
        """Python calls this hook to locate and import a module."""

        parts = fqname.split('.')

        # determine the context of this import
        parent = self._determine_import_context(globals)

        # if there is a parent, then its importer should manage this import
        if parent:
            module = parent.__importer__._do_import(parent, parts, fromlist)
            if module:
                return module

        # has the top module already been imported?
        try:
            top_module = sys.modules[parts[0]]
        except KeyError:

            # look for the topmost module
            top_module = self._import_top_module(parts[0])
            if not top_module:
                # the topmost module wasn't found at all.
                raise ImportError, 'No module named ' + fqname

        # fast-path simple imports
        if len(parts) == 1:
            if not fromlist:
                return top_module

            if not top_module.__dict__.get('__ispkg__'):
                # __ispkg__ isn't defined (the module was not imported by us),
                # or it is zero.
                #
                # In the former case, there is no way that we could import
                # sub-modules that occur in the fromlist (but we can't raise an
                # error because it may just be names) because we don't know how
                # to deal with packages that were imported by other systems.
                #
                # In the latter case (__ispkg__ == 0), there can't be any sub-
                # modules present, so we can just return.
                #
                # In both cases, since len(parts) == 1, the top_module is also
                # the "bottom" which is the defined return when a fromlist
                # exists.
                return top_module

        importer = top_module.__dict__.get('__importer__')
        if importer:
            return importer._finish_import(top_module, parts[1:], fromlist)

        # Grrr, some people "import os.path" or do "from os.path import ..."
        if len(parts) == 2 and hasattr(top_module, parts[1]):
            if fromlist:
                return getattr(top_module, parts[1])
            else:
                return top_module

        # If the importer does not exist, then we have to bail. A missing
        # importer means that something else imported the module, and we have
        # no knowledge of how to get sub-modules out of the thing.
        raise ImportError, 'No module named ' + fqname

    def _determine_import_context(self, globals):
        """Returns the context in which a module should be imported.

        The context could be a loaded (package) module and the imported module
        will be looked for within that package. The context could also be None,
        meaning there is no context -- the module should be looked for as a
        "top-level" module.
        """

        if not globals or not globals.get('__importer__'):
            # globals does not refer to one of our modules or packages. That
            # implies there is no relative import context (as far as we are
            # concerned), and it should just pick it off the standard path.
            return None

        # The globals refer to a module or package of ours. It will define
        # the context of the new import. Get the module/package fqname.
        parent_fqname = globals['__name__']

        # if a package is performing the import, then return itself (imports
        # refer to pkg contents)
        if globals['__ispkg__']:
            parent = sys.modules[parent_fqname]
            assert globals is parent.__dict__
            return parent

        i = parent_fqname.rfind('.')

        # a module outside of a package has no particular import context
        if i == -1:
            return None

        # if a module in a package is performing the import, then return the
        # package (imports refer to siblings)
        parent_fqname = parent_fqname[:i]
        parent = sys.modules[parent_fqname]
        assert parent.__name__ == parent_fqname
        return parent

    def _import_top_module(self, name):
        # scan sys.path looking for a location in the filesystem that contains
        # the module, or an Importer object that can import the module.
        for item in sys.path:
            if isinstance(item, _StringType):
                module = self.fs_imp.import_from_dir(item, name)
            else:
                module = item.import_top(name)
            if module:
                return module
        return None

    def _reload_hook(self, module):
        "Python calls this hook to reload a module."

        # reloading of a module may or may not be possible (depending on the
        # importer), but at least we can validate that it's ours to reload
        importer = module.__dict__.get('__importer__')
        if not importer:
            ### oops. now what...
            pass

        # okay. it is using the imputil system, and we must delegate it, but
        # we don't know what to do (yet)
        ### we should blast the module dict and do another get_code(). need to
        ### flesh this out and add proper docco...
        raise SystemError, "reload not yet implemented"


class Importer:
    "Base class for replacing standard import functions."

    def import_top(self, name):
        "Import a top-level module."
        return self._import_one(None, name, name)

    ######################################################################
    #
    # PRIVATE METHODS
    #
    def _finish_import(self, top, parts, fromlist):
        # if "a.b.c" was provided, then load the ".b.c" portion down from
        # below the top-level module.
        bottom = self._load_tail(top, parts)

        # if the form is "import a.b.c", then return "a"
        if not fromlist:
            # no fromlist: return the top of the import tree
            return top

        # the top module was imported by self.
        #
        # this means that the bottom module was also imported by self (just
        # now, or in the past and we fetched it from sys.modules).
        #
        # since we imported/handled the bottom module, this means that we can
        # also handle its fromlist (and reliably use __ispkg__).

        # if the bottom node is a package, then (potentially) import some
        # modules.
        #
        # note: if it is not a package, then "fromlist" refers to names in
        #       the bottom module rather than modules.
        # note: for a mix of names and modules in the fromlist, we will
        #       import all modules and insert those into the namespace of
        #       the package module. Python will pick up all fromlist names
        #       from the bottom (package) module; some will be modules that
        #       we imported and stored in the namespace, others are expected
        #       to be present already.
        if bottom.__ispkg__:
            self._import_fromlist(bottom, fromlist)

        # if the form is "from a.b import c, d" then return "b"
        return bottom

    def _import_one(self, parent, modname, fqname):
        "Import a single module."

        # has the module already been imported?
        try:
            return sys.modules[fqname]
        except KeyError:
            pass

        # load the module's code, or fetch the module itself
        result = self.get_code(parent, modname, fqname)
        if result is None:
            return None

        module = self._process_result(result, fqname)

        # insert the module into its parent
        if parent:
            setattr(parent, modname, module)
        return module

    def _process_result(self, result, fqname):
        ispkg, code, values = result
        # did get_code() return an actual module? (rather than a code object)
        is_module = isinstance(code, _ModuleType)

        # use the returned module, or create a new one to exec code into
        if is_module:
            module = code
        else:
            module = imp.new_module(fqname)

        ### record packages a bit differently??
        module.__importer__ = self
        module.__ispkg__ = ispkg

        # insert additional values into the module (before executing the code)
        module.__dict__.update(values)

        # the module is almost ready... make it visible
        sys.modules[fqname] = module

        # execute the code within the module's namespace
        if not is_module:
            try:
                exec code in module.__dict__
            except:
                if fqname in sys.modules:
                    del sys.modules[fqname]
                raise

        # fetch from sys.modules instead of returning module directly.
        # also make module's __name__ agree with fqname, in case
        # the "exec code in module.__dict__" played games on us.
        module = sys.modules[fqname]
        module.__name__ = fqname
        return module

    def _load_tail(self, m, parts):
        """Import the rest of the modules, down from the top-level module.

        Returns the last module in the dotted list of modules.
        """
        for part in parts:
            fqname = "%s.%s" % (m.__name__, part)
            m = self._import_one(m, part, fqname)
            if not m:
                raise ImportError, "No module named " + fqname
        return m

    def _import_fromlist(self, package, fromlist):
        'Import any sub-modules in the "from" list.'

        # if '*' is present in the fromlist, then look for the '__all__'
        # variable to find additional items (modules) to import.
        if '*' in fromlist:
            fromlist = list(fromlist) + \
                       list(package.__dict__.get('__all__', []))

        for sub in fromlist:
            # if the name is already present, then don't try to import it (it
            # might not be a module!).
            if sub != '*' and not hasattr(package, sub):
                subname = "%s.%s" % (package.__name__, sub)
                submod = self._import_one(package, sub, subname)
                if not submod:
                    raise ImportError, "cannot import name " + subname

    def _do_import(self, parent, parts, fromlist):
        """Attempt to import the module relative to parent.

        This method is used when the import context specifies that <self>
        imported the parent module.
        """
        top_name = parts[0]
        top_fqname = parent.__name__ + '.' + top_name
        top_module = self._import_one(parent, top_name, top_fqname)
        if not top_module:
            # this importer and parent could not find the module (relatively)
            return None

        return self._finish_import(top_module, parts[1:], fromlist)

    ######################################################################
    #
    # METHODS TO OVERRIDE
    #
    def get_code(self, parent, modname, fqname):
        """Find and retrieve the code for the given module.

        parent specifies a parent module to define a context for importing. It
        may be None, indicating no particular context for the search.

        modname specifies a single module (not dotted) within the parent.

        fqname specifies the fully-qualified module name. This is a
        (potentially) dotted name from the "root" of the module namespace
        down to the modname.
        If there is no parent, then modname==fqname.

        This method should return None, or a 3-tuple.

        * If the module was not found, then None should be returned.

        * The first item of the 2- or 3-tuple should be the integer 0 or 1,
            specifying whether the module that was found is a package or not.

        * The second item is the code object for the module (it will be
            executed within the new module's namespace). This item can also
            be a fully-loaded module object (e.g. loaded from a shared lib).

        * The third item is a dictionary of name/value pairs that will be
            inserted into new module before the code object is executed. This
            is provided in case the module's code expects certain values (such
            as where the module was found). When the second item is a module
            object, then these names/values will be inserted *after* the module
            has been loaded/initialized.
        """
        raise RuntimeError, "get_code not implemented"


######################################################################
#
# Some handy stuff for the Importers
#

# byte-compiled file suffix character
_suffix_char = __debug__ and 'c' or 'o'

# byte-compiled file suffix
_suffix = '.py' + _suffix_char

def _compile(pathname, timestamp):
    """Compile (and cache) a Python source file.

    The file specified by <pathname> is compiled to a code object and
    returned.

    Presuming the appropriate privileges exist, the bytecodes will be
    saved back to the filesystem for future imports. The source file's
    modification timestamp must be provided as a Long value.
    """
    codestring = open(pathname, 'rU').read()
    if codestring and codestring[-1] != '\n':
        codestring = codestring + '\n'
    code = __builtin__.compile(codestring, pathname, 'exec')

    # try to cache the compiled code
    try:
        f = open(pathname + _suffix_char, 'wb')
    except IOError:
        pass
    else:
        f.write('\0\0\0\0')
        f.write(struct.pack('<I', timestamp))
        marshal.dump(code, f)
        f.flush()
        f.seek(0, 0)
        f.write(imp.get_magic())
        f.close()

    return code

_os_stat = _os_path_join = None
def _os_bootstrap():
    "Set up 'os' module replacement functions for use during import bootstrap."

    names = sys.builtin_module_names

    join = None
    if 'posix' in names:
        sep = '/'
        from posix import stat
    elif 'nt' in names:
        sep = '\\'
        from nt import stat
    elif 'dos' in names:
        sep = '\\'
        from dos import stat
    elif 'os2' in names:
        sep = '\\'
        from os2 import stat
    else:
        raise ImportError, 'no os specific module found'

    if join is None:
        def join(a, b, sep=sep):
            if a == '':
                return b
            lastchar = a[-1:]
            if lastchar == '/' or lastchar == sep:
                return a + b
            return a + sep + b

    global _os_stat
    _os_stat = stat

    global _os_path_join
    _os_path_join = join

def _os_path_isdir(pathname):
    "Local replacement for os.path.isdir()."
    try:
        s = _os_stat(pathname)
    except OSError:
        return None
    return (s.st_mode & 0170000) == 0040000

def _timestamp(pathname):
    "Return the file modification time as a Long."
    try:
        s = _os_stat(pathname)
    except OSError:
        return None
    return long(s.st_mtime)


######################################################################
#
# Emulate the import mechanism for builtin and frozen modules
#
class BuiltinImporter(Importer):
    def get_code(self, parent, modname, fqname):
        if parent:
            # these modules definitely do not occur within a package context
            return None

        # look for the module
        if imp.is_builtin(modname):
            type = imp.C_BUILTIN
        elif imp.is_frozen(modname):
            type = imp.PY_FROZEN
        else:
            # not found
            return None

        # got it. now load and return it.
        module = imp.load_module(modname, None, modname, ('', '', type))
        return 0, module, { }


######################################################################
#
# Internal importer used for importing from the filesystem
#
class _FilesystemImporter(Importer):
    def __init__(self):
        self.suffixes = [ ]

    def add_suffix(self, suffix, importFunc):
        assert hasattr(importFunc, '__call__')
        self.suffixes.append((suffix, importFunc))

    def import_from_dir(self, dir, fqname):
        result = self._import_pathname(_os_path_join(dir, fqname), fqname)
        if result:
            return self._process_result(result, fqname)
        return None

    def get_code(self, parent, modname, fqname):
        # This importer is never used with an empty parent. Its existence is
        # private to the ImportManager. The ImportManager uses the
        # import_from_dir() method to import top-level modules/packages.
        # This method is only used when we look for a module within a package.
        assert parent

        for submodule_path in parent.__path__:
            code = self._import_pathname(_os_path_join(submodule_path, modname), fqname)
            if code is not None:
                return code
        return self._import_pathname(_os_path_join(parent.__pkgdir__, modname),
                                     fqname)

    def _import_pathname(self, pathname, fqname):
        if _os_path_isdir(pathname):
            result = self._import_pathname(_os_path_join(pathname, '__init__'),
                                           fqname)
            if result:
                values = result[2]
                values['__pkgdir__'] = pathname
                values['__path__'] = [ pathname ]
                return 1, result[1], values
            return None

        for suffix, importFunc in self.suffixes:
            filename = pathname + suffix
            try:
                finfo = _os_stat(filename)
            except OSError:
                pass
            else:
                return importFunc(filename, finfo, fqname)
        return None

######################################################################
#
# SUFFIX-BASED IMPORTERS
#

def py_suffix_importer(filename, finfo, fqname):
    file = filename[:-3] + _suffix
    t_py = long(finfo[8])
    t_pyc = _timestamp(file)

    code = None
    if t_pyc is not None and t_pyc >= t_py:
        f = open(file, 'rb')
        if f.read(4) == imp.get_magic():
            t = struct.unpack('<I', f.read(4))[0]
            if t == t_py:
                code = marshal.load(f)
        f.close()
    if code is None:
        file = filename
        code = _compile(file, t_py)

    return 0, code, { '__file__' : file }

class DynLoadSuffixImporter:
    def __init__(self, desc):
        self.desc = desc

    def import_file(self, filename, finfo, fqname):
        fp = open(filename, self.desc[1])
        module = imp.load_module(fqname, fp, filename, self.desc)
        module.__file__ = filename
        return 0, module, { }


######################################################################

def _print_importers():
    items = sys.modules.items()
    items.sort()
    for name, module in items:
        if module:
            print name, module.__dict__.get('__importer__', '-- no importer')
        else:
            print name, '-- non-existent module'

def _test_revamp():
    ImportManager().install()
    sys.path.insert(0, BuiltinImporter())

######################################################################

#
# TODO
#
# from Finn Bock:
#   type(sys) is not a module in Jython. what to use instead?
#   imp.C_EXTENSION is not in Jython. same for get_suffixes and new_module
#
#   given foo.py of:
#      import sys
#      sys.modules['foo'] = sys
#
#   ---- standard import mechanism
#   >>> import foo
#   >>> foo
#   <module 'sys' (built-in)>
#
#   ---- revamped import mechanism
#   >>> import imputil
#   >>> imputil._test_revamp()
#   >>> import foo
#   >>> foo
#   <module 'foo' from 'foo.py'>
#
#
# from MAL:
#   should BuiltinImporter exist in sys.path or hard-wired in ImportManager?
#   need __path__ processing
#   performance
#   move chaining to a subclass [gjs: it's been nuked]
#   deinstall should be possible
#   query mechanism needed: is a specific Importer installed?
#   py/pyc/pyo piping hooks to filter/process these files
#   wish list:
#     distutils importer hooked to list of standard Internet repositories
#     module->file location mapper to speed FS-based imports
#     relative imports
#     keep chaining so that it can play nice with other import hooks
#
# from Gordon:
#   push MAL's mapper into sys.path[0] as a cache (hard-coded for apps)
#
# from Guido:
#   need to change sys.* references for rexec environs
#   need hook for MAL's walk-me-up import strategy, or Tim's absolute strategy
#   watch out for sys.modules[...] is None
#   flag to force absolute imports? (speeds _determine_import_context and
#       checking for a relative module)
#   insert names of archives into sys.path  (see quote below)
#   note: reload does NOT blast module dict
#   shift import mechanisms and policies around; provide for hooks, overrides
#       (see quote below)
#   add get_source stuff
#   get_topcode and get_subcode
#   CRLF handling in _compile
#   race condition in _compile
#   refactoring of os.py to deal with _os_bootstrap problem
#   any special handling to do for importing a module with a SyntaxError?
#       (e.g. clean up the traceback)
#   implement "domain" for path-type functionality using pkg namespace
#       (rather than FS-names like __path__)
#   don't use the word "private"... maybe "internal"
#
#
# Guido's comments on sys.path caching:
#
# We could cache this in a dictionary: the ImportManager can have a
# cache dict mapping pathnames to importer objects, and a separate
# method for coming up with an importer given a pathname that's not yet
# in the cache.  The method should do a stat and/or look at the
# extension to decide which importer class to use; you can register new
# importer classes by registering a suffix or a Boolean function, plus a
# class.  If you register a new importer class, the cache is zapped.
# The cache is independent from sys.path (but maintained per
# ImportManager instance) so that rearrangements of sys.path do the
# right thing.  If a path is dropped from sys.path the corresponding
# cache entry is simply no longer used.
#
# My/Guido's comments on factoring ImportManager and Importer:
#
# > However, we still have a tension occurring here:
# >
# > 1) implementing policy in ImportManager assists in single-point policy
# >    changes for app/rexec situations
# > 2) implementing policy in Importer assists in package-private policy
# >    changes for normal, operating conditions
# >
# > I'll see if I can sort out a way to do this. Maybe the Importer class will
# > implement the methods (which can be overridden to change policy) by
# > delegating to ImportManager.
#
# Maybe also think about what kind of policies an Importer would be
# likely to want to change.  I have a feeling that a lot of the code
# there is actually not so much policy but a *necessity* to get things
# working given the calling conventions for the __import__ hook: whether
# to return the head or tail of a dotted name, or when to do the "finish
# fromlist" stuff.
#
