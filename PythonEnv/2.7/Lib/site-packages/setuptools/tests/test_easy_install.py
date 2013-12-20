"""Easy install Tests
"""
import sys
import os
import shutil
import tempfile
import unittest
import site
from setuptools.compat import StringIO, BytesIO, next
from setuptools.compat import urlparse
import textwrap
import tarfile
import distutils.core

from setuptools.compat import StringIO, BytesIO, next, urlparse
from setuptools.sandbox import run_setup, SandboxViolation
from setuptools.command.easy_install import easy_install, fix_jython_executable, get_script_args
from setuptools.command.easy_install import  PthDistributions
from setuptools.command import easy_install as easy_install_pkg
from setuptools.dist import Distribution
from pkg_resources import Distribution as PRDistribution
import setuptools.tests.server

try:
    # import multiprocessing solely for the purpose of testing its existence
    __import__('multiprocessing')
    import logging
    _LOG = logging.getLogger('test_easy_install')
    logging.basicConfig(level=logging.INFO, stream=sys.stderr)
    _MULTIPROC = True
except ImportError:
    _MULTIPROC = False
    _LOG = None

class FakeDist(object):
    def get_entry_map(self, group):
        if group != 'console_scripts':
            return {}
        return {'name': 'ep'}

    def as_requirement(self):
        return 'spec'

WANTED = """\
#!%s
# EASY-INSTALL-ENTRY-SCRIPT: 'spec','console_scripts','name'
__requires__ = 'spec'
import sys
from pkg_resources import load_entry_point

if __name__ == '__main__':
    sys.exit(
        load_entry_point('spec', 'console_scripts', 'name')()
    )
""" % fix_jython_executable(sys.executable, "")

SETUP_PY = """\
from setuptools import setup

setup(name='foo')
"""

class TestEasyInstallTest(unittest.TestCase):

    def test_install_site_py(self):
        dist = Distribution()
        cmd = easy_install(dist)
        cmd.sitepy_installed = False
        cmd.install_dir = tempfile.mkdtemp()
        try:
            cmd.install_site_py()
            sitepy = os.path.join(cmd.install_dir, 'site.py')
            self.assertTrue(os.path.exists(sitepy))
        finally:
            shutil.rmtree(cmd.install_dir)

    def test_get_script_args(self):
        dist = FakeDist()

        old_platform = sys.platform
        try:
            name, script = [i for i in next(get_script_args(dist))][0:2]
        finally:
            sys.platform = old_platform

        self.assertEqual(script, WANTED)

    def test_no_find_links(self):
        # new option '--no-find-links', that blocks find-links added at
        # the project level
        dist = Distribution()
        cmd = easy_install(dist)
        cmd.check_pth_processing = lambda: True
        cmd.no_find_links = True
        cmd.find_links = ['link1', 'link2']
        cmd.install_dir = os.path.join(tempfile.mkdtemp(), 'ok')
        cmd.args = ['ok']
        cmd.ensure_finalized()
        self.assertEqual(cmd.package_index.scanned_urls, {})

        # let's try without it (default behavior)
        cmd = easy_install(dist)
        cmd.check_pth_processing = lambda: True
        cmd.find_links = ['link1', 'link2']
        cmd.install_dir = os.path.join(tempfile.mkdtemp(), 'ok')
        cmd.args = ['ok']
        cmd.ensure_finalized()
        keys = sorted(cmd.package_index.scanned_urls.keys())
        self.assertEqual(keys, ['link1', 'link2'])


class TestPTHFileWriter(unittest.TestCase):
    def test_add_from_cwd_site_sets_dirty(self):
        '''a pth file manager should set dirty
        if a distribution is in site but also the cwd
        '''
        pth = PthDistributions('does-not_exist', [os.getcwd()])
        self.assertTrue(not pth.dirty)
        pth.add(PRDistribution(os.getcwd()))
        self.assertTrue(pth.dirty)

    def test_add_from_site_is_ignored(self):
        if os.name != 'nt':
            location = '/test/location/does-not-have-to-exist'
        else:
            location = 'c:\\does_not_exist'
        pth = PthDistributions('does-not_exist', [location, ])
        self.assertTrue(not pth.dirty)
        pth.add(PRDistribution(location))
        self.assertTrue(not pth.dirty)


