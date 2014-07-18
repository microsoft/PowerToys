# -*- coding: utf-8 -*-
from __future__ import absolute_import

import os
import sys
from pip.basecommand import Command
from pip.index import PackageFinder
from pip.log import logger
from pip.exceptions import CommandError, PreviousBuildDirError
from pip.req import InstallRequirement, RequirementSet, parse_requirements
from pip.util import normalize_path
from pip.wheel import WheelBuilder
from pip import cmdoptions

DEFAULT_WHEEL_DIR = os.path.join(normalize_path(os.curdir), 'wheelhouse')

class WheelCommand(Command):
    """
    Build Wheel archives for your requirements and dependencies.

    Wheel is a built-package format, and offers the advantage of not recompiling your software during every install.
    For more details, see the wheel docs: http://wheel.readthedocs.org/en/latest.

    Requirements: setuptools>=0.8, and wheel.

    'pip wheel' uses the bdist_wheel setuptools extension from the wheel package to build individual wheels.

    """

    name = 'wheel'
    usage = """
      %prog [options] <requirement specifier> ...
      %prog [options] -r <requirements file> ...
      %prog [options] <vcs project url> ...
      %prog [options] <local project path> ...
      %prog [options] <archive url/path> ..."""

    summary = 'Build wheels from your requirements.'

    def __init__(self, *args, **kw):
        super(WheelCommand, self).__init__(*args, **kw)

        cmd_opts = self.cmd_opts

        cmd_opts.add_option(
            '-w', '--wheel-dir',
            dest='wheel_dir',
            metavar='dir',
            default=DEFAULT_WHEEL_DIR,
            help="Build wheels into <dir>, where the default is '<cwd>/wheelhouse'.")
        cmd_opts.add_option(cmdoptions.use_wheel.make())
        cmd_opts.add_option(cmdoptions.no_use_wheel.make())
        cmd_opts.add_option(
            '--build-option',
            dest='build_options',
            metavar='options',
            action='append',
            help="Extra arguments to be supplied to 'setup.py bdist_wheel'.")
        cmd_opts.add_option(cmdoptions.requirements.make())
        cmd_opts.add_option(cmdoptions.download_cache.make())
        cmd_opts.add_option(cmdoptions.no_deps.make())
        cmd_opts.add_option(cmdoptions.build_dir.make())

        cmd_opts.add_option(
            '--global-option',
            dest='global_options',
            action='append',
            metavar='options',
            help="Extra global options to be supplied to the setup.py "
            "call before the 'bdist_wheel' command.")

        cmd_opts.add_option(
            '--pre',
            action='store_true',
            default=False,
            help="Include pre-release and development versions. By default, pip only finds stable versions.")

        cmd_opts.add_option(cmdoptions.no_clean.make())

        index_opts = cmdoptions.make_option_group(cmdoptions.index_group, self.parser)

        self.parser.insert_option_group(0, index_opts)
        self.parser.insert_option_group(0, cmd_opts)

    def run(self, options, args):

        # confirm requirements
        try:
            import wheel.bdist_wheel
        except ImportError:
            raise CommandError("'pip wheel' requires the 'wheel' package. To fix this, run:  pip install wheel")

        try:
            import pkg_resources
        except ImportError:
            raise CommandError(
                "'pip wheel' requires setuptools >= 0.8 for dist-info support."
                " To fix this, run: pip install --upgrade setuptools"
            )
        else:
            if not hasattr(pkg_resources, 'DistInfoDistribution'):
                raise CommandError(
                    "'pip wheel' requires setuptools >= 0.8 for dist-info "
                    "support. To fix this, run: pip install --upgrade "
                    "setuptools"
                )

        index_urls = [options.index_url] + options.extra_index_urls
        if options.no_index:
            logger.notify('Ignoring indexes: %s' % ','.join(index_urls))
            index_urls = []

        if options.use_mirrors:
            logger.deprecated("1.7",
                        "--use-mirrors has been deprecated and will be removed"
                        " in the future. Explicit uses of --index-url and/or "
                        "--extra-index-url is suggested.")

        if options.mirrors:
            logger.deprecated("1.7",
                        "--mirrors has been deprecated and will be removed in "
                        " the future. Explicit uses of --index-url and/or "
                        "--extra-index-url is suggested.")
            index_urls += options.mirrors

        session = self._build_session(options)

        finder = PackageFinder(find_links=options.find_links,
                               index_urls=index_urls,
                               use_wheel=options.use_wheel,
                               allow_external=options.allow_external,
                               allow_unverified=options.allow_unverified,
                               allow_all_external=options.allow_all_external,
                               allow_all_prereleases=options.pre,
                               process_dependency_links=
                                options.process_dependency_links,
                               session=session,
                            )

        options.build_dir = os.path.abspath(options.build_dir)
        requirement_set = RequirementSet(
            build_dir=options.build_dir,
            src_dir=None,
            download_dir=None,
            download_cache=options.download_cache,
            ignore_dependencies=options.ignore_dependencies,
            ignore_installed=True,
            session=session,
            wheel_download_dir=options.wheel_dir
        )

        # make the wheelhouse
        if not os.path.exists(options.wheel_dir):
            os.makedirs(options.wheel_dir)

        #parse args and/or requirements files
        for name in args:
            requirement_set.add_requirement(
                InstallRequirement.from_line(name, None))

        for filename in options.requirements:
            for req in parse_requirements(
                filename,
                finder=finder,
                options=options,
                session=session):
                if req.editable:
                    logger.notify("ignoring %s" % req.url)
                    continue
                requirement_set.add_requirement(req)

        #fail if no requirements
        if not requirement_set.has_requirements:
            opts = {'name': self.name}
            msg = ('You must give at least one requirement '
                   'to %(name)s (see "pip help %(name)s")' % opts)
            logger.error(msg)
            return

        try:
            #build wheels
            wb = WheelBuilder(
                requirement_set,
                finder,
                options.wheel_dir,
                build_options = options.build_options or [],
                global_options = options.global_options or []
                )
            wb.build()
        except PreviousBuildDirError:
            options.no_clean = True
            raise
        finally:
            if not options.no_clean:
                requirement_set.cleanup_files()
