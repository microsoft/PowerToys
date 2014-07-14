"""runpy.py - locating and running Python code using the module namespace

Provides support for locating and running Python scripts using the Python
module namespace instead of the native filesystem.

This allows Python code to play nicely with non-filesystem based PEP 302
importers when locating support scripts as well as when importing modules.
"""
# Written by Nick Coghlan <ncoghlan at gmail.com>
#    to implement PEP 338 (Executing Modules as Scripts)

import sys
import imp
from pkgutil import read_code
try:
    from imp import get_loader
except ImportError:
    from pkgutil import get_loader

__all__ = [
    "run_module", "run_path",
]

class _TempModule(object):
    """Temporarily replace a module in sys.modules with an empty namespace"""
    def __init__(self, mod_name):
        self.mod_name = mod_name
        self.module = imp.new_module(mod_name)
        self._saved_module = []

    def __enter__(self):
        mod_name = self.mod_name
        try:
            self._saved_module.append(sys.modules[mod_name])
        except KeyError:
            pass
        sys.modules[mod_name] = self.module
        return self

    def __exit__(self, *args):
        if self._saved_module:
            sys.modules[self.mod_name] = self._saved_module[0]
        else:
            del sys.modules[self.mod_name]
        self._saved_module = []

class _ModifiedArgv0(object):
    def __init__(self, value):
        self.value = value
        self._saved_value = self._sentinel = object()

    def __enter__(self):
        if self._saved_value is not self._sentinel:
            raise RuntimeError("Already preserving saved value")
        self._saved_value = sys.argv[0]
        sys.argv[0] = self.value

    def __exit__(self, *args):
        self.value = self._sentinel
        sys.argv[0] = self._saved_value

def _run_code(code, run_globals, init_globals=None,
              mod_name=None, mod_fname=None,
              mod_loader=None, pkg_name=None):
    """Helper to run code in nominated namespace"""
    if init_globals is not None:
        run_globals.update(init_globals)
    run_globals.update(__name__ = mod_name,
                       __file__ = mod_fname,
                       __loader__ = mod_loader,
                       __package__ = pkg_name)
    exec code in run_globals
    return run_globals

def _run_module_code(code, init_globals=None,
                    mod_name=None, mod_fname=None,
                    mod_loader=None, pkg_name=None):
    """Helper to run code in new namespace with sys modified"""
    with _TempModule(mod_name) as temp_module, _ModifiedArgv0(mod_fname):
        mod_globals = temp_module.module.__dict__
        _run_code(code, mod_globals, init_globals,
                  mod_name, mod_fname, mod_loader, pkg_name)
    # Copy the globals of the temporary module, as they
    # may be cleared when the temporary module goes away
    return mod_globals.copy()


# This helper is needed due to a missing component in the PEP 302
# loader protocol (specifically, "get_filename" is non-standard)
# Since we can't introduce new features in maintenance releases,
# support was added to zipimporter under the name '_get_filename'
def _get_filename(loader, mod_name):
    for attr in ("get_filename", "_get_filename"):
        meth = getattr(loader, attr, None)
        if meth is not None:
            return meth(mod_name)
    return None

# Helper to get the loader, code and filename for a module
def _get_module_details(mod_name):
    loader = get_loader(mod_name)
    if loader is None:
        raise ImportError("No module named %s" % mod_name)
    if loader.is_package(mod_name):
        if mod_name == "__main__" or mod_name.endswith(".__main__"):
            raise ImportError("Cannot use package as __main__ module")
        try:
            pkg_main_name = mod_name + ".__main__"
            return _get_module_details(pkg_main_name)
        except ImportError, e:
            raise ImportError(("%s; %r is a package and cannot " +
                               "be directly executed") %(e, mod_name))
    code = loader.get_code(mod_name)
    if code is None:
        raise ImportError("No code object available for %s" % mod_name)
    filename = _get_filename(loader, mod_name)
    return mod_name, loader, code, filename


def _get_main_module_details():
    # Helper that gives a nicer error message when attempting to
    # execute a zipfile or directory by invoking __main__.py
    main_name = "__main__"
    try:
        return _get_module_details(main_name)
    except ImportError as exc:
        if main_name in str(exc):
            raise ImportError("can't find %r module in %r" %
                              (main_name, sys.path[0]))
        raise

