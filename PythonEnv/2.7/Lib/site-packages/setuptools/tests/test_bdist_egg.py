"""develop tests
"""
import sys
import os, re, shutil, tempfile, unittest
import tempfile
import site

from distutils.errors import DistutilsError
from setuptools.compat import StringIO
from setuptools.command.bdist_egg import bdist_egg
from setuptools.command import easy_install as easy_install_pkg
from setuptools.dist import Distribution

SETUP_PY = """\
from setuptools import setup

setup(name='foo', py_modules=['hi'])
"""

class TestDevelopTest(unittest.TestCase):

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.old_cwd = os.getcwd()
        os.chdir(self.dir)
        f = open('setup.py', 'w')
        f.write(SETUP_PY)
        f.close()
        f = open('hi.py', 'w')
        f.write('1\n')
        f.close()
        if sys.version >= "2.6":
            self.old_base = site.USER_BASE
            site.USER_BASE = tempfile.mkdtemp()
            self.old_site = site.USER_SITE
            site.USER_SITE = tempfile.mkdtemp()

    def tearDown(self):
        os.chdir(self.old_cwd)
        shutil.rmtree(self.dir)
        if sys.version >= "2.6":
            shutil.rmtree(site.USER_BASE)
            shutil.rmtree(site.USER_SITE)
            site.USER_BASE = self.old_base
            site.USER_SITE = self.old_site

    def test_bdist_egg(self):
        dist = Distribution(dict(
            script_name='setup.py',
            script_args=['bdist_egg'],
            name='foo',
            py_modules=['hi']
            ))
        os.makedirs(os.path.join('build', 'src'))
        old_stdout = sys.stdout
        sys.stdout = o = StringIO()
        try:
            dist.parse_command_line()
            dist.run_commands()
        finally:
            sys.stdout = old_stdout

        # let's see if we got our egg link at the right place
        [content] = os.listdir('dist')
        self.assertTrue(re.match('foo-0.0.0-py[23].\d.egg$', content))

def test_suite():
    return unittest.makeSuite(TestDevelopTest)

