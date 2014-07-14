# $Id$
#
#  Copyright (C) 2005   Gregory P. Smith (greg@krypto.org)
#  Licensed to PSF under a Contributor Agreement.
#

__doc__ = """hashlib module - A common interface to many hash functions.

new(name, string='') - returns a new hash object implementing the
                       given hash function; initializing the hash
                       using the given string data.

Named constructor functions are also available, these are much faster
than using new():

md5(), sha1(), sha224(), sha256(), sha384(), and sha512()

More algorithms may be available on your platform but the above are
guaranteed to exist.

NOTE: If you want the adler32 or crc32 hash functions they are available in
the zlib module.

Choose your hash function wisely.  Some have known collision weaknesses.
sha384 and sha512 will be slow on 32 bit platforms.

Hash objects have these methods:
 - update(arg): Update the hash object with the string arg. Repeated calls
                are equivalent to a single call with the concatenation of all
                the arguments.
 - digest():    Return the digest of the strings passed to the update() method
                so far. This may contain non-ASCII characters, including
                NUL bytes.
 - hexdigest(): Like digest() except the digest is returned as a string of
                double length, containing only hexadecimal digits.
 - copy():      Return a copy (clone) of the hash object. This can be used to
                efficiently compute the digests of strings that share a common
                initial substring.

For example, to obtain the digest of the string 'Nobody inspects the
spammish repetition':

    >>> import hashlib
    >>> m = hashlib.md5()
    >>> m.update("Nobody inspects")
    >>> m.update(" the spammish repetition")
    >>> m.digest()
    '\\xbbd\\x9c\\x83\\xdd\\x1e\\xa5\\xc9\\xd9\\xde\\xc9\\xa1\\x8d\\xf0\\xff\\xe9'

More condensed:

    >>> hashlib.sha224("Nobody inspects the spammish repetition").hexdigest()
    'a4337bc45a8fc544c03f52dc550cd6e1e87021bc896588bd79e901e2'

"""

# This tuple and __get_builtin_constructor() must be modified if a new
# always available algorithm is added.
__always_supported = ('md5', 'sha1', 'sha224', 'sha256', 'sha384', 'sha512')

algorithms = __always_supported

__all__ = __always_supported + ('new', 'algorithms', 'pbkdf2_hmac')


def __get_builtin_constructor(name):
    try:
        if name in ('SHA1', 'sha1'):
            import _sha
            return _sha.new
        elif name in ('MD5', 'md5'):
            import _md5
            return _md5.new
        elif name in ('SHA256', 'sha256', 'SHA224', 'sha224'):
            import _sha256
            bs = name[3:]
            if bs == '256':
                return _sha256.sha256
            elif bs == '224':
                return _sha256.sha224
        elif name in ('SHA512', 'sha512', 'SHA384', 'sha384'):
            import _sha512
            bs = name[3:]
            if bs == '512':
                return _sha512.sha512
            elif bs == '384':
                return _sha512.sha384
    except ImportError:
        pass  # no extension module, this hash is unsupported.

    raise ValueError('unsupported hash type ' + name)


def __get_openssl_constructor(name):
    try:
        f = getattr(_hashlib, 'openssl_' + name)
        # Allow the C module to raise ValueError.  The function will be
        # defined but the hash not actually available thanks to OpenSSL.
        f()
        # Use the C function directly (very fast)
        return f
    except (AttributeError, ValueError):
        return __get_builtin_constructor(name)


def __py_new(name, string=''):
    """new(name, string='') - Return a new hashing object using the named algorithm;
    optionally initialized with a string.
    """
    return __get_builtin_constructor(name)(string)


def __hash_new(name, string=''):
    """new(name, string='') - Return a new hashing object using the named algorithm;
    optionally initialized with a string.
    """
    try:
        return _hashlib.new(name, string)
    except ValueError:
        # If the _hashlib module (OpenSSL) doesn't support the named
        # hash, try using our builtin implementations.
        # This allows for SHA224/256 and SHA384/512 support even though
        # the OpenSSL library prior to 0.9.8 doesn't provide them.
        return __get_builtin_constructor(name)(string)


try:
    import _hashlib
    new = __hash_new
    __get_hash = __get_openssl_constructor
except ImportError:
    new = __py_new
    __get_hash = __get_builtin_constructor

for __func_name in __always_supported:
    # try them all, some may not work due to the OpenSSL
    # version not supporting that algorithm.
    try:
        globals()[__func_name] = __get_hash(__func_name)
    except ValueError:
        import logging
        logging.exception('code for hash %s was not found.', __func_name)


try:
    # OpenSSL's PKCS5_PBKDF2_HMAC requires OpenSSL 1.0+ with HMAC and SHA
    from _hashlib import pbkdf2_hmac
except ImportError:
    import binascii
    import struct

    _trans_5C = b"".join(chr(x ^ 0x5C) for x in range(256))
    _trans_36 = b"".join(chr(x ^ 0x36) for x in range(256))

    def pbkdf2_hmac(hash_name, password, salt, iterations, dklen=None):
        """Password based key derivation function 2 (PKCS #5 v2.0)

        This Python implementations based on the hmac module about as fast
        as OpenSSL's PKCS5_PBKDF2_HMAC for short passwords and much faster
        for long passwords.
        """
        if not isinstance(hash_name, str):
            raise TypeError(hash_name)

        if not isinstance(password, (bytes, bytearray)):
            password = bytes(buffer(password))
        if not isinstance(salt, (bytes, bytearray)):
            salt = bytes(buffer(salt))

        # Fast inline HMAC implementation
        inner = new(hash_name)
        outer = new(hash_name)
        blocksize = getattr(inner, 'block_size', 64)
        if len(password) > blocksize:
            password = new(hash_name, password).digest()
        password = password + b'\x00' * (blocksize - len(password))
        inner.update(password.translate(_trans_36))
        outer.update(password.translate(_trans_5C))

        def prf(msg, inner=inner, outer=outer):
            # PBKDF2_HMAC uses the password as key. We can re-use the same
            # digest objects and and just update copies to skip initialization.
            icpy = inner.copy()
            ocpy = outer.copy()
            icpy.update(msg)
            ocpy.update(icpy.digest())
            return ocpy.digest()

        if iterations < 1:
            raise ValueError(iterations)
        if dklen is None:
            dklen = outer.digest_size
        if dklen < 1:
            raise ValueError(dklen)

        hex_format_string = "%%0%ix" % (new(hash_name).digest_size * 2)

        dkey = b''
        loop = 1
        while len(dkey) < dklen:
            prev = prf(salt + struct.pack(b'>I', loop))
            rkey = int(binascii.hexlify(prev), 16)
            for i in xrange(iterations - 1):
                prev = prf(prev)
                rkey ^= int(binascii.hexlify(prev), 16)
            loop += 1
            dkey += binascii.unhexlify(hex_format_string % rkey)

        return dkey[:dklen]

# Cleanup locals()
del __always_supported, __func_name, __get_hash
del __py_new, __hash_new, __get_openssl_constructor
