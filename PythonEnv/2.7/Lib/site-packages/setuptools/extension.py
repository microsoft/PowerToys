import sys
import distutils.core
import distutils.extension

from setuptools.dist import _get_unpatched

_Extension = _get_unpatched(distutils.core.Extension)

def have_pyrex():
    """
    Return True if Cython or Pyrex can be imported.
    """
    pyrex_impls = 'Cython.Distutils.build_ext', 'Pyrex.Distutils.build_ext'
    for pyrex_impl in pyrex_impls:
        try:
            # from (pyrex_impl) import build_ext
            __import__(pyrex_impl, fromlist=['build_ext']).build_ext
            return True
        except Exception:
            pass
    return False


class Extension(_Extension):
    """Extension that uses '.c' files in place of '.pyx' files"""

    def __init__(self, *args, **kw):
        _Extension.__init__(self, *args, **kw)
        if not have_pyrex():
            self._convert_pyx_sources_to_c()

    def _convert_pyx_sources_to_c(self):
        "convert .pyx extensions to .c"
        def pyx_to_c(source):
            if source.endswith('.pyx'):
                source = source[:-4] + '.c'
            return source
        self.sources = list(map(pyx_to_c, self.sources))

class Library(Extension):
    """Just like a regular Extension, but built as a library instead"""

distutils.core.Extension = Extension
distutils.extension.Extension = Extension
if 'distutils.command.build_ext' in sys.modules:
    sys.modules['distutils.command.build_ext'].Extension = Extension
