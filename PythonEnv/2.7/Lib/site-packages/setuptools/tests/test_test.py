# -*- coding: UTF-8 -*-

"""develop tests
"""
import sys
import os, shutil, tempfile, unittest
import tempfile
import site

from distutils.errors import DistutilsError
from setuptools.compat import StringIO
from setuptools.command.test import test
from setuptools.command import easy_install as easy_install_pkg
from setuptools.dist import Distribution

SETUP_PY = """\
from setuptools import setup

setup(name='foo',
    packages=['name', 'name.space', 'name.space.tests'],
    namespace_packages=['name'],
    test_suite='name.space.tests.test_suite',
)
"""

NS_INIT = """# -*- coding: Latin-1 -*-
# Söme Arbiträry Ünicode to test Issüé 310
try:
    __import__('pkg_resources').declare_namespace(__name__)
except ImportError:
    from pkgutil import extend_path
    __path__ = extend_path(__path__, __name__)
"""
# Make sure this is Latin-1 binary, before writing:
if sys.version_info < (3,):
    NS_INIT = NS_INIT.decode('UTF-8')
NS_INIT = NS_INIT.encode('Latin-1')

TEST_PY = """import unittest

class TestTest(unittest.TestCase):
    def test_test(self):
        print "Foo" # Should fail under Python 3 unless 2to3 is used

test_suite = unittest.makeSuite(TestTest)
"""

class TestTestTest(unittest.TestCase):

    def setUp(self):
        if sys.version < "2.6" or hasattr(sys, 'real_prefix'):
            return

        # Directory structure
        self.dir = tempfile.mkdtemp()
        os.mkdir(os.path.join(self.dir, 'name'))
        os.mkdir(os.path.join(self.dir, 'name', 'space'))
        os.mkdir(os.path.join(self.dir, 'name', 'space', 'tests'))
        # setup.py
        setup = os.path.join(self.dir, 'setup.py')
        f = open(setup, 'wt')
        f.write(SETUP_PY)
        f.close()
        self.old_cwd = os.getcwd()
        # name/__init__.py
        init = os.path.join(self.dir, 'name', '__init__.py')
        f = open(init, 'wb')
        f.write(NS_INIT)
        f.close()
        # name/space/__init__.py
        init = os.path.join(self.dir, 'name', 'space', '__init__.py')
        f = open(init, 'wt')
        f.write('#empty\n')
        f.close()
        # name/space/tests/__init__.py
        init = os.path.join(self.dir, 'name', 'space', 'tests', '__init__.py')
        f = open(init, 'wt')
        f.write(TEST_PY)
        f.close()

        os.chdir(self.dir)
        self.old_base = site.USER_BASE
        site.USER_BASE = tempfile.mkdtemp()
        self.old_site = site.USER_SITE
        site.USER_SITE = tempfile.mkdtemp()

    def tearDown(self):
        if sys.version < "2.6" or hasattr(sys, 'real_prefix'):
            return

        os.chdir(self.old_cwd)
        shutil.rmtree(self.dir)
        shutil.rmtree(site.USER_BASE)
        shutil.rmtree(site.USER_SITE)
        site.USER_BASE = self.old_base
        site.USER_SITE = self.old_site

    def test_test(self):
        if sys.version < "2.6" or hasattr(sys, 'real_prefix'):
            return

        dist = Distribution(dict(
            name='foo',
            packages=['name', 'name.space', 'name.space.tests'],
            namespace_packages=['name'],
            test_suite='name.space.tests.test_suite',
            use_2to3=True,
            ))
        dist.script_name = 'setup.py'
        cmd = test(dist)
        cmd.user = 1
        cmd.ensure_finalized()
        cmd.install_dir = site.USER_SITE
        cmd.user = 1
        old_stdout = sys.stdout
        sys.stdout = StringIO()
        try:
            try: # try/except/finally doesn't work in Python 2.4, so we need nested try-statements.
                cmd.run()
            except SystemExit: # The test runner calls sys.exit, stop that making an error.
                pass
        finally:
            sys.stdout = old_stdout

