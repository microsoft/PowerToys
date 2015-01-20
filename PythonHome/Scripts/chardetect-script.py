#!d:\Personal\Github\Wox\PythonHome\python.exe
# EASY-INSTALL-ENTRY-SCRIPT: 'chardet==2.3.0','console_scripts','chardetect'
__requires__ = 'chardet==2.3.0'
import sys
from pkg_resources import load_entry_point

if __name__ == '__main__':
    sys.exit(
        load_entry_point('chardet==2.3.0', 'console_scripts', 'chardetect')()
    )
