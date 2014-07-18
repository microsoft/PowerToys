import os

from pip.basecommand import Command
from pip.log import logger
from pip._vendor import pkg_resources


class ShowCommand(Command):
    """Show information about one or more installed packages."""
    name = 'show'
    usage = """
      %prog [options] <package> ..."""
    summary = 'Show information about installed packages.'

    def __init__(self, *args, **kw):
        super(ShowCommand, self).__init__(*args, **kw)
        self.cmd_opts.add_option(
            '-f', '--files',
            dest='files',
            action='store_true',
            default=False,
            help='Show the full list of installed files for each package.')

        self.parser.insert_option_group(0, self.cmd_opts)

    def run(self, options, args):
        if not args:
            logger.warn('ERROR: Please provide a package name or names.')
            return
        query = args

        results = search_packages_info(query)
        print_results(results, options.files)


def search_packages_info(query):
    """
    Gather details from installed distributions. Print distribution name,
    version, location, and installed files. Installed files requires a
    pip generated 'installed-files.txt' in the distributions '.egg-info'
    directory.
    """
    installed_packages = dict(
        [(p.project_name.lower(), p) for p in pkg_resources.working_set])
    for name in query:
        normalized_name = name.lower()
        if normalized_name in installed_packages:
            dist = installed_packages[normalized_name]
            package = {
                'name': dist.project_name,
                'version': dist.version,
                'location': dist.location,
                'requires': [dep.project_name for dep in dist.requires()],
            }
            filelist = os.path.join(
                       dist.location,
                       dist.egg_name() + '.egg-info',
                       'installed-files.txt')
            if os.path.isfile(filelist):
                package['files'] = filelist
            yield package


def print_results(distributions, list_all_files):
    """
    Print the informations from installed distributions found.
    """
    for dist in distributions:
        logger.notify("---")
        logger.notify("Name: %s" % dist['name'])
        logger.notify("Version: %s" % dist['version'])
        logger.notify("Location: %s" % dist['location'])
        logger.notify("Requires: %s" % ', '.join(dist['requires']))
        if list_all_files:
            logger.notify("Files:")
            if 'files' in dist:
                for line in open(dist['files']):
                    logger.notify("  %s" % line.strip())
            else:
                logger.notify("Cannot locate installed-files.txt")