class TestUserInstallTest(unittest.TestCase):

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        setup = os.path.join(self.dir, 'setup.py')
        f = open(setup, 'w')
        f.write(SETUP_PY)
        f.close()
        self.old_cwd = os.getcwd()
        os.chdir(self.dir)
        if sys.version >= "2.6":
            self.old_has_site = easy_install_pkg.HAS_USER_SITE
            self.old_file = easy_install_pkg.__file__
            self.old_base = site.USER_BASE
            site.USER_BASE = tempfile.mkdtemp()
            self.old_site = site.USER_SITE
            site.USER_SITE = tempfile.mkdtemp()
            easy_install_pkg.__file__ = site.USER_SITE

    def tearDown(self):
        os.chdir(self.old_cwd)
        shutil.rmtree(self.dir)
        if sys.version >= "2.6":
            shutil.rmtree(site.USER_BASE)
            shutil.rmtree(site.USER_SITE)
            site.USER_BASE = self.old_base
            site.USER_SITE = self.old_site
            easy_install_pkg.HAS_USER_SITE = self.old_has_site
            easy_install_pkg.__file__ = self.old_file

    def test_user_install_implied(self):
        easy_install_pkg.HAS_USER_SITE = True # disabled sometimes
        #XXX: replace with something meaningfull
        if sys.version < "2.6":
            return #SKIP
        dist = Distribution()
        dist.script_name = 'setup.py'
        cmd = easy_install(dist)
        cmd.args = ['py']
        cmd.ensure_finalized()
        self.assertTrue(cmd.user, 'user should be implied')

    def test_multiproc_atexit(self):
        if not _MULTIPROC:
            return
        _LOG.info('this should not break')

    def test_user_install_not_implied_without_usersite_enabled(self):
        easy_install_pkg.HAS_USER_SITE = False # usually enabled
        #XXX: replace with something meaningfull
        if sys.version < "2.6":
            return #SKIP
        dist = Distribution()
        dist.script_name = 'setup.py'
        cmd = easy_install(dist)
        cmd.args = ['py']
        cmd.initialize_options()
        self.assertFalse(cmd.user, 'NOT user should be implied')

    def test_local_index(self):
        # make sure the local index is used
        # when easy_install looks for installed
        # packages
        new_location = tempfile.mkdtemp()
        target = tempfile.mkdtemp()
        egg_file = os.path.join(new_location, 'foo-1.0.egg-info')
        f = open(egg_file, 'w')
        try:
            f.write('Name: foo\n')
        finally:
            f.close()

        sys.path.append(target)
        old_ppath = os.environ.get('PYTHONPATH')
        os.environ['PYTHONPATH'] = os.path.pathsep.join(sys.path)
        try:
            dist = Distribution()
            dist.script_name = 'setup.py'
            cmd = easy_install(dist)
            cmd.install_dir = target
            cmd.args = ['foo']
            cmd.ensure_finalized()
            cmd.local_index.scan([new_location])
            res = cmd.easy_install('foo')
            self.assertEqual(os.path.realpath(res.location),
                             os.path.realpath(new_location))
        finally:
            sys.path.remove(target)
            for basedir in [new_location, target, ]:
                if not os.path.exists(basedir) or not os.path.isdir(basedir):
                    continue
                try:
                    shutil.rmtree(basedir)
                except:
                    pass
            if old_ppath is not None:
                os.environ['PYTHONPATH'] = old_ppath
            else:
                del os.environ['PYTHONPATH']

    def test_setup_requires(self):
        """Regression test for Distribute issue #318

        Ensure that a package with setup_requires can be installed when
        setuptools is installed in the user site-packages without causing a
        SandboxViolation.
        """

        test_setup_attrs = {
            'name': 'test_pkg', 'version': '0.0',
            'setup_requires': ['foobar'],
            'dependency_links': [os.path.abspath(self.dir)]
        }

        test_pkg = os.path.join(self.dir, 'test_pkg')
        test_setup_py = os.path.join(test_pkg, 'setup.py')
        test_setup_cfg = os.path.join(test_pkg, 'setup.cfg')
        os.mkdir(test_pkg)

        f = open(test_setup_py, 'w')
        f.write(textwrap.dedent("""\
            import setuptools
            setuptools.setup(**%r)
        """ % test_setup_attrs))
        f.close()

        foobar_path = os.path.join(self.dir, 'foobar-0.1.tar.gz')
        make_trivial_sdist(
            foobar_path,
            textwrap.dedent("""\
                import setuptools
                setuptools.setup(
                    name='foobar',
                    version='0.1'
                )
            """))

        old_stdout = sys.stdout
        old_stderr = sys.stderr
        sys.stdout = StringIO()
        sys.stderr = StringIO()
        try:
            try:
                reset_setup_stop_context(
                    lambda: run_setup(test_setup_py, ['install'])
                )
            except SandboxViolation:
                self.fail('Installation caused SandboxViolation')
        finally:
            sys.stdout = old_stdout
            sys.stderr = old_stderr


