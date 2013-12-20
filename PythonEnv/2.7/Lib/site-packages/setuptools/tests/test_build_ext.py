"""build_ext tests
"""
import os, shutil, tempfile, unittest
from distutils.command.build_ext import build_ext as distutils_build_ext
from setuptools.command.build_ext import build_ext
from setuptools.dist import Distribution

class TestBuildExtTest(unittest.TestCase):

    def test_get_ext_filename(self):
        # setuptools needs to give back the same
        # result than distutils, even if the fullname
        # is not in ext_map
        dist = Distribution()
        cmd = build_ext(dist)
        cmd.ext_map['foo/bar'] = ''
        res = cmd.get_ext_filename('foo')
        wanted = distutils_build_ext.get_ext_filename(cmd, 'foo')
        assert res == wanted

