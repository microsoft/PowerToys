"""Generate and work with PEP 425 Compatibility Tags."""

import sys
import warnings

try:
    import sysconfig
except ImportError:  # pragma nocover
    # Python < 2.7
    import distutils.sysconfig as sysconfig
import distutils.util


def get_abbr_impl():
    """Return abbreviated implementation name."""
    if hasattr(sys, 'pypy_version_info'):
        pyimpl = 'pp'
    elif sys.platform.startswith('java'):
        pyimpl = 'jy'
    elif sys.platform == 'cli':
        pyimpl = 'ip'
    else:
        pyimpl = 'cp'
    return pyimpl


def get_impl_ver():
    """Return implementation version."""
    return ''.join(map(str, sys.version_info[:2]))


def get_platform():
    """Return our platform name 'win32', 'linux_x86_64'"""
    # XXX remove distutils dependency
    return distutils.util.get_platform().replace('.', '_').replace('-', '_')


def get_supported(versions=None, noarch=False):
    """Return a list of supported tags for each version specified in
    `versions`.

    :param versions: a list of string versions, of the form ["33", "32"],
        or None. The first version will be assumed to support our ABI.
    """
    supported = []

    # Versions must be given with respect to the preference
    if versions is None:
        versions = []
        major = sys.version_info[0]
        # Support all previous minor Python versions.
        for minor in range(sys.version_info[1], -1, -1):
            versions.append(''.join(map(str, (major, minor))))

    impl = get_abbr_impl()

    abis = []

    try:
        soabi = sysconfig.get_config_var('SOABI')
    except IOError as e: # Issue #1074
        warnings.warn("{0}".format(e), RuntimeWarning)
        soabi = None

    if soabi and soabi.startswith('cpython-'):
        abis[0:0] = ['cp' + soabi.split('-', 1)[-1]]

    abi3s = set()
    import imp
    for suffix in imp.get_suffixes():
        if suffix[0].startswith('.abi'):
            abi3s.add(suffix[0].split('.', 2)[1])

    abis.extend(sorted(list(abi3s)))

    abis.append('none')

    if not noarch:
        arch = get_platform()

        # Current version, current API (built specifically for our Python):
        for abi in abis:
            supported.append(('%s%s' % (impl, versions[0]), abi, arch))

    # No abi / arch, but requires our implementation:
    for i, version in enumerate(versions):
        supported.append(('%s%s' % (impl, version), 'none', 'any'))
        if i == 0:
            # Tagged specifically as being cross-version compatible
            # (with just the major version specified)
            supported.append(('%s%s' % (impl, versions[0][0]), 'none', 'any'))

    # No abi / arch, generic Python
    for i, version in enumerate(versions):
        supported.append(('py%s' % (version,), 'none', 'any'))
        if i == 0:
            supported.append(('py%s' % (version[0]), 'none', 'any'))

    return supported

supported_tags = get_supported()
supported_tags_noarch = get_supported(noarch=True)