class TestSetupRequires(unittest.TestCase):

    def test_setup_requires_honors_fetch_params(self):
        """
        When easy_install installs a source distribution which specifies
        setup_requires, it should honor the fetch parameters (such as
        allow-hosts, index-url, and find-links).
        """
        # set up a server which will simulate an alternate package index.
        p_index = setuptools.tests.server.MockServer()
        p_index.start()
        netloc = 1
        p_index_loc = urlparse(p_index.url)[netloc]
        if p_index_loc.endswith(':0'):
            # Some platforms (Jython) don't find a port to which to bind,
            #  so skip this test for them.
            return

        # I realize this is all-but-impossible to read, because it was
        #  ported from some well-factored, safe code using 'with'. If you
        #  need to maintain this code, consider making the changes in
        #  the parent revision (of this comment) and then port the changes
        #  back for Python 2.4 (or deprecate Python 2.4).

        def install(dist_file):
            def install_at(temp_install_dir):
                def install_env():
                    ei_params = ['--index-url', p_index.url,
                        '--allow-hosts', p_index_loc,
                        '--exclude-scripts', '--install-dir', temp_install_dir,
                        dist_file]
                    def install_clean_reset():
                        def install_clean_argv():
                            # attempt to install the dist. It should fail because
                            #  it doesn't exist.
                            self.assertRaises(SystemExit,
                                easy_install_pkg.main, ei_params)
                        argv_context(install_clean_argv, ['easy_install'])
                    reset_setup_stop_context(install_clean_reset)
                environment_context(install_env, PYTHONPATH=temp_install_dir)
            tempdir_context(install_at)

        # create an sdist that has a build-time dependency.
        self.create_sdist(install)

        # there should have been two or three requests to the server
        #  (three happens on Python 3.3a)
        self.assertTrue(2 <= len(p_index.requests) <= 3)
        self.assertEqual(p_index.requests[0].path, '/does-not-exist/')

    def create_sdist(self, installer):
        """
        Create an sdist with a setup_requires dependency (of something that
        doesn't exist) and invoke installer on it.
        """
        def build_sdist(dir):
            dist_path = os.path.join(dir, 'setuptools-test-fetcher-1.0.tar.gz')
            make_trivial_sdist(
                dist_path,
                textwrap.dedent("""
                    import setuptools
                    setuptools.setup(
                        name="setuptools-test-fetcher",
                        version="1.0",
                        setup_requires = ['does-not-exist'],
                    )
                """).lstrip())
            installer(dist_path)
        tempdir_context(build_sdist)


def make_trivial_sdist(dist_path, setup_py):
    """Create a simple sdist tarball at dist_path, containing just a
    setup.py, the contents of which are provided by the setup_py string.
    """

    setup_py_file = tarfile.TarInfo(name='setup.py')
    try:
        # Python 3 (StringIO gets converted to io module)
        MemFile = BytesIO
    except AttributeError:
        MemFile = StringIO
    setup_py_bytes = MemFile(setup_py.encode('utf-8'))
    setup_py_file.size = len(setup_py_bytes.getvalue())
    dist = tarfile.open(dist_path, 'w:gz')
    try:
        dist.addfile(setup_py_file, fileobj=setup_py_bytes)
    finally:
        dist.close()


def tempdir_context(f, cd=lambda dir:None):
    """
    Invoke f in the context
    """
    temp_dir = tempfile.mkdtemp()
    orig_dir = os.getcwd()
    try:
        cd(temp_dir)
        f(temp_dir)
    finally:
        cd(orig_dir)
        shutil.rmtree(temp_dir)

def environment_context(f, **updates):
    """
    Invoke f in the context
    """
    old_env = os.environ.copy()
    os.environ.update(updates)
    try:
        f()
    finally:
        for key in updates:
            del os.environ[key]
        os.environ.update(old_env)

def argv_context(f, repl):
    """
    Invoke f in the context
    """
    old_argv = sys.argv[:]
    sys.argv[:] = repl
    try:
        f()
    finally:
        sys.argv[:] = old_argv

def reset_setup_stop_context(f):
    """
    When the setuptools tests are run using setup.py test, and then
    one wants to invoke another setup() command (such as easy_install)
    within those tests, it's necessary to reset the global variable
    in distutils.core so that the setup() command will run naturally.
    """
    setup_stop_after = distutils.core._setup_stop_after
    distutils.core._setup_stop_after = None
    try:
        f()
    finally:
        distutils.core._setup_stop_after = setup_stop_after
