from __future__ import generators
import sys, imp, marshal
from imp import PKG_DIRECTORY, PY_COMPILED, PY_SOURCE, PY_FROZEN
from distutils.version import StrictVersion, LooseVersion

__all__ = [
    'Require', 'find_module', 'get_module_constant', 'extract_constant'
]

class Require:
    """A prerequisite to building or installing a distribution"""

    def __init__(self,name,requested_version,module,homepage='',
        attribute=None,format=None
    ):

        if format is None and requested_version is not None:
            format = StrictVersion

        if format is not None:
            requested_version = format(requested_version)
            if attribute is None:
                attribute = '__version__'

        self.__dict__.update(locals())
        del self.self


    def full_name(self):
        """Return full package/distribution name, w/version"""
        if self.requested_version is not None:
            return '%s-%s' % (self.name,self.requested_version)
        return self.name


    def version_ok(self,version):
        """Is 'version' sufficiently up-to-date?"""
        return self.attribute is None or self.format is None or \
            str(version) != "unknown" and version >= self.requested_version


    def get_version(self, paths=None, default="unknown"):

        """Get version number of installed module, 'None', or 'default'

        Search 'paths' for module.  If not found, return 'None'.  If found,
        return the extracted version attribute, or 'default' if no version
        attribute was specified, or the value cannot be determined without
        importing the module.  The version is formatted according to the
        requirement's version format (if any), unless it is 'None' or the
        supplied 'default'.
        """

        if self.attribute is None:
            try:
                f,p,i = find_module(self.module,paths)
                if f: f.close()
                return default
            except ImportError:
                return None

        v = get_module_constant(self.module,self.attribute,default,paths)

        if v is not None and v is not default and self.format is not None:
            return self.format(v)

        return v


    def is_present(self,paths=None):
        """Return true if dependency is present on 'paths'"""
        return self.get_version(paths) is not None


    def is_current(self,paths=None):
        """Return true if dependency is present and up-to-date on 'paths'"""
        version = self.get_version(paths)
        if version is None:
            return False
        return self.version_ok(version)


def _iter_code(code):

    """Yield '(op,arg)' pair for each operation in code object 'code'"""

    from array import array
    from dis import HAVE_ARGUMENT, EXTENDED_ARG

    bytes = array('b',code.co_code)
    eof = len(code.co_code)

    ptr = 0
    extended_arg = 0

    while ptr<eof:

        op = bytes[ptr]

        if op>=HAVE_ARGUMENT:

            arg = bytes[ptr+1] + bytes[ptr+2]*256 + extended_arg
            ptr += 3

            if op==EXTENDED_ARG:
                extended_arg = arg * long_type(65536)
                continue

        else:
            arg = None
            ptr += 1

        yield op,arg










def find_module(module, paths=None):
    """Just like 'imp.find_module()', but with package support"""

    parts = module.split('.')

    while parts:
        part = parts.pop(0)
        f, path, (suffix,mode,kind) = info = imp.find_module(part, paths)

        if kind==PKG_DIRECTORY:
            parts = parts or ['__init__']
            paths = [path]

        elif parts:
            raise ImportError("Can't find %r in %s" % (parts,module))

    return info
























def get_module_constant(module, symbol, default=-1, paths=None):

    """Find 'module' by searching 'paths', and extract 'symbol'

    Return 'None' if 'module' does not exist on 'paths', or it does not define
    'symbol'.  If the module defines 'symbol' as a constant, return the
    constant.  Otherwise, return 'default'."""

    try:
        f, path, (suffix,mode,kind) = find_module(module,paths)
    except ImportError:
        # Module doesn't exist
        return None

    try:
        if kind==PY_COMPILED:
            f.read(8)   # skip magic & date
            code = marshal.load(f)
        elif kind==PY_FROZEN:
            code = imp.get_frozen_object(module)
        elif kind==PY_SOURCE:
            code = compile(f.read(), path, 'exec')
        else:
            # Not something we can parse; we'll have to import it.  :(
            if module not in sys.modules:
                imp.load_module(module,f,path,(suffix,mode,kind))
            return getattr(sys.modules[module],symbol,None)

    finally:
        if f:
            f.close()

    return extract_constant(code,symbol,default)








def extract_constant(code,symbol,default=-1):
    """Extract the constant value of 'symbol' from 'code'

    If the name 'symbol' is bound to a constant value by the Python code
    object 'code', return that value.  If 'symbol' is bound to an expression,
    return 'default'.  Otherwise, return 'None'.

    Return value is based on the first assignment to 'symbol'.  'symbol' must
    be a global, or at least a non-"fast" local in the code block.  That is,
    only 'STORE_NAME' and 'STORE_GLOBAL' opcodes are checked, and 'symbol'
    must be present in 'code.co_names'.
    """

    if symbol not in code.co_names:
        # name's not there, can't possibly be an assigment
        return None

    name_idx = list(code.co_names).index(symbol)

    STORE_NAME = 90
    STORE_GLOBAL = 97
    LOAD_CONST = 100

    const = default

    for op, arg in _iter_code(code):

        if op==LOAD_CONST:
            const = code.co_consts[arg]
        elif arg==name_idx and (op==STORE_NAME or op==STORE_GLOBAL):
            return const
        else:
            const = default
            
if sys.platform.startswith('java') or sys.platform == 'cli':
    # XXX it'd be better to test assertions about bytecode instead...
    del extract_constant, get_module_constant
    __all__.remove('extract_constant')
    __all__.remove('get_module_constant')


