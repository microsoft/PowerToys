######################################################################
#  This file should be kept compatible with Python 2.3, see PEP 291. #
######################################################################
"""
Generic dylib path manipulation
"""

import re

__all__ = ['dylib_info']

DYLIB_RE = re.compile(r"""(?x)
(?P<location>^.*)(?:^|/)
(?P<name>
    (?P<shortname>\w+?)
    (?:\.(?P<version>[^._]+))?
    (?:_(?P<suffix>[^._]+))?
    \.dylib$
)
""")

def dylib_info(filename):
    """
    A dylib name can take one of the following four forms:
        Location/Name.SomeVersion_Suffix.dylib
        Location/Name.SomeVersion.dylib
        Location/Name_Suffix.dylib
        Location/Name.dylib

    returns None if not found or a mapping equivalent to:
        dict(
            location='Location',
            name='Name.SomeVersion_Suffix.dylib',
            shortname='Name',
            version='SomeVersion',
            suffix='Suffix',
        )

    Note that SomeVersion and Suffix are optional and may be None
    if not present.
    """
    is_dylib = DYLIB_RE.match(filename)
    if not is_dylib:
        return None
    return is_dylib.groupdict()


def test_dylib_info():
    def d(location=None, name=None, shortname=None, version=None, suffix=None):
        return dict(
            location=location,
            name=name,
            shortname=shortname,
            version=version,
            suffix=suffix
        )
    assert dylib_info('completely/invalid') is None
    assert dylib_info('completely/invalide_debug') is None
    assert dylib_info('P/Foo.dylib') == d('P', 'Foo.dylib', 'Foo')
    assert dylib_info('P/Foo_debug.dylib') == d('P', 'Foo_debug.dylib', 'Foo', suffix='debug')
    assert dylib_info('P/Foo.A.dylib') == d('P', 'Foo.A.dylib', 'Foo', 'A')
    assert dylib_info('P/Foo_debug.A.dylib') == d('P', 'Foo_debug.A.dylib', 'Foo_debug', 'A')
    assert dylib_info('P/Foo.A_debug.dylib') == d('P', 'Foo.A_debug.dylib', 'Foo', 'A', 'debug')

if __name__ == '__main__':
    test_dylib_info()
