#!/usr/bin/env python
import os
import optparse

import sys
import re

from pip.exceptions import InstallationError, CommandError, PipError
from pip.log import logger
from pip.util import get_installed_distributions, get_prog
from pip.vcs import git, mercurial, subversion, bazaar  # noqa
from pip.baseparser import create_main_parser
from pip.commands import commands, get_similar_commands, get_summaries


# The version as used in the setup.py and the docs conf.py
__version__ = "1.4.1"

def autocomplete():
    """Command and option completion for the main option parser (and options)
    and its subcommands (and options).

    Enable by sourcing one of the completion shell scripts (bash or zsh).
    """
    # Don't complete if user hasn't sourced bash_completion file.
    if 'PIP_AUTO_COMPLETE' not in os.environ:
        return
    cwords = os.environ['COMP_WORDS'].split()[1:]
    cword = int(os.environ['COMP_CWORD'])
    try:
        current = cwords[cword - 1]
    except IndexError:
        current = ''

    subcommands = [cmd for cmd, summary in get_summaries()]
    options = []
    # subcommand
    try:
        subcommand_name = [w for w in cwords if w in subcommands][0]
    except IndexError:
        subcommand_name = None

    parser = create_main_parser()
    # subcommand options
    if subcommand_name:
        # special case: 'help' subcommand has no options
        if subcommand_name == 'help':
            sys.exit(1)
        # special case: list locally installed dists for uninstall command
        if subcommand_name == 'uninstall' and not current.startswith('-'):
            installed = []
            lc = current.lower()
            for dist in get_installed_distributions(local_only=True):
                if dist.key.startswith(lc) and dist.key not in cwords[1:]:
                    installed.append(dist.key)
            # if there are no dists installed, fall back to option completion
            if installed:
                for dist in installed:
                    print(dist)
                sys.exit(1)

        subcommand = commands[subcommand_name](parser)
        options += [(opt.get_opt_string(), opt.nargs)
                    for opt in subcommand.parser.option_list_all
                    if opt.help != optparse.SUPPRESS_HELP]

        # filter out previously specified options from available options
        prev_opts = [x.split('=')[0] for x in cwords[1:cword - 1]]
        options = [(x, v) for (x, v) in options if x not in prev_opts]
        # filter options by current input
        options = [(k, v) for k, v in options if k.startswith(current)]
        for option in options:
            opt_label = option[0]
            # append '=' to options which require args
            if option[1]:
                opt_label += '='
            print(opt_label)
    else:
        # show main parser options only when necessary
        if current.startswith('-') or current.startswith('--'):
            opts = [i.option_list for i in parser.option_groups]
            opts.append(parser.option_list)
            opts = (o for it in opts for o in it)

            subcommands += [i.get_opt_string() for i in opts
                            if i.help != optparse.SUPPRESS_HELP]

        print(' '.join([x for x in subcommands if x.startswith(current)]))
    sys.exit(1)


def parseopts(args):
    parser = create_main_parser()
    parser.main = True # so the help formatter knows

    # create command listing
    command_summaries = get_summaries()

    description = [''] + ['%-27s %s' % (i, j) for i, j in command_summaries]
    parser.description = '\n'.join(description)

    options, args = parser.parse_args(args)

    if options.version:
        sys.stdout.write(parser.version)
        sys.stdout.write(os.linesep)
        sys.exit()

    # pip || pip help || pip --help -> print_help()
    if not args or (args[0] == 'help' and len(args) == 1):
        parser.print_help()
        sys.exit()

    if not args:
        msg = ('You must give a command '
               '(use "pip --help" to see a list of commands)')
        raise CommandError(msg)

    command = args[0].lower()

    if command not in commands:
        guess = get_similar_commands(command)

        msg = ['unknown command "%s"' % command]
        if guess:
            msg.append('maybe you meant "%s"' % guess)

        raise CommandError(' - '.join(msg))

    return command, options, args, parser


def main(initial_args=None):
    if initial_args is None:
        initial_args = sys.argv[1:]

    autocomplete()

    try:
        cmd_name, options, args, parser = parseopts(initial_args)
    except PipError:
        e = sys.exc_info()[1]
        sys.stderr.write("ERROR: %s" % e)
        sys.stderr.write(os.linesep)
        sys.exit(1)

    command = commands[cmd_name](parser)  # see baseparser.Command
    return command.main(args[1:], options)


def bootstrap():
    """
    Bootstrapping function to be called from install-pip.py script.
    """
    return main(['install', '--upgrade', 'pip'] + sys.argv[1:])

############################################################
## Writing freeze files


class FrozenRequirement(object):

    def __init__(self, name, req, editable, comments=()):
        self.name = name
        self.req = req
        self.editable = editable
        self.comments = comments

    _rev_re = re.compile(r'-r(\d+)$')
    _date_re = re.compile(r'-(20\d\d\d\d\d\d)$')

    @classmethod
    def from_dist(cls, dist, dependency_links, find_tags=False):
        location = os.path.normcase(os.path.abspath(dist.location))
        comments = []
        from pip.vcs import vcs, get_src_requirement
        if vcs.get_backend_name(location):
            editable = True
            try:
                req = get_src_requirement(dist, location, find_tags)
            except InstallationError:
                ex = sys.exc_info()[1]
                logger.warn("Error when trying to get requirement for VCS system %s, falling back to uneditable format" % ex)
                req = None
            if req is None:
                logger.warn('Could not determine repository location of %s' % location)
                comments.append('## !! Could not determine repository location')
                req = dist.as_requirement()
                editable = False
        else:
            editable = False
            req = dist.as_requirement()
            specs = req.specs
            assert len(specs) == 1 and specs[0][0] == '=='
            version = specs[0][1]
            ver_match = cls._rev_re.search(version)
            date_match = cls._date_re.search(version)
            if ver_match or date_match:
                svn_backend = vcs.get_backend('svn')
                if svn_backend:
                    svn_location = svn_backend(
                        ).get_location(dist, dependency_links)
                if not svn_location:
                    logger.warn(
                        'Warning: cannot find svn location for %s' % req)
                    comments.append('## FIXME: could not find svn URL in dependency_links for this package:')
                else:
                    comments.append('# Installing as editable to satisfy requirement %s:' % req)
                    if ver_match:
                        rev = ver_match.group(1)
                    else:
                        rev = '{%s}' % date_match.group(1)
                    editable = True
                    req = '%s@%s#egg=%s' % (svn_location, rev, cls.egg_name(dist))
        return cls(dist.project_name, req, editable, comments)

    @staticmethod
    def egg_name(dist):
        name = dist.egg_name()
        match = re.search(r'-py\d\.\d$', name)
        if match:
            name = name[:match.start()]
        return name

    def __str__(self):
        req = self.req
        if self.editable:
            req = '-e %s' % req
        return '\n'.join(list(self.comments) + [str(req)]) + '\n'


if __name__ == '__main__':
    exit = main()
    if exit:
        sys.exit(exit)
