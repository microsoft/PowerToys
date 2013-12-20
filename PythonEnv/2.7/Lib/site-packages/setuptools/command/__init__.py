__all__ = [
    'alias', 'bdist_egg', 'bdist_rpm', 'build_ext', 'build_py', 'develop',
    'easy_install', 'egg_info', 'install', 'install_lib', 'rotate', 'saveopts',
    'sdist', 'setopt', 'test', 'upload', 'install_egg_info', 'install_scripts',
    'register', 'bdist_wininst', 'upload_docs',
]

from setuptools.command import install_scripts
import sys

if sys.version>='2.5':
    # In Python 2.5 and above, distutils includes its own upload command
    __all__.remove('upload')

from distutils.command.bdist import bdist

if 'egg' not in bdist.format_commands:
    bdist.format_command['egg'] = ('bdist_egg', "Python .egg file")
    bdist.format_commands.append('egg')

del bdist, sys
