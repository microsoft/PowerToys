"""Base option parser setup"""

import sys
import optparse
import pkg_resources
import os
import textwrap
from distutils.util import strtobool
from pip.backwardcompat import ConfigParser, string_types
from pip.locations import default_config_file, default_log_file
from pip.util import get_terminal_size, get_prog


class PrettyHelpFormatter(optparse.IndentedHelpFormatter):
    """A prettier/less verbose help formatter for optparse."""

    def __init__(self, *args, **kwargs):
        # help position must be aligned with __init__.parseopts.description
        kwargs['max_help_position'] = 30
        kwargs['indent_increment'] = 1
        kwargs['width'] = get_terminal_size()[0] - 2
        optparse.IndentedHelpFormatter.__init__(self, *args, **kwargs)

    def format_option_strings(self, option):
        return self._format_option_strings(option, ' <%s>', ', ')

    def _format_option_strings(self, option, mvarfmt=' <%s>', optsep=', '):
        """
        Return a comma-separated list of option strings and metavars.

        :param option:  tuple of (short opt, long opt), e.g: ('-f', '--format')
        :param mvarfmt: metavar format string - evaluated as mvarfmt % metavar
        :param optsep:  separator
        """
        opts = []

        if option._short_opts:
            opts.append(option._short_opts[0])
        if option._long_opts:
            opts.append(option._long_opts[0])
        if len(opts) > 1:
            opts.insert(1, optsep)

        if option.takes_value():
            metavar = option.metavar or option.dest.lower()
            opts.append(mvarfmt % metavar.lower())

        return ''.join(opts)

    def format_heading(self, heading):
        if heading == 'Options':
            return ''
        return heading + ':\n'

    def format_usage(self, usage):
        """
        Ensure there is only one newline between usage and the first heading
        if there is no description.
        """
        msg = '\nUsage: %s\n' % self.indent_lines(textwrap.dedent(usage), "  ")
        return msg

    def format_description(self, description):
        # leave full control over description to us
        if description:
            if hasattr(self.parser, 'main'):
                label = 'Commands'
            else:
                label = 'Description'
            #some doc strings have inital newlines, some don't
            description = description.lstrip('\n')
            #some doc strings have final newlines and spaces, some don't
            description = description.rstrip()
            #dedent, then reindent
            description = self.indent_lines(textwrap.dedent(description), "  ")
            description = '%s:\n%s\n' % (label, description)
            return description
        else:
            return ''

    def format_epilog(self, epilog):
        # leave full control over epilog to us
        if epilog:
            return epilog
        else:
            return ''

    def indent_lines(self, text, indent):
        new_lines = [indent + line for line in text.split('\n')]
        return "\n".join(new_lines)


class UpdatingDefaultsHelpFormatter(PrettyHelpFormatter):
    """Custom help formatter for use in ConfigOptionParser that updates
    the defaults before expanding them, allowing them to show up correctly
    in the help listing"""

    def expand_default(self, option):
        if self.parser is not None:
            self.parser.update_defaults(self.parser.defaults)
        return optparse.IndentedHelpFormatter.expand_default(self, option)


class CustomOptionParser(optparse.OptionParser):
    def insert_option_group(self, idx, *args, **kwargs):
        """Insert an OptionGroup at a given position."""
        group = self.add_option_group(*args, **kwargs)

        self.option_groups.pop()
        self.option_groups.insert(idx, group)

        return group

    @property
    def option_list_all(self):
        """Get a list of all options, including those in option groups."""
        res = self.option_list[:]
        for i in self.option_groups:
            res.extend(i.option_list)

        return res


