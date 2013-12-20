import re
import sys
import pkg_resources
import pip
from pip.req import InstallRequirement
from pip.log import logger
from pip.basecommand import Command
from pip.util import get_installed_distributions


class FreezeCommand(Command):
    """Output installed packages in requirements format."""
    name = 'freeze'
    usage = """
      %prog [options]"""
    summary = 'Output installed packages in requirements format.'

    def __init__(self, *args, **kw):
        super(FreezeCommand, self).__init__(*args, **kw)

        self.cmd_opts.add_option(
            '-r', '--requirement',
            dest='requirement',
            action='store',
            default=None,
            metavar='file',
            help="Use the order in the given requirements file and it's comments when generating output.")
        self.cmd_opts.add_option(
            '-f', '--find-links',
            dest='find_links',
            action='append',
            default=[],
            metavar='URL',
            help='URL for finding packages, which will be added to the output.')
        self.cmd_opts.add_option(
            '-l', '--local',
            dest='local',
            action='store_true',
            default=False,
            help='If in a virtualenv that has global access, do not output globally-installed packages.')

        self.parser.insert_option_group(0, self.cmd_opts)

    def setup_logging(self):
        logger.move_stdout_to_stderr()

    def run(self, options, args):
        requirement = options.requirement
        find_links = options.find_links or []
        local_only = options.local
        ## FIXME: Obviously this should be settable:
        find_tags = False
        skip_match = None

        skip_regex = options.skip_requirements_regex
        if skip_regex:
            skip_match = re.compile(skip_regex)

        dependency_links = []

        f = sys.stdout

        for dist in pkg_resources.working_set:
            if dist.has_metadata('dependency_links.txt'):
                dependency_links.extend(dist.get_metadata_lines('dependency_links.txt'))
        for link in find_links:
            if '#egg=' in link:
                dependency_links.append(link)
        for link in find_links:
            f.write('-f %s\n' % link)
        installations = {}
        for dist in get_installed_distributions(local_only=local_only):
            req = pip.FrozenRequirement.from_dist(dist, dependency_links, find_tags=find_tags)
            installations[req.name] = req
        if requirement:
            req_f = open(requirement)
            for line in req_f:
                if not line.strip() or line.strip().startswith('#'):
                    f.write(line)
                    continue
                if skip_match and skip_match.search(line):
                    f.write(line)
                    continue
                elif line.startswith('-e') or line.startswith('--editable'):
                    if line.startswith('-e'):
                        line = line[2:].strip()
                    else:
                        line = line[len('--editable'):].strip().lstrip('=')
                    line_req = InstallRequirement.from_editable(line, default_vcs=options.default_vcs)
                elif (line.startswith('-r') or line.startswith('--requirement')
                      or line.startswith('-Z') or line.startswith('--always-unzip')
                      or line.startswith('-f') or line.startswith('-i')
                      or line.startswith('--extra-index-url')
                      or line.startswith('--find-links')
                      or line.startswith('--index-url')):
                    f.write(line)
                    continue
                else:
                    line_req = InstallRequirement.from_line(line)
                if not line_req.name:
                    logger.notify("Skipping line because it's not clear what it would install: %s"
                                  % line.strip())
                    logger.notify("  (add #egg=PackageName to the URL to avoid this warning)")
                    continue
                if line_req.name not in installations:
                    logger.warn("Requirement file contains %s, but that package is not installed"
                                % line.strip())
                    continue
                f.write(str(installations[line_req.name]))
                del installations[line_req.name]
            f.write('## The following requirements were added by pip --freeze:\n')
        for installation in sorted(installations.values(), key=lambda x: x.name):
            f.write(str(installation))
