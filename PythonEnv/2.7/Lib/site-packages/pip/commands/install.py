import os
import sys
import tempfile
import shutil
from pip.req import InstallRequirement, RequirementSet, parse_requirements
from pip.log import logger
from pip.locations import src_prefix, virtualenv_no_global, distutils_scheme
from pip.basecommand import Command
from pip.index import PackageFinder
from pip.exceptions import InstallationError, CommandError, PreviousBuildDirError
from pip import cmdoptions


class InstallCommand(Command):
    """
    Install packages from:

    - PyPI (and other indexes) using requirement specifiers.
    - VCS project urls.
    - Local project directories.
    - Local or remote source archives.

    pip also supports installing from "requirements files", which provide
    an easy way to specify a whole environment to be installed.

    See http://www.pip-installer.org for details on VCS url formats and
    requirements files.
    """
    name = 'install'

    usage = """
      %prog [options] <requirement specifier> ...
      %prog [options] -r <requirements file> ...
      %prog [options] [-e] <vcs project url> ...
      %prog [options] [-e] <local project path> ...
      %prog [options] <archive url/path> ..."""

    summary = 'Install packages.'
    bundle = False

    def __init__(self, *args, **kw):
        super(InstallCommand, self).__init__(*args, **kw)

        cmd_opts = self.cmd_opts

        cmd_opts.add_option(
            '-e', '--editable',
            dest='editables',
            action='append',
            default=[],
            metavar='path/url',
            help='Install a project in editable mode (i.e. setuptools "develop mode") from a local project path or a VCS url.')

        cmd_opts.add_option(cmdoptions.requirements)
        cmd_opts.add_option(cmdoptions.build_dir)

        cmd_opts.add_option(
            '-t', '--target',
            dest='target_dir',
            metavar='dir',
            default=None,
            help='Install packages into <dir>.')

        cmd_opts.add_option(
            '-d', '--download', '--download-dir', '--download-directory',
            dest='download_dir',
            metavar='dir',
            default=None,
            help="Download packages into <dir> instead of installing them, regardless of what's already installed.")

        cmd_opts.add_option(cmdoptions.download_cache)

        cmd_opts.add_option(
            '--src', '--source', '--source-dir', '--source-directory',
            dest='src_dir',
            metavar='dir',
            default=src_prefix,
            help='Directory to check out editable projects into. '
            'The default in a virtualenv is "<venv path>/src". '
            'The default for global installs is "<current dir>/src".')

        cmd_opts.add_option(
            '-U', '--upgrade',
            dest='upgrade',
            action='store_true',
            help='Upgrade all packages to the newest available version. '
            'This process is recursive regardless of whether a dependency is already satisfied.')

        cmd_opts.add_option(
            '--force-reinstall',
            dest='force_reinstall',
            action='store_true',
            help='When upgrading, reinstall all packages even if they are '
                 'already up-to-date.')

        cmd_opts.add_option(
            '-I', '--ignore-installed',
            dest='ignore_installed',
            action='store_true',
            help='Ignore the installed packages (reinstalling instead).')

        cmd_opts.add_option(cmdoptions.no_deps)

        cmd_opts.add_option(
            '--no-install',
            dest='no_install',
            action='store_true',
            help="Download and unpack all packages, but don't actually install them.")

        cmd_opts.add_option(
            '--no-download',
            dest='no_download',
            action="store_true",
            help="Don't download any packages, just install the ones already downloaded "
            "(completes an install run with --no-install).")

        cmd_opts.add_option(cmdoptions.install_options)
        cmd_opts.add_option(cmdoptions.global_options)

        cmd_opts.add_option(
            '--user',
            dest='use_user_site',
            action='store_true',
            help='Install using the user scheme.')

        cmd_opts.add_option(
            '--egg',
            dest='as_egg',
            action='store_true',
            help="Install as self contained egg file, like easy_install does.")

        cmd_opts.add_option(
            '--root',
            dest='root_path',
            metavar='dir',
            default=None,
            help="Install everything relative to this alternate root directory.")

        cmd_opts.add_option(cmdoptions.use_wheel)

        cmd_opts.add_option(
            '--pre',
            action='store_true',
            default=False,
            help="Include pre-release and development versions. By default, pip only finds stable versions.")

        cmd_opts.add_option(cmdoptions.no_clean)

        index_opts = cmdoptions.make_option_group(cmdoptions.index_group, self.parser)

        self.parser.insert_option_group(0, index_opts)
        self.parser.insert_option_group(0, cmd_opts)

    def _build_package_finder(self, options, index_urls):
        """
        Create a package finder appropriate to this install command.
        This method is meant to be overridden by subclasses, not
        called directly.
        """
        return PackageFinder(find_links=options.find_links,
                             index_urls=index_urls,
                             use_mirrors=options.use_mirrors,
                             mirrors=options.mirrors,
                             use_wheel=options.use_wheel,
                             allow_external=options.allow_external,
                             allow_insecure=options.allow_insecure,
                             allow_all_external=options.allow_all_external,
                             allow_all_insecure=options.allow_all_insecure,
                             allow_all_prereleases=options.pre,
                            )

    def run(self, options, args):
        if options.download_dir:
            options.no_install = True
            options.ignore_installed = True
        options.build_dir = os.path.abspath(options.build_dir)
        options.src_dir = os.path.abspath(options.src_dir)
        install_options = options.install_options or []
        if options.use_user_site:
            if virtualenv_no_global():
                raise InstallationError("Can not perform a '--user' install. User site-packages are not visible in this virtualenv.")
            install_options.append('--user')

        temp_target_dir = None
        if options.target_dir:
            options.ignore_installed = True
            temp_target_dir = tempfile.mkdtemp()
            options.target_dir = os.path.abspath(options.target_dir)
            if os.path.exists(options.target_dir) and not os.path.isdir(options.target_dir):
                raise CommandError("Target path exists but is not a directory, will not continue.")
            install_options.append('--home=' + temp_target_dir)

        global_options = options.global_options or []
        index_urls = [options.index_url] + options.extra_index_urls
        if options.no_index:
            logger.notify('Ignoring indexes: %s' % ','.join(index_urls))
            index_urls = []

        finder = self._build_package_finder(options, index_urls)

        requirement_set = RequirementSet(
            build_dir=options.build_dir,
            src_dir=options.src_dir,
            download_dir=options.download_dir,
            download_cache=options.download_cache,
            upgrade=options.upgrade,
            as_egg=options.as_egg,
            ignore_installed=options.ignore_installed,
            ignore_dependencies=options.ignore_dependencies,
            force_reinstall=options.force_reinstall,
            use_user_site=options.use_user_site,
            target_dir=temp_target_dir)
        for name in args:
            requirement_set.add_requirement(
                InstallRequirement.from_line(name, None))
        for name in options.editables:
            requirement_set.add_requirement(
                InstallRequirement.from_editable(name, default_vcs=options.default_vcs))
        for filename in options.requirements:
            for req in parse_requirements(filename, finder=finder, options=options):
                requirement_set.add_requirement(req)
        if not requirement_set.has_requirements:
            opts = {'name': self.name}
            if options.find_links:
                msg = ('You must give at least one requirement to %(name)s '
                       '(maybe you meant "pip %(name)s %(links)s"?)' %
                       dict(opts, links=' '.join(options.find_links)))
            else:
                msg = ('You must give at least one requirement '
                       'to %(name)s (see "pip help %(name)s")' % opts)
            logger.warn(msg)
            return

        try:
            if not options.no_download:
                requirement_set.prepare_files(finder, force_root_egg_info=self.bundle, bundle=self.bundle)
            else:
                requirement_set.locate_files()

            if not options.no_install and not self.bundle:
                requirement_set.install(install_options, global_options, root=options.root_path)
                installed = ' '.join([req.name for req in
                                      requirement_set.successfully_installed])
                if installed:
                    logger.notify('Successfully installed %s' % installed)
            elif not self.bundle:
                downloaded = ' '.join([req.name for req in
                                       requirement_set.successfully_downloaded])
                if downloaded:
                    logger.notify('Successfully downloaded %s' % downloaded)
            elif self.bundle:
                requirement_set.create_bundle(self.bundle_filename)
                logger.notify('Created bundle in %s' % self.bundle_filename)
        except PreviousBuildDirError:
            return
        finally:
            # Clean up
            if (not options.no_clean) and ((not options.no_install) or options.download_dir):
                requirement_set.cleanup_files(bundle=self.bundle)

        if options.target_dir:
            if not os.path.exists(options.target_dir):
                os.makedirs(options.target_dir)
            lib_dir = distutils_scheme('', home=temp_target_dir)['purelib']
            for item in os.listdir(lib_dir):
                shutil.move(
                    os.path.join(lib_dir, item),
                    os.path.join(options.target_dir, item)
                    )
            shutil.rmtree(temp_target_dir)
        return requirement_set
