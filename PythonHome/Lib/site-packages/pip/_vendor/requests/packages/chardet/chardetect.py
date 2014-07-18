#!/usr/bin/env python
"""
Script which takes one or more file paths and reports on their detected
encodings

Example::

    % chardetect somefile someotherfile
    somefile: windows-1252 with confidence 0.5
    someotherfile: ascii with confidence 1.0

If no paths are provided, it takes its input from stdin.

"""
from io import open
from sys import argv, stdin

from chardet.universaldetector import UniversalDetector


def description_of(file, name='stdin'):
    """Return a string describing the probable encoding of a file."""
    u = UniversalDetector()
    for line in file:
        u.feed(line)
    u.close()
    result = u.result
    if result['encoding']:
        return '%s: %s with confidence %s' % (name,
                                              result['encoding'],
                                              result['confidence'])
    else:
        return '%s: no result' % name


def main():
    if len(argv) <= 1:
        print(description_of(stdin))
    else:
        for path in argv[1:]:
            with open(path, 'rb') as f:
                print(description_of(f, path))


if __name__ == '__main__':
    main()
