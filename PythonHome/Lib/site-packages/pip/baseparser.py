"""Base option parser setup"""

import sys
import optparse
import os
import textwrap
from distutils.util import strtobool

from pip.backwardcompat import ConfigParser, string_types
from pip.locations import default_config_file
from pip.util import get_terminal_size, get_prog
from pip._vendor import pkg_resources


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
    """Custom option parser which updates its defaults by checking the
    configuration files and environmental variables"""

    def __init__(self, *args, **kwargs):
        self.config = ConfigParser.RawConfigParser()
        self.name = kwargs.pop('name')
        self.files = self.get_config_files()
        if self.files:
            self.config.read(self.files)
        assert self.name
        optparse.OptionParser.__init__(self, *args, **kwargs)

    def get_config_files(self):
        config_file = os.environ.get('PIP_CONFIG_FILE', False)
        if config_file == os.devnull:
            return []
        if config_file and os.path.exists(config_file):
            return [config_file]
        return [default_config_file]

    def check_default(self, option, key, val):
        try:
            return option.check_value(key, val)
        except optparse.OptionValueError:
            e = sys.exc_info()[1]
            print("An error occurred during configuration: %s" % e)
            sys.exit(3)

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
                if option.action in ('store_true', 'store_false', 'count'):
                    val = strtobool(val)
                if option.action == 'append':
                    val = val.split()
                    val = [self.check_default(option, key, v) for v in val]
                else:
                    val = self.check_default(option, key, val)

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
