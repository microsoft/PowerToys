"""shared options and groups"""
from optparse import make_option, OptionGroup
from pip.locations import build_prefix


def make_option_group(group, parser):
    """
    Return an OptionGroup object
    group  -- assumed to be dict with 'name' and 'options' keys
    parser -- an optparse Parser
    """
    option_group = OptionGroup(parser, group['name'])
    for option in group['options']:
        option_group.add_option(option)
    return option_group

###########
# options #
###########

index_url = make_option(
    '-i', '--index-url', '--pypi-url',
    dest='index_url',
    metavar='URL',
    default='https://pypi.python.org/simple/',
    help='Base URL of Python Package Index (default %default).')

extra_index_url = make_option(
    '--extra-index-url',
    dest='extra_index_urls',
    metavar='URL',
    action='append',
    default=[],
    help='Extra URLs of package indexes to use in addition to --index-url.')

no_index = make_option(
    '--no-index',
    dest='no_index',
    action='store_true',
    default=False,
    help='Ignore package index (only looking at --find-links URLs instead).')

find_links =  make_option(
    '-f', '--find-links',
    dest='find_links',
    action='append',
    default=[],
    metavar='url',
    help="If a url or path to an html file, then parse for links to archives. If a local path or file:// url that's a directory, then look for archives in the directory listing.")

use_mirrors = make_option(
    '-M', '--use-mirrors',
    dest='use_mirrors',
    action='store_true',
    default=False,
    help='Use the PyPI mirrors as a fallback in case the main index is down.')

mirrors = make_option(
    '--mirrors',
    dest='mirrors',
    metavar='URL',
    action='append',
    default=[],
    help='Specific mirror URLs to query when --use-mirrors is used.')

allow_external = make_option(
    "--allow-external",
    dest="allow_external",
    action="append",
    default=[],
    metavar="PACKAGE",
    help="Allow the installation of externally hosted files",
)

allow_all_external = make_option(
    "--allow-all-external",
    dest="allow_all_external",
    action="store_true",
    default=True,  # TODO: Change to False after 1.4 has been released
    help="Allow the installation of all externally hosted files",
)

# TODO: NOOP after 1.4 has been released
no_allow_external = make_option(
    "--no-allow-external",
    dest="allow_all_external",
    action="store_false",
    help="Disallow the installation of all externally hosted files",
)

allow_unsafe = make_option(
    "--allow-insecure",
    dest="allow_insecure",
    action="append",
    default=[],
    metavar="PACKAGE",
    help="Allow the installation of insecure and unverifiable files",
)

no_allow_unsafe = make_option(
    "--no-allow-insecure",
    dest="allow_all_insecure",
    action="store_false",
    default=True,
    help="Disallow the installation of insecure and unverifiable files"
)

requirements = make_option(
    '-r', '--requirement',
    dest='requirements',
    action='append',
    default=[],
    metavar='file',
    help='Install from the given requirements file. '
    'This option can be used multiple times.')

use_wheel = make_option(
    '--use-wheel',
    dest='use_wheel',
    action='store_true',
    help='Find and prefer wheel archives when searching indexes and find-links locations. Default to accepting source archives.')

download_cache = make_option(
    '--download-cache',
    dest='download_cache',
    metavar='dir',
    default=None,
    help='Cache downloaded packages in <dir>.')

no_deps = make_option(
    '--no-deps', '--no-dependencies',
    dest='ignore_dependencies',
    action='store_true',
    default=False,
    help="Don't install package dependencies.")

build_dir = make_option(
    '-b', '--build', '--build-dir', '--build-directory',
    dest='build_dir',
    metavar='dir',
    default=build_prefix,
    help='Directory to unpack packages into and build in. '
    'The default in a virtualenv is "<venv path>/build". '
    'The default for global installs is "<OS temp dir>/pip_build_<username>".')

install_options = make_option(
    '--install-option',
    dest='install_options',
    action='append',
    metavar='options',
    help="Extra arguments to be supplied to the setup.py install "
    "command (use like --install-option=\"--install-scripts=/usr/local/bin\"). "
    "Use multiple --install-option options to pass multiple options to setup.py install. "
    "If you are using an option with a directory path, be sure to use absolute path.")

global_options = make_option(
    '--global-option',
    dest='global_options',
    action='append',
    metavar='options',
    help="Extra global options to be supplied to the setup.py "
    "call before the install command.")

no_clean = make_option(
    '--no-clean',
    action='store_true',
    default=False,
    help="Don't clean up build directories.")


##########
# groups #
##########

index_group = {
    'name': 'Package Index Options',
    'options': [
        index_url,
        extra_index_url,
        no_index,
        find_links,
        use_mirrors,
        mirrors,
        allow_external,
        allow_all_external,
        no_allow_external,
        allow_unsafe,
        no_allow_unsafe,
        ]
    }
