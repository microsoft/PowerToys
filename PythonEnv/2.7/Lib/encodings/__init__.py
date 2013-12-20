""" Standard "encodings" Package

    Standard Python encoding modules are stored in this package
    directory.

    Codec modules must have names corresponding to normalized encoding
    names as defined in the normalize_encoding() function below, e.g.
    'utf-8' must be implemented by the module 'utf_8.py'.

    Each codec module must export the following interface:

    * getregentry() -> codecs.CodecInfo object
    The getregentry() API must a CodecInfo object with encoder, decoder,
    incrementalencoder, incrementaldecoder, streamwriter and streamreader
    atttributes which adhere to the Python Codec Interface Standard.

    In addition, a module may optionally also define the following
    APIs which are then used by the package's codec search function:

    * getaliases() -> sequence of encoding name strings to use as aliases

    Alias names returned by getaliases() must be normalized encoding
    names as defined by normalize_encoding().

Written by Marc-Andre Lemburg (mal@lemburg.com).

(c) Copyright CNRI, All Rights Reserved. NO WARRANTY.

"""#"

import codecs
from encodings import aliases
import __builtin__

_cache = {}
_unknown = '--unknown--'
_import_tail = ['*']
_norm_encoding_map = ('                                              . '
                      '0123456789       ABCDEFGHIJKLMNOPQRSTUVWXYZ     '
                      ' abcdefghijklmnopqrstuvwxyz                     '
                      '                                                '
                      '                                                '
                      '                ')
_aliases = aliases.aliases

class CodecRegistryError(LookupError, SystemError):
    pass

def normalize_encoding(encoding):

    """ Normalize an encoding name.

        Normalization works as follows: all non-alphanumeric
        characters except the dot used for Python package names are
        collapsed and replaced with a single underscore, e.g. '  -;#'
        becomes '_'. Leading and trailing underscores are removed.

        Note that encoding names should be ASCII only; if they do use
        non-ASCII characters, these must be Latin-1 compatible.

    """
    # Make sure we have an 8-bit string, because .translate() works
    # differently for Unicode strings.
    if hasattr(__builtin__, "unicode") and isinstance(encoding, unicode):
        # Note that .encode('latin-1') does *not* use the codec
        # registry, so this call doesn't recurse. (See unicodeobject.c
        # PyUnicode_AsEncodedString() for details)
        encoding = encoding.encode('latin-1')
    return '_'.join(encoding.translate(_norm_encoding_map).split())

def search_function(encoding):

    # Cache lookup
    entry = _cache.get(encoding, _unknown)
    if entry is not _unknown:
        return entry

    # Import the module:
    #
    # First try to find an alias for the normalized encoding
    # name and lookup the module using the aliased name, then try to
    # lookup the module using the standard import scheme, i.e. first
    # try in the encodings package, then at top-level.
    #
    norm_encoding = normalize_encoding(encoding)
    aliased_encoding = _aliases.get(norm_encoding) or \
                       _aliases.get(norm_encoding.replace('.', '_'))
    if aliased_encoding is not None:
        modnames = [aliased_encoding,
                    norm_encoding]
    else:
        modnames = [norm_encoding]
    for modname in modnames:
        if not modname or '.' in modname:
            continue
        try:
            # Import is absolute to prevent the possibly malicious import of a
            # module with side-effects that is not in the 'encodings' package.
            mod = __import__('encodings.' + modname, fromlist=_import_tail,
                             level=0)
        except ImportError:
            pass
        else:
            break
    else:
        mod = None

    try:
        getregentry = mod.getregentry
    except AttributeError:
        # Not a codec module
        mod = None

    if mod is None:
        # Cache misses
        _cache[encoding] = None
        return None

    # Now ask the module for the registry entry
    entry = getregentry()
    if not isinstance(entry, codecs.CodecInfo):
        if not 4 <= len(entry) <= 7:
            raise CodecRegistryError,\
                 'module "%s" (%s) failed to register' % \
                  (mod.__name__, mod.__file__)
        if not hasattr(entry[0], '__call__') or \
           not hasattr(entry[1], '__call__') or \
           (entry[2] is not None and not hasattr(entry[2], '__call__')) or \
           (entry[3] is not None and not hasattr(entry[3], '__call__')) or \
           (len(entry) > 4 and entry[4] is not None and not hasattr(entry[4], '__call__')) or \
           (len(entry) > 5 and entry[5] is not None and not hasattr(entry[5], '__call__')):
            raise CodecRegistryError,\
                'incompatible codecs in module "%s" (%s)' % \
                (mod.__name__, mod.__file__)
        if len(entry)<7 or entry[6] is None:
            entry += (None,)*(6-len(entry)) + (mod.__name__.split(".", 1)[1],)
        entry = codecs.CodecInfo(*entry)

    # Cache the codec registry entry
    _cache[encoding] = entry

    # Register its aliases (without overwriting previously registered
    # aliases)
    try:
        codecaliases = mod.getaliases()
    except AttributeError:
        pass
    else:
        for alias in codecaliases:
            if alias not in _aliases:
                _aliases[alias] = modname

    # Return the registry entry
    return entry

# Register the search_function in the Python codec registry
codecs.register(search_function)
