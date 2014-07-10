######################################################################
#  This file should be kept compatible with Python 2.3, see PEP 291. #
######################################################################
"""
Generic framework path manipulation
"""

import re

__all__ = ['framework_info']

STRICT_FRAMEWORK_RE = re.compile(r"""(?x)
(?P<location>^.*)(?:^|/)
(?P<name>
    (?P<shortname>\w+).framework/
    (?:Versions/(?P<version>[^/]+)/)?
    (?P=shortname)
    (?:_(?P<suffix>[^_]+))?
)$
""")

def framework_info(filename):
    """
    A framework name can take one of the following four forms:
        Location/Name.framework/Versions/SomeVersion/Name_Suffix
        Location/Name.framework/Versions/SomeVersion/Name
        Location/Name.framework/Name_Suffix
        Location/Name.framework/Name

    returns None if not found, or a mapping equivalent to:
        dict(
            location='Location',
            name='Name.framework/Versions/SomeVersion/Name_Suffix',
            shortname='Name',
            version='SomeVersion',
            suffix='Suffix',
        )

    Note that SomeVersion and Suffix are optional and may be None
    if not present
    """
    is_framework = STRICT_FRAMEWORK_RE.match(filename)
    if not is_framework:
        return None
    return is_framework.groupdict()

def test_framework_info():
    def d(location=None, name=None, shortname=None, version=None, suffix=None):
        return dict(
            location=location,
            name=name,
            shortname=shortname,
            version=version,
            suffix=suffix
        )
    assert framework_info('completely/invalid') is None
    assert framework_info('completely/invalid/_debug') is None
    assert framework_info('P/F.framework') is None
    assert framework_info('P/F.framework/_debug') is None
    assert framework_info('P/F.framework/F') == d('P', 'F.framework/F', 'F')
    assert framework_info('P/F.framework/F_debug') == d('P', 'F.framework/F_debug', 'F', suffix='debug')
    assert framework_info('P/F.framework/Versions') is None
    assert framework_info('P/F.framework/Versions/A') is None
    assert framework_info('P/F.framework/Versions/A/F') == d('P', 'F.framework/Versions/A/F', 'F', 'A')
    assert framework_info('P/F.framework/Versions/A/F_debug') == d('P', 'F.framework/Versions/A/F_debug', 'F', 'A', 'debug')

if __name__ == '__main__':
    test_framework_info()