# This function is the actual implementation of the -m switch and direct
# execution of zipfiles and directories and is deliberately kept private.
# This avoids a repeat of the situation where run_module() no longer met the
# needs of mainmodule.c, but couldn't be changed because it was public
def _run_module_as_main(mod_name, alter_argv=True):
    """Runs the designated module in the __main__ namespace

       Note that the executed module will have full access to the
       __main__ namespace. If this is not desirable, the run_module()
       function should be used to run the module code in a fresh namespace.

       At the very least, these variables in __main__ will be overwritten:
           __name__
           __file__
           __loader__
           __package__
    """
    try:
        if alter_argv or mod_name != "__main__": # i.e. -m switch
            mod_name, loader, code, fname = _get_module_details(mod_name)
        else:          # i.e. directory or zipfile execution
            mod_name, loader, code, fname = _get_main_module_details()
    except ImportError as exc:
        msg = "%s: %s" % (sys.executable, str(exc))
        sys.exit(msg)
    pkg_name = mod_name.rpartition('.')[0]
    main_globals = sys.modules["__main__"].__dict__
    if alter_argv:
        sys.argv[0] = fname
    return _run_code(code, main_globals, None,
                     "__main__", fname, loader, pkg_name)

def run_module(mod_name, init_globals=None,
               run_name=None, alter_sys=False):
    """Execute a module's code without importing it

       Returns the resulting top level namespace dictionary
    """
    mod_name, loader, code, fname = _get_module_details(mod_name)
    if run_name is None:
        run_name = mod_name
    pkg_name = mod_name.rpartition('.')[0]
    if alter_sys:
        return _run_module_code(code, init_globals, run_name,
                                fname, loader, pkg_name)
    else:
        # Leave the sys module alone
        return _run_code(code, {}, init_globals, run_name,
                         fname, loader, pkg_name)


# XXX (ncoghlan): Perhaps expose the C API function
# as imp.get_importer instead of reimplementing it in Python?
def _get_importer(path_name):
    """Python version of PyImport_GetImporter C API function"""
    cache = sys.path_importer_cache
    try:
        importer = cache[path_name]
    except KeyError:
        # Not yet cached. Flag as using the
        # standard machinery until we finish
        # checking the hooks
        cache[path_name] = None
        for hook in sys.path_hooks:
            try:
                importer = hook(path_name)
                break
            except ImportError:
                pass
        else:
            # The following check looks a bit odd. The trick is that
            # NullImporter raises ImportError if the supplied path is a
            # *valid* directory entry (and hence able to be handled
            # by the standard import machinery)
            try:
                importer = imp.NullImporter(path_name)
            except ImportError:
                return None
        cache[path_name] = importer
    return importer

def _get_code_from_file(fname):
    # Check for a compiled file first
    with open(fname, "rb") as f:
        code = read_code(f)
    if code is None:
        # That didn't work, so try it as normal source code
        with open(fname, "rU") as f:
            code = compile(f.read(), fname, 'exec')
    return code

def run_path(path_name, init_globals=None, run_name=None):
    """Execute code located at the specified filesystem location

       Returns the resulting top level namespace dictionary

       The file path may refer directly to a Python script (i.e.
       one that could be directly executed with execfile) or else
       it may refer to a zipfile or directory containing a top
       level __main__.py script.
    """
    if run_name is None:
        run_name = "<run_path>"
    importer = _get_importer(path_name)
    if isinstance(importer, imp.NullImporter):
        # Not a valid sys.path entry, so run the code directly
        # execfile() doesn't help as we want to allow compiled files
        code = _get_code_from_file(path_name)
        return _run_module_code(code, init_globals, run_name, path_name)
    else:
        # Importer is defined for path, so add it to
        # the start of sys.path
        sys.path.insert(0, path_name)
        try:
            # Here's where things are a little different from the run_module
            # case. There, we only had to replace the module in sys while the
            # code was running and doing so was somewhat optional. Here, we
            # have no choice and we have to remove it even while we read the
            # code. If we don't do this, a __loader__ attribute in the
            # existing __main__ module may prevent location of the new module.
            main_name = "__main__"
            saved_main = sys.modules[main_name]
            del sys.modules[main_name]
            try:
                mod_name, loader, code, fname = _get_main_module_details()
            finally:
                sys.modules[main_name] = saved_main
            pkg_name = ""
            with _TempModule(run_name) as temp_module, \
                 _ModifiedArgv0(path_name):
                mod_globals = temp_module.module.__dict__
                return _run_code(code, mod_globals, init_globals,
                                    run_name, fname, loader, pkg_name).copy()
        finally:
            try:
                sys.path.remove(path_name)
            except ValueError:
                pass


if __name__ == "__main__":
    # Run the module specified as the next command line argument
    if len(sys.argv) < 2:
        print >> sys.stderr, "No module specified for execution"
    else:
        del sys.argv[0] # Make the requested module sys.argv[0]
        _run_module_as_main(sys.argv[0])
