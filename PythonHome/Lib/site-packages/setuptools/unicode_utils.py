import unicodedata
import sys
from setuptools.compat import unicode as decoded_string


# HFS Plus uses decomposed UTF-8
def decompose(path):
    if isinstance(path, decoded_string):
        return unicodedata.normalize('NFD', path)
    try:
        path = path.decode('utf-8')
        path = unicodedata.normalize('NFD', path)
        path = path.encode('utf-8')
    except UnicodeError:
        pass  # Not UTF-8
    return path


def filesys_decode(path):
    """
    Ensure that the given path is decoded,
    NONE when no expected encoding works
    """

    fs_enc = sys.getfilesystemencoding()
    if isinstance(path, decoded_string):
        return path

    for enc in (fs_enc, "utf-8"):
        try:
            return path.decode(enc)
        except UnicodeDecodeError:
            continue


def try_encode(string, enc):
    "turn unicode encoding into a functional routine"
    try:
        return string.encode(enc)
    except UnicodeEncodeError:
        return None
