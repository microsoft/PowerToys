import sys
from .runner import run

if __name__ == '__main__':
    exit = run()
    if exit:
        sys.exit(exit)
