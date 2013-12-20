import textwrap
from pip.locations import build_prefix, src_prefix
from pip.util import display_path, backup_dir
from pip.log import logger
from pip.exceptions import InstallationError
from pip.commands.install import InstallCommand


class BundleCommand(InstallCommand):
    """Create pybundles (archives containing multiple packages)."""
    name = 'bundle'
    usage = """
      %prog [options] <bundle name>.pybundle <package>..."""
    summary = 'Create pybundles.'
    bundle = True

    def __init__(self, *args, **kw):
        super(BundleCommand, self).__init__(*args, **kw)
        # bundle uses different default source and build dirs
        build_opt = self.parser.get_option("--build")
        build_opt.default = backup_dir(build_prefix, '-bundle')
        src_opt = self.parser.get_option("--src")
        src_opt.default = backup_dir(src_prefix, '-bundle')
        self.parser.set_defaults(**{
                src_opt.dest: src_opt.default,
                build_opt.dest: build_opt.default,
                })

    def run(self, options, args):

        deprecation = textwrap.dedent("""

            ###############################################
            ##                                           ##
            ##  Due to lack of interest and maintenance, ##
            ##  'pip bundle' and support for installing  ##
            ##  from *.pybundle files is now deprecated, ##
            ##  and will be removed in pip v1.5.         ##
            ##                                           ##
            ###############################################

        """)
        logger.notify(deprecation)

        if not args:
            raise InstallationError('You must give a bundle filename')
        # We have to get everything when creating a bundle:
        options.ignore_installed = True
        logger.notify('Putting temporary build files in %s and source/develop files in %s'
                      % (display_path(options.build_dir), display_path(options.src_dir)))
        self.bundle_filename = args.pop(0)
        requirement_set = super(BundleCommand, self).run(options, args)
        return requirement_set
