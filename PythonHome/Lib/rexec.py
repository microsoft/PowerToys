"""Restricted execution facilities.

The class RExec exports methods r_exec(), r_eval(), r_execfile(), and
r_import(), which correspond roughly to the built-in operations
exec, eval(), execfile() and import, but executing the code in an
environment that only exposes those built-in operations that are
deemed safe.  To this end, a modest collection of 'fake' modules is
created which mimics the standard modules by the same names.  It is a
policy decision which built-in modules and operations are made
available; this module provides a reasonable default, but derived
classes can change the policies e.g. by overriding or extending class
variables like ok_builtin_modules or methods like make_sys().

XXX To do:
- r_open should allow writing tmp dir
- r_exec etc. with explicit globals/locals? (Use rexec("exec ... in ...")?)

"""
from warnings import warnpy3k
warnpy3k("the rexec module has been removed in Python 3.0", stacklevel=2)
del warnpy3k


import sys
import __builtin__
import os
import ihooks
import imp

__all__ = ["RExec"]

class FileBase:

    ok_file_methods = ('fileno', 'flush', 'isatty', 'read', 'readline',
            'readlines', 'seek', 'tell', 'write', 'writelines', 'xreadlines',
            '__iter__')


class FileWrapper(FileBase):

    # XXX This is just like a Bastion -- should use that!

    def __init__(self, f):
        for m in self.ok_file_methods:
            if not hasattr(self, m) and hasattr(f, m):
                setattr(self, m, getattr(f, m))

    def close(self):
        self.flush()


TEMPLATE = """
def %s(self, *args):
        return getattr(self.mod, self.name).%s(*args)
"""

class FileDelegate(FileBase):

    def __init__(self, mod, name):
        self.mod = mod
        self.name = name

    for m in FileBase.ok_file_methods + ('close',):
        exec TEMPLATE % (m, m)


class RHooks(ihooks.Hooks):

    def __init__(self, *args):
        # Hacks to support both old and new interfaces:
        # old interface was RHooks(rexec[, verbose])
        # new interface is RHooks([verbose])
        verbose = 0
        rexec = None
        if args and type(args[-1]) == type(0):
            verbose = args[-1]
            args = args[:-1]
        if args and hasattr(args[0], '__class__'):
            rexec = args[0]
            args = args[1:]
        if args:
            raise TypeError, "too many arguments"
        ihooks.Hooks.__init__(self, verbose)
        self.rexec = rexec

    def set_rexec(self, rexec):
        # Called by RExec instance to complete initialization
        self.rexec = rexec

    def get_suffixes(self):
        return self.rexec.get_suffixes()

    def is_builtin(self, name):
        return self.rexec.is_builtin(name)

    def init_builtin(self, name):
        m = __import__(name)
        return self.rexec.copy_except(m, ())

    def init_frozen(self, name): raise SystemError, "don't use this"
    def load_source(self, *args): raise SystemError, "don't use this"
    def load_compiled(self, *args): raise SystemError, "don't use this"
    def load_package(self, *args): raise SystemError, "don't use this"

    def load_dynamic(self, name, filename, file):
        return self.rexec.load_dynamic(name, filename, file)

    def add_module(self, name):
        return self.rexec.add_module(name)

    def modules_dict(self):
        return self.rexec.modules

    def default_path(self):
        return self.rexec.modules['sys'].path


# XXX Backwards compatibility
RModuleLoader = ihooks.FancyModuleLoader
RModuleImporter = ihooks.ModuleImporter


