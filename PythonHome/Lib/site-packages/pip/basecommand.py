"""Base Command class, and related routines"""

import os
import sys
import tempfile
import traceback
import time
import optparse

from pip import cmdoptions
from pip.locations import running_under_virtualenv
from pip.log import logger
from pip.download import PipSession
from pip.exceptions import (BadCommand, InstallationError, UninstallationError,
                            CommandError, PreviousBuildDirError)
from pip.backwardcompat import StringIO
from pip.baseparser import ConfigOptionParser, UpdatingDefaultsHelpFormatter
from pip.status_codes import (SUCCESS, ERROR, UNKNOWN_ERROR, VIRTUALENV_NOT_FOUND,
                              PREVIOUS_BUILD_DIR_ERROR)
from pip.util import get_prog


__all__ = ['Command']


class Command(object):
    name = None
    usage = None
    hidden = False

    def __init__(self):
        parser_kw = {
            'usage': self.usage,
            'prog': '%s %s' % (get_prog(), self.name),
            'formatter': UpdatingDefaultsHelpFormatter(),
            'add_help_option': False,
            'name': self.name,
            'description': self.__doc__,
        }

        self.parser = ConfigOptionParser(**parser_kw)

        # Commands should add options to this option group
        optgroup_name = '%s Options' % self.name.capitalize()
        self.cmd_opts = optparse.OptionGroup(self.parser, optgroup_name)

        # Add the general options
        gen_opts = cmdoptions.make_option_group(cmdoptions.general_group, self.parser)
        self.parser.add_option_group(gen_opts)

    def _build_session(self, options):
        session = PipSession()

        # Handle custom ca-bundles from the user
        if options.cert:
            session.verify = options.cert

        # Handle timeouts
        if options.timeout:
            session.timeout = options.timeout

        # Handle configured proxies
        if options.proxy:
            session.proxies = {
                "http": options.proxy,
                "https": options.proxy,
            }

        # Determine if we can prompt the user for authentication or not
        session.auth.prompting = not options.no_input

        return session

    def setup_logging(self):
        pass

    def parse_args(self, args):
        # factored out for testability
        return self.parser.parse_args(args)

    def main(self, args):
        options, args = self.parse_args(args)

        level = 1  # Notify
        level += options.verbose
        level -= options.quiet
        level = logger.level_for_integer(4 - level)
        complete_log = []
        logger.add_consumers(
            (level, sys.stdout),
            (logger.DEBUG, complete_log.append),
        )
        if options.log_explicit_levels:
            logger.explicit_levels = True

        self.setup_logging()

        #TODO: try to get these passing down from the command?
        #      without resorting to os.environ to hold these.

        if options.no_input:
            os.environ['PIP_NO_INPUT'] = '1'

        if options.exists_action:
            os.environ['PIP_EXISTS_ACTION'] = ' '.join(options.exists_action)

        if options.require_venv:
            # If a venv is required check if it can really be found
            if not running_under_virtualenv():
                logger.fatal('Could not find an activated virtualenv (required).')
                sys.exit(VIRTUALENV_NOT_FOUND)

        if options.log:
            log_fp = open_logfile(options.log, 'a')
            logger.add_consumers((logger.DEBUG, log_fp))
        else:
            log_fp = None

        exit = SUCCESS
        store_log = False
        try:
            status = self.run(options, args)
            # FIXME: all commands should return an exit status
            # and when it is done, isinstance is not needed anymore
            if isinstance(status, int):
                exit = status
        except PreviousBuildDirError:
            e = sys.exc_info()[1]
            logger.fatal(str(e))
            logger.info('Exception information:\n%s' % format_exc())
            store_log = True
            exit = PREVIOUS_BUILD_DIR_ERROR
        except (InstallationError, UninstallationError):
            e = sys.exc_info()[1]
            logger.fatal(str(e))
            logger.info('Exception information:\n%s' % format_exc())
            store_log = True
            exit = ERROR
        except BadCommand:
            e = sys.exc_info()[1]
            logger.fatal(str(e))
            logger.info('Exception information:\n%s' % format_exc())
            store_log = True
            exit = ERROR
        except CommandError:
            e = sys.exc_info()[1]
            logger.fatal('ERROR: %s' % e)
            logger.info('Exception information:\n%s' % format_exc())
            exit = ERROR
        except KeyboardInterrupt:
            logger.fatal('Operation cancelled by user')
            logger.info('Exception information:\n%s' % format_exc())
            store_log = True
            exit = ERROR
        except:
            logger.fatal('Exception:\n%s' % format_exc())
            store_log = True
            exit = UNKNOWN_ERROR
        if store_log:
            log_file_fn = options.log_file
            text = '\n'.join(complete_log)
            try:
                log_file_fp = open_logfile(log_file_fn, 'w')
            except IOError:
                temp = tempfile.NamedTemporaryFile(delete=False)
                log_file_fn = temp.name
                log_file_fp = open_logfile(log_file_fn, 'w')
            logger.fatal('Storing debug log for failure in %s' % log_file_fn)
            log_file_fp.write(text)
            log_file_fp.close()
        if log_fp is not None:
            log_fp.close()
        return exit


def format_exc(exc_info=None):
    if exc_info is None:
        exc_info = sys.exc_info()
    out = StringIO()
    traceback.print_exception(*exc_info, **dict(file=out))
    return out.getvalue()


def open_logfile(filename, mode='a'):
    """Open the named log file in append mode.

    If the file already exists, a separator will also be printed to
    the file to separate past activity from current activity.
    """
    filename = os.path.expanduser(filename)
    filename = os.path.abspath(filename)
    dirname = os.path.dirname(filename)
    if not os.path.exists(dirname):
        os.makedirs(dirname)
    exists = os.path.exists(filename)

    log_fp = open(filename, mode)
    if exists:
        log_fp.write('%s\n' % ('-' * 60))
        log_fp.write('%s run on %s\n' % (sys.argv[0], time.strftime('%c')))
    return log_fp
