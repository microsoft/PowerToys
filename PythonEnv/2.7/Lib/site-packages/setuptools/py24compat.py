"""
Forward-compatibility support for Python 2.4 and earlier
"""

# from jaraco.compat 1.2
try:
    from functools import wraps
except ImportError:
    def wraps(func):
        "Just return the function unwrapped"
        return lambda x: x


try:
    import hashlib
except ImportError:
    from setuptools._backport import hashlib