class RExec(ihooks._Verbose):
    """Basic restricted execution framework.

    Code executed in this restricted environment will only have access to
    modules and functions that are deemed safe; you can subclass RExec to
    add or remove capabilities as desired.

    The RExec class can prevent code from performing unsafe operations like
    reading or writing disk files, or using TCP/IP sockets.  However, it does
    not protect against code using extremely large amounts of memory or
    processor time.

    """

    ok_path = tuple(sys.path)           # That's a policy decision

    ok_builtin_modules = ('audioop', 'array', 'binascii',
                          'cmath', 'errno', 'imageop',
                          'marshal', 'math', 'md5', 'operator',
                          'parser', 'select',
                          'sha', '_sre', 'strop', 'struct', 'time',
                          '_weakref')

    ok_posix_names = ('error', 'fstat', 'listdir', 'lstat', 'readlink',
                      'stat', 'times', 'uname', 'getpid', 'getppid',
                      'getcwd', 'getuid', 'getgid', 'geteuid', 'getegid')

    ok_sys_names = ('byteorder', 'copyright', 'exit', 'getdefaultencoding',
                    'getrefcount', 'hexversion', 'maxint', 'maxunicode',
                    'platform', 'ps1', 'ps2', 'version', 'version_info')

    nok_builtin_names = ('open', 'file', 'reload', '__import__')

    ok_file_types = (imp.C_EXTENSION, imp.PY_SOURCE)

    def __init__(self, hooks = None, verbose = 0):
        """Returns an instance of the RExec class.

        The hooks parameter is an instance of the RHooks class or a subclass
        of it.  If it is omitted or None, the default RHooks class is
        instantiated.

        Whenever the RExec module searches for a module (even a built-in one)
        or reads a module's code, it doesn't actually go out to the file
        system itself.  Rather, it calls methods of an RHooks instance that
        was passed to or created by its constructor.  (Actually, the RExec
        object doesn't make these calls --- they are made by a module loader
        object that's part of the RExec object.  This allows another level of
        flexibility, which can be useful when changing the mechanics of
        import within the restricted environment.)

        By providing an alternate RHooks object, we can control the file
        system accesses made to import a module, without changing the
        actual algorithm that controls the order in which those accesses are
        made.  For instance, we could substitute an RHooks object that
        passes all filesystem requests to a file server elsewhere, via some
        RPC mechanism such as ILU.  Grail's applet loader uses this to support
        importing applets from a URL for a directory.

        If the verbose parameter is true, additional debugging output may be
        sent to standard output.

        """

        raise RuntimeError, "This code is not secure in Python 2.2 and later"

        ihooks._Verbose.__init__(self, verbose)
        # XXX There's a circular reference here:
        self.hooks = hooks or RHooks(verbose)
        self.hooks.set_rexec(self)
        self.modules = {}
        self.ok_dynamic_modules = self.ok_builtin_modules
        list = []
        for mname in self.ok_builtin_modules:
            if mname in sys.builtin_module_names:
                list.append(mname)
        self.ok_builtin_modules = tuple(list)
        self.set_trusted_path()
        self.make_builtin()
        self.make_initial_modules()
        # make_sys must be last because it adds the already created
        # modules to its builtin_module_names
        self.make_sys()
        self.loader = RModuleLoader(self.hooks, verbose)
        self.importer = RModuleImporter(self.loader, verbose)

    def set_trusted_path(self):
        # Set the path from which dynamic modules may be loaded.
        # Those dynamic modules must also occur in ok_builtin_modules
        self.trusted_path = filter(os.path.isabs, sys.path)

    def load_dynamic(self, name, filename, file):
        if name not in self.ok_dynamic_modules:
            raise ImportError, "untrusted dynamic module: %s" % name
        if name in sys.modules:
            src = sys.modules[name]
        else:
            src = imp.load_dynamic(name, filename, file)
        dst = self.copy_except(src, [])
        return dst

    def make_initial_modules(self):
        self.make_main()
        self.make_osname()

    # Helpers for RHooks

    def get_suffixes(self):
        return [item   # (suff, mode, type)
                for item in imp.get_suffixes()
                if item[2] in self.ok_file_types]

    def is_builtin(self, mname):
        return mname in self.ok_builtin_modules

    # The make_* methods create specific built-in modules

    def make_builtin(self):
        m = self.copy_except(__builtin__, self.nok_builtin_names)
        m.__import__ = self.r_import
        m.reload = self.r_reload
        m.open = m.file = self.r_open

    def make_main(self):
        self.add_module('__main__')

    def make_osname(self):
        osname = os.name
        src = __import__(osname)
        dst = self.copy_only(src, self.ok_posix_names)
        dst.environ = e = {}
        for key, value in os.environ.items():
            e[key] = value

    def make_sys(self):
        m = self.copy_only(sys, self.ok_sys_names)
        m.modules = self.modules
        m.argv = ['RESTRICTED']
        m.path = map(None, self.ok_path)
        m.exc_info = self.r_exc_info
        m = self.modules['sys']
        l = self.modules.keys() + list(self.ok_builtin_modules)
        l.sort()
        m.builtin_module_names = tuple(l)

    # The copy_* methods copy existing modules with some changes

    def copy_except(self, src, exceptions):
        dst = self.copy_none(src)
        for name in dir(src):
            setattr(dst, name, getattr(src, name))
        for name in exceptions:
            try:
                delattr(dst, name)
            except AttributeError:
                pass
        return dst

    def copy_only(self, src, names):
        dst = self.copy_none(src)
        for name in names:
            try:
                value = getattr(src, name)
            except AttributeError:
                continue
            setattr(dst, name, value)
        return dst

    def copy_none(self, src):
        m = self.add_module(src.__name__)
        m.__doc__ = src.__doc__
        return m

    # Add a module -- return an existing module or create one

    def add_module(self, mname):
        m = self.modules.get(mname)
        if m is None:
            self.modules[mname] = m = self.hooks.new_module(mname)
        m.__builtins__ = self.modules['__builtin__']
        return m

    # The r* methods are public interfaces

    def r_exec(self, code):
        """Execute code within a restricted environment.

        The code parameter must either be a string containing one or more
        lines of Python code, or a compiled code object, which will be
        executed in the restricted environment's __main__ module.

        """
        m = self.add_module('__main__')
        exec code in m.__dict__

    def r_eval(self, code):
        """Evaluate code within a restricted environment.

        The code parameter must either be a string containing a Python
        expression, or a compiled code object, which will be evaluated in
        the restricted environment's __main__ module.  The value of the
        expression or code object will be returned.

        """
        m = self.add_module('__main__')
        return eval(code, m.__dict__)

    def r_execfile(self, file):
        """Execute the Python code in the file in the restricted
        environment's __main__ module.

        """
        m = self.add_module('__main__')
        execfile(file, m.__dict__)

    def r_import(self, mname, globals={}, locals={}, fromlist=[]):
        """Import a module, raising an ImportError exception if the module
        is considered unsafe.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        """
        return self.importer.import_module(mname, globals, locals, fromlist)

    def r_reload(self, m):
        """Reload the module object, re-parsing and re-initializing it.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        """
        return self.importer.reload(m)

    def r_unload(self, m):
        """Unload the module.

        Removes it from the restricted environment's sys.modules dictionary.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        """
        return self.importer.unload(m)

    # The s_* methods are similar but also swap std{in,out,err}

    def make_delegate_files(self):
        s = self.modules['sys']
        self.delegate_stdin = FileDelegate(s, 'stdin')
        self.delegate_stdout = FileDelegate(s, 'stdout')
        self.delegate_stderr = FileDelegate(s, 'stderr')
        self.restricted_stdin = FileWrapper(sys.stdin)
        self.restricted_stdout = FileWrapper(sys.stdout)
        self.restricted_stderr = FileWrapper(sys.stderr)

    def set_files(self):
        if not hasattr(self, 'save_stdin'):
            self.save_files()
        if not hasattr(self, 'delegate_stdin'):
            self.make_delegate_files()
        s = self.modules['sys']
        s.stdin = self.restricted_stdin
        s.stdout = self.restricted_stdout
        s.stderr = self.restricted_stderr
        sys.stdin = self.delegate_stdin
        sys.stdout = self.delegate_stdout
        sys.stderr = self.delegate_stderr

    def reset_files(self):
        self.restore_files()
        s = self.modules['sys']
        self.restricted_stdin = s.stdin
        self.restricted_stdout = s.stdout
        self.restricted_stderr = s.stderr


    def save_files(self):
        self.save_stdin = sys.stdin
        self.save_stdout = sys.stdout
        self.save_stderr = sys.stderr

    def restore_files(self):
        sys.stdin = self.save_stdin
        sys.stdout = self.save_stdout
        sys.stderr = self.save_stderr

    def s_apply(self, func, args=(), kw={}):
        self.save_files()
        try:
            self.set_files()
            r = func(*args, **kw)
        finally:
            self.restore_files()
        return r

    def s_exec(self, *args):
        """Execute code within a restricted environment.

        Similar to the r_exec() method, but the code will be granted access
        to restricted versions of the standard I/O streams sys.stdin,
        sys.stderr, and sys.stdout.

        The code parameter must either be a string containing one or more
        lines of Python code, or a compiled code object, which will be
        executed in the restricted environment's __main__ module.

        """
        return self.s_apply(self.r_exec, args)

    def s_eval(self, *args):
        """Evaluate code within a restricted environment.

        Similar to the r_eval() method, but the code will be granted access
        to restricted versions of the standard I/O streams sys.stdin,
        sys.stderr, and sys.stdout.

        The code parameter must either be a string containing a Python
        expression, or a compiled code object, which will be evaluated in
        the restricted environment's __main__ module.  The value of the
        expression or code object will be returned.

        """
        return self.s_apply(self.r_eval, args)

    def s_execfile(self, *args):
        """Execute the Python code in the file in the restricted
        environment's __main__ module.

        Similar to the r_execfile() method, but the code will be granted
        access to restricted versions of the standard I/O streams sys.stdin,
        sys.stderr, and sys.stdout.

        """
        return self.s_apply(self.r_execfile, args)

    def s_import(self, *args):
        """Import a module, raising an ImportError exception if the module
        is considered unsafe.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        Similar to the r_import() method, but has access to restricted
        versions of the standard I/O streams sys.stdin, sys.stderr, and
        sys.stdout.

        """
        return self.s_apply(self.r_import, args)

    def s_reload(self, *args):
        """Reload the module object, re-parsing and re-initializing it.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        Similar to the r_reload() method, but has access to restricted
        versions of the standard I/O streams sys.stdin, sys.stderr, and
        sys.stdout.

        """
        return self.s_apply(self.r_reload, args)

    def s_unload(self, *args):
        """Unload the module.

        Removes it from the restricted environment's sys.modules dictionary.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        Similar to the r_unload() method, but has access to restricted
        versions of the standard I/O streams sys.stdin, sys.stderr, and
        sys.stdout.

        """
        return self.s_apply(self.r_unload, args)

    # Restricted open(...)

    def r_open(self, file, mode='r', buf=-1):
        """Method called when open() is called in the restricted environment.

        The arguments are identical to those of the open() function, and a
        file object (or a class instance compatible with file objects)
        should be returned.  RExec's default behaviour is allow opening
        any file for reading, but forbidding any attempt to write a file.

        This method is implicitly called by code executing in the
        restricted environment.  Overriding this method in a subclass is
        used to change the policies enforced by a restricted environment.

        """
        mode = str(mode)
        if mode not in ('r', 'rb'):
            raise IOError, "can't open files for writing in restricted mode"
        return open(file, mode, buf)

    # Restricted version of sys.exc_info()

    def r_exc_info(self):
        ty, va, tr = sys.exc_info()
        tr = None
        return ty, va, tr


