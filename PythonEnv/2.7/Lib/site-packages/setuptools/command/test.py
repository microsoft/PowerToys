from setuptools import Command
from distutils.errors import DistutilsOptionError
import sys
from pkg_resources import *
from pkg_resources import _namespace_packages
from unittest import TestLoader, main

class ScanningLoader(TestLoader):

    def loadTestsFromModule(self, module):
        """Return a suite of all tests cases contained in the given module

        If the module is a package, load tests from all the modules in it.
        If the module has an ``additional_tests`` function, call it and add
        the return value to the tests.
        """
        tests = []
        if module.__name__!='setuptools.tests.doctest':  # ugh
            tests.append(TestLoader.loadTestsFromModule(self,module))

        if hasattr(module, "additional_tests"):
            tests.append(module.additional_tests())

        if hasattr(module, '__path__'):
            for file in resource_listdir(module.__name__, ''):
                if file.endswith('.py') and file!='__init__.py':
                    submodule = module.__name__+'.'+file[:-3]
                else:
                    if resource_exists(
                        module.__name__, file+'/__init__.py'
                    ):
                        submodule = module.__name__+'.'+file
                    else:
                        continue
                tests.append(self.loadTestsFromName(submodule))

        if len(tests)!=1:
            return self.suiteClass(tests)
        else:
            return tests[0] # don't create a nested suite for only one return


class test(Command):

    """Command to run unit tests after in-place build"""

    description = "run unit tests after in-place build"

    user_options = [
        ('test-module=','m', "Run 'test_suite' in specified module"),
        ('test-suite=','s',
            "Test suite to run (e.g. 'some_module.test_suite')"),
    ]

    def initialize_options(self):
        self.test_suite = None
        self.test_module = None
        self.test_loader = None


    def finalize_options(self):

        if self.test_suite is None:
            if self.test_module is None:
                self.test_suite = self.distribution.test_suite
            else:
                self.test_suite = self.test_module+".test_suite"
        elif self.test_module:
            raise DistutilsOptionError(
                "You may specify a module or a suite, but not both"
            )

        self.test_args = [self.test_suite]

        if self.verbose:
            self.test_args.insert(0,'--verbose')
        if self.test_loader is None:
            self.test_loader = getattr(self.distribution,'test_loader',None)
        if self.test_loader is None:
            self.test_loader = "setuptools.command.test:ScanningLoader"



    def with_project_on_sys_path(self, func):
        if sys.version_info >= (3,) and getattr(self.distribution, 'use_2to3', False):
            # If we run 2to3 we can not do this inplace:

            # Ensure metadata is up-to-date
            self.reinitialize_command('build_py', inplace=0)
            self.run_command('build_py')
            bpy_cmd = self.get_finalized_command("build_py")
            build_path = normalize_path(bpy_cmd.build_lib)

            # Build extensions
            self.reinitialize_command('egg_info', egg_base=build_path)
            self.run_command('egg_info')

            self.reinitialize_command('build_ext', inplace=0)
            self.run_command('build_ext')
        else:
            # Without 2to3 inplace works fine:
            self.run_command('egg_info')

            # Build extensions in-place
            self.reinitialize_command('build_ext', inplace=1)
            self.run_command('build_ext')

        ei_cmd = self.get_finalized_command("egg_info")

        old_path = sys.path[:]
        old_modules = sys.modules.copy()

        try:
            sys.path.insert(0, normalize_path(ei_cmd.egg_base))
            working_set.__init__()
            add_activation_listener(lambda dist: dist.activate())
            require('%s==%s' % (ei_cmd.egg_name, ei_cmd.egg_version))
            func()
        finally:
            sys.path[:] = old_path
            sys.modules.clear()
            sys.modules.update(old_modules)
            working_set.__init__()


    def run(self):
        if self.distribution.install_requires:
            self.distribution.fetch_build_eggs(self.distribution.install_requires)
        if self.distribution.tests_require:
            self.distribution.fetch_build_eggs(self.distribution.tests_require)

        if self.test_suite:
            cmd = ' '.join(self.test_args)
            if self.dry_run:
                self.announce('skipping "unittest %s" (dry run)' % cmd)
            else:
                self.announce('running "unittest %s"' % cmd)
                self.with_project_on_sys_path(self.run_tests)


    def run_tests(self):
        import unittest

        # Purge modules under test from sys.modules. The test loader will
        # re-import them from the build location. Required when 2to3 is used
        # with namespace packages.
        if sys.version_info >= (3,) and getattr(self.distribution, 'use_2to3', False):
            module = self.test_args[-1].split('.')[0]
            if module in _namespace_packages:
                del_modules = []
                if module in sys.modules:
                    del_modules.append(module)
                module += '.'
                for name in sys.modules:
                    if name.startswith(module):
                        del_modules.append(name)
                list(map(sys.modules.__delitem__, del_modules))

        loader_ep = EntryPoint.parse("x="+self.test_loader)
        loader_class = loader_ep.load(require=False)
        cks = loader_class()
        unittest.main(
            None, None, [unittest.__file__]+self.test_args,
            testLoader = cks
        )

































