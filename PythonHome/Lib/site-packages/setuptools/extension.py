import sys
import re
import functools
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
        self._convert_pyx_sources_to_lang()

    def _convert_pyx_sources_to_lang(self):
        """
        Replace sources with .pyx extensions to sources with the target
        language extension. This mechanism allows language authors to supply
        pre-converted sources but to prefer the .pyx sources.
        """
        if have_pyrex():
            # the build has Cython, so allow it to compile the .pyx files
            return
        lang = self.language or ''
        target_ext = '.cpp' if lang.lower() == 'c++' else '.c'
        sub = functools.partial(re.sub, '.pyx$', target_ext)
        self.sources = list(map(sub, self.sources))

class Library(Extension):
    """Just like a regular Extension, but built as a library instead"""

distutils.core.Extension = Extension
distutils.extension.Extension = Extension
if 'distutils.command.build_ext' in sys.modules:
    sys.modules['distutils.command.build_ext'].Extension = Extension