def test():
    import getopt, traceback
    opts, args = getopt.getopt(sys.argv[1:], 'vt:')
    verbose = 0
    trusted = []
    for o, a in opts:
        if o == '-v':
            verbose = verbose+1
        if o == '-t':
            trusted.append(a)
    r = RExec(verbose=verbose)
    if trusted:
        r.ok_builtin_modules = r.ok_builtin_modules + tuple(trusted)
    if args:
        r.modules['sys'].argv = args
        r.modules['sys'].path.insert(0, os.path.dirname(args[0]))
    else:
        r.modules['sys'].path.insert(0, "")
    fp = sys.stdin
    if args and args[0] != '-':
        try:
            fp = open(args[0])
        except IOError, msg:
            print "%s: can't open file %r" % (sys.argv[0], args[0])
            return 1
    if fp.isatty():
        try:
            import readline
        except ImportError:
            pass
        import code
        class RestrictedConsole(code.InteractiveConsole):
            def runcode(self, co):
                self.locals['__builtins__'] = r.modules['__builtin__']
                r.s_apply(code.InteractiveConsole.runcode, (self, co))
        try:
            RestrictedConsole(r.modules['__main__'].__dict__).interact()
        except SystemExit, n:
            return n
    else:
        text = fp.read()
        fp.close()
        c = compile(text, fp.name, 'exec')
        try:
            r.s_exec(c)
        except SystemExit, n:
            return n
        except:
            traceback.print_exc()
            return 1


if __name__ == '__main__':
    sys.exit(test())