class ConfigOptionParser(CustomOptionParser):
    """Custom option parser which updates its defaults by by checking the
    configuration files and environmental variables"""

    def __init__(self, *args, **kwargs):
        self.config = ConfigParser.RawConfigParser()
        self.name = kwargs.pop('name')
        self.files = self.get_config_files()
        self.config.read(self.files)
        assert self.name
        optparse.OptionParser.__init__(self, *args, **kwargs)

    def get_config_files(self):
        config_file = os.environ.get('PIP_CONFIG_FILE', False)
        if config_file and os.path.exists(config_file):
            return [config_file]
        return [default_config_file]

    def update_defaults(self, defaults):
        """Updates the given defaults with values from the config files and
        the environ. Does a little special handling for certain types of
        options (lists)."""
        # Then go and look for the other sources of configuration:
        config = {}
        # 1. config files
        for section in ('global', self.name):
            config.update(self.normalize_keys(self.get_config_section(section)))
        # 2. environmental variables
        config.update(self.normalize_keys(self.get_environ_vars()))
        # Then set the options with those values
        for key, val in config.items():
            option = self.get_option(key)
            if option is not None:
                # ignore empty values
                if not val:
                    continue
                # handle multiline configs
                if option.action == 'append':
                    val = val.split()
                else:
                    option.nargs = 1
                if option.action in ('store_true', 'store_false', 'count'):
                    val = strtobool(val)
                try:
                    val = option.convert_value(key, val)
                except optparse.OptionValueError:
                    e = sys.exc_info()[1]
                    print("An error occurred during configuration: %s" % e)
                    sys.exit(3)
                defaults[option.dest] = val
        return defaults

    def normalize_keys(self, items):
        """Return a config dictionary with normalized keys regardless of
        whether the keys were specified in environment variables or in config
        files"""
        normalized = {}
        for key, val in items:
            key = key.replace('_', '-')
            if not key.startswith('--'):
                key = '--%s' % key  # only prefer long opts
            normalized[key] = val
        return normalized

    def get_config_section(self, name):
        """Get a section of a configuration"""
        if self.config.has_section(name):
            return self.config.items(name)
        return []

    def get_environ_vars(self, prefix='PIP_'):
        """Returns a generator with all environmental vars with prefix PIP_"""
        for key, val in os.environ.items():
            if key.startswith(prefix):
                yield (key.replace(prefix, '').lower(), val)

    def get_default_values(self):
        """Overridding to make updating the defaults after instantiation of
        the option parser possible, update_defaults() does the dirty work."""
        if not self.process_default_values:
            # Old, pre-Optik 1.5 behaviour.
            return optparse.Values(self.defaults)

        defaults = self.update_defaults(self.defaults.copy())  # ours
        for option in self._get_all_options():
            default = defaults.get(option.dest)
            if isinstance(default, string_types):
                opt_str = option.get_opt_string()
                defaults[option.dest] = option.check_value(opt_str, default)
        return optparse.Values(defaults)

    def error(self, msg):
        self.print_usage(sys.stderr)
        self.exit(2, "%s\n" % msg)


try:
    pip_dist = pkg_resources.get_distribution('pip')
    version = '%s from %s (python %s)' % (
        pip_dist, pip_dist.location, sys.version[:3])
except pkg_resources.DistributionNotFound:
    # when running pip.py without installing
    version = None


def create_main_parser():
    parser_kw = {
        'usage': '\n%prog <command> [options]',
        'add_help_option': False,
        'formatter': UpdatingDefaultsHelpFormatter(),
        'name': 'global',
        'prog': get_prog(),
    }

    parser = ConfigOptionParser(**parser_kw)
    genopt = optparse.OptionGroup(parser, 'General Options')
    parser.disable_interspersed_args()

    # having a default version action just causes trouble
    parser.version = version

    for opt in standard_options:
        genopt.add_option(opt)
    parser.add_option_group(genopt)

    return parser


standard_options = [
    optparse.make_option(
        '-h', '--help',
        dest='help',
        action='help',
        help='Show help.'),

    optparse.make_option(
        # Run only if inside a virtualenv, bail if not.
        '--require-virtualenv', '--require-venv',
        dest='require_venv',
        action='store_true',
        default=False,
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        '-v', '--verbose',
        dest='verbose',
        action='count',
        default=0,
        help='Give more output. Option is additive, and can be used up to 3 times.'),

    optparse.make_option(
        '-V', '--version',
        dest='version',
        action='store_true',
        help='Show version and exit.'),

    optparse.make_option(
        '-q', '--quiet',
        dest='quiet',
        action='count',
        default=0,
        help='Give less output.'),

    optparse.make_option(
        '--log',
        dest='log',
        metavar='file',
        help='Log file where a complete (maximum verbosity) record will be kept.'),

    optparse.make_option(
        # Writes the log levels explicitely to the log'
        '--log-explicit-levels',
        dest='log_explicit_levels',
        action='store_true',
        default=False,
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        # The default log file
        '--local-log', '--log-file',
        dest='log_file',
        metavar='file',
        default=default_log_file,
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        # Don't ask for input
        '--no-input',
        dest='no_input',
        action='store_true',
        default=False,
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        '--proxy',
        dest='proxy',
        type='str',
        default='',
        help="Specify a proxy in the form [user:passwd@]proxy.server:port."),

    optparse.make_option(
        '--timeout', '--default-timeout',
        metavar='sec',
        dest='timeout',
        type='float',
        default=15,
        help='Set the socket timeout (default %default seconds).'),

    optparse.make_option(
        # The default version control system for editables, e.g. 'svn'
        '--default-vcs',
        dest='default_vcs',
        type='str',
        default='',
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        # A regex to be used to skip requirements
        '--skip-requirements-regex',
        dest='skip_requirements_regex',
        type='str',
        default='',
        help=optparse.SUPPRESS_HELP),

    optparse.make_option(
        # Option when path already exist
        '--exists-action',
        dest='exists_action',
        type='choice',
        choices=['s', 'i', 'w', 'b'],
        default=[],
        action='append',
        metavar='action',
        help="Default action when a path already exists: "
             "(s)witch, (i)gnore, (w)ipe, (b)ackup."),

    optparse.make_option(
        '--cert',
        dest='cert',
        type='str',
        default='',
        metavar='path',
        help = "Path to alternate CA bundle."),

    ]
