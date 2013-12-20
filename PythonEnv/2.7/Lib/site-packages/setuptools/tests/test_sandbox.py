"""develop tests
"""
import sys
import os
import shutil
import unittest
import tempfile

from setuptools.sandbox import DirectorySandbox, SandboxViolation

def has_win32com():
    """
    Run this to determine if the local machine has win32com, and if it
    does, include additional tests.
    """
    if not sys.platform.startswith('win32'):
        return False
    try:
        mod = __import__('win32com')
    except ImportError:
        return False
    return True

class TestSandbox(unittest.TestCase):

    def setUp(self):
        self.dir = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.dir)

    def test_devnull(self):
        if sys.version < '2.4':
            return
        sandbox = DirectorySandbox(self.dir)
        sandbox.run(self._file_writer(os.devnull))

    def _file_writer(path):
        def do_write():
            f = open(path, 'w')
            f.write('xxx')
            f.close()
        return do_write

    _file_writer = staticmethod(_file_writer)

    if has_win32com():
        def test_win32com(self):
            """
            win32com should not be prevented from caching COM interfaces
            in gen_py.
            """
            import win32com
            gen_py = win32com.__gen_path__
            target = os.path.join(gen_py, 'test_write')
            sandbox = DirectorySandbox(self.dir)
            try:
                try:
                    sandbox.run(self._file_writer(target))
                except SandboxViolation:
                    self.fail("Could not create gen_py file due to SandboxViolation")
            finally:
                if os.path.exists(target): os.remove(target)

if __name__ == '__main__':
    unittest.main()
