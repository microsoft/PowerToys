# -*- coding: iso-8859-1 -*-
"""A sample implementation of SHA-1 in pure Python.

   Framework adapted from Dinu Gherman's MD5 implementation by
   J. Hallén and L. Creighton. SHA-1 implementation based directly on
   the text of the NIST standard FIPS PUB 180-1.
"""


__date__    = '2004-11-17'
__version__ = 0.91 # Modernised by J. Hallén and L. Creighton for Pypy


import struct, copy


# ======================================================================
# Bit-Manipulation helpers
#
#   _long2bytes() was contributed by Barry Warsaw
#   and is reused here with tiny modifications.
# ======================================================================

def _long2bytesBigEndian(n, blocksize=0):
    """Convert a long integer to a byte string.

    If optional blocksize is given and greater than zero, pad the front
    of the byte string with binary zeros so that the length is a multiple
    of blocksize.
    """

    # After much testing, this algorithm was deemed to be the fastest.
    s = ''
    pack = struct.pack
    while n > 0:
        s = pack('>I', n & 0xffffffff) + s
        n = n >> 32

    # Strip off leading zeros.
    for i in range(len(s)):
        if s[i] != '\000':
            break
    else:
        # Only happens when n == 0.
        s = '\000'
        i = 0

    s = s[i:]

    # Add back some pad bytes. This could be done more efficiently
    # w.r.t. the de-padding being done above, but sigh...
    if blocksize > 0 and len(s) % blocksize:
        s = (blocksize - len(s) % blocksize) * '\000' + s

    return s


def _bytelist2longBigEndian(list):
    "Transform a list of characters into a list of longs."

    imax = len(list) // 4
    hl = [0] * imax

    j = 0
    i = 0
    while i < imax:
        b0 = ord(list[j]) << 24
        b1 = ord(list[j+1]) << 16
        b2 = ord(list[j+2]) << 8
        b3 = ord(list[j+3])
        hl[i] = b0 | b1 | b2 | b3
        i = i+1
        j = j+4

    return hl


def _rotateLeft(x, n):
    "Rotate x (32 bit) left n bits circularly."

    return (x << n) | (x >> (32-n))


# ======================================================================
# The SHA transformation functions
#
# ======================================================================

def f0_19(B, C, D):
    return (B & C) | ((~ B) & D)

def f20_39(B, C, D):
    return B ^ C ^ D

def f40_59(B, C, D):
    return (B & C) | (B & D) | (C & D)

def f60_79(B, C, D):
    return B ^ C ^ D


f = [f0_19, f20_39, f40_59, f60_79]

# Constants to be used
K = [
    0x5A827999, # ( 0 <= t <= 19)
    0x6ED9EBA1, # (20 <= t <= 39)
    0x8F1BBCDC, # (40 <= t <= 59)
    0xCA62C1D6  # (60 <= t <= 79)
    ]

class sha:
    "An implementation of the MD5 hash function in pure Python."

    digest_size = digestsize = 20
    block_size = 1

    def __init__(self):
        "Initialisation."

        # Initial message length in bits(!).
        self.length = 0
        self.count = [0, 0]

        # Initial empty message as a sequence of bytes (8 bit characters).
        self.input = []

        # Call a separate init function, that can be used repeatedly
        # to start from scratch on the same object.
        self.init()


    def init(self):
        "Initialize the message-digest and set all fields to zero."

        self.length = 0
        self.input = []

        # Initial 160 bit message digest (5 times 32 bit).
        self.H0 = 0x67452301
        self.H1 = 0xEFCDAB89
        self.H2 = 0x98BADCFE
        self.H3 = 0x10325476
        self.H4 = 0xC3D2E1F0

    def _transform(self, W):

        for t in range(16, 80):
            W.append(_rotateLeft(
                W[t-3] ^ W[t-8] ^ W[t-14] ^ W[t-16], 1) & 0xffffffff)

        A = self.H0
        B = self.H1
        C = self.H2
        D = self.H3
        E = self.H4

        """
        This loop was unrolled to gain about 10% in speed
        for t in range(0, 80):
            TEMP = _rotateLeft(A, 5) + f[t/20] + E + W[t] + K[t/20]
            E = D
            D = C
            C = _rotateLeft(B, 30) & 0xffffffff
            B = A
            A = TEMP & 0xffffffff
        """

        for t in range(0, 20):
            TEMP = _rotateLeft(A, 5) + ((B & C) | ((~ B) & D)) + E + W[t] + K[0]
            E = D
            D = C
            C = _rotateLeft(B, 30) & 0xffffffff
            B = A
            A = TEMP & 0xffffffff

        for t in range(20, 40):
            TEMP = _rotateLeft(A, 5) + (B ^ C ^ D) + E + W[t] + K[1]
            E = D
            D = C
            C = _rotateLeft(B, 30) & 0xffffffff
            B = A
            A = TEMP & 0xffffffff

        for t in range(40, 60):
            TEMP = _rotateLeft(A, 5) + ((B & C) | (B & D) | (C & D)) + E + W[t] + K[2]
            E = D
            D = C
            C = _rotateLeft(B, 30) & 0xffffffff
            B = A
            A = TEMP & 0xffffffff

        for t in range(60, 80):
            TEMP = _rotateLeft(A, 5) + (B ^ C ^ D)  + E + W[t] + K[3]
            E = D
            D = C
            C = _rotateLeft(B, 30) & 0xffffffff
            B = A
            A = TEMP & 0xffffffff


        self.H0 = (self.H0 + A) & 0xffffffff
        self.H1 = (self.H1 + B) & 0xffffffff
        self.H2 = (self.H2 + C) & 0xffffffff
        self.H3 = (self.H3 + D) & 0xffffffff
        self.H4 = (self.H4 + E) & 0xffffffff


    # Down from here all methods follow the Python Standard Library
    # API of the sha module.

    def update(self, inBuf):
        """Add to the current message.

        Update the md5 object with the string arg. Repeated calls
        are equivalent to a single call with the concatenation of all
        the arguments, i.e. m.update(a); m.update(b) is equivalent
        to m.update(a+b).

        The hash is immediately calculated for all full blocks. The final
        calculation is made in digest(). It will calculate 1-2 blocks,
        depending on how much padding we have to add. This allows us to
        keep an intermediate value for the hash, so that we only need to
        make minimal recalculation if we call update() to add more data
        to the hashed string.
        """

        leninBuf = len(inBuf)

        # Compute number of bytes mod 64.
        index = (self.count[1] >> 3) & 0x3F

        # Update number of bits.
        self.count[1] = self.count[1] + (leninBuf << 3)
        if self.count[1] < (leninBuf << 3):
            self.count[0] = self.count[0] + 1
        self.count[0] = self.count[0] + (leninBuf >> 29)

        partLen = 64 - index

        if leninBuf >= partLen:
            self.input[index:] = list(inBuf[:partLen])
            self._transform(_bytelist2longBigEndian(self.input))
            i = partLen
            while i + 63 < leninBuf:
                self._transform(_bytelist2longBigEndian(list(inBuf[i:i+64])))
                i = i + 64
            else:
                self.input = list(inBuf[i:leninBuf])
        else:
            i = 0
            self.input = self.input + list(inBuf)


    def digest(self):
        """Terminate the message-digest computation and return digest.

        Return the digest of the strings passed to the update()
        method so far. This is a 16-byte string which may contain
        non-ASCII characters, including null bytes.
        """

        H0 = self.H0
        H1 = self.H1
        H2 = self.H2
        H3 = self.H3
        H4 = self.H4
        input = [] + self.input
        count = [] + self.count

        index = (self.count[1] >> 3) & 0x3f

        if index < 56:
            padLen = 56 - index
        else:
            padLen = 120 - index

        padding = ['\200'] + ['\000'] * 63
        self.update(padding[:padLen])

        # Append length (before padding).
        bits = _bytelist2longBigEndian(self.input[:56]) + count

        self._transform(bits)

        # Store state in digest.
        digest = _long2bytesBigEndian(self.H0, 4) + \
                 _long2bytesBigEndian(self.H1, 4) + \
                 _long2bytesBigEndian(self.H2, 4) + \
                 _long2bytesBigEndian(self.H3, 4) + \
                 _long2bytesBigEndian(self.H4, 4)

        self.H0 = H0
        self.H1 = H1
        self.H2 = H2
        self.H3 = H3
        self.H4 = H4
        self.input = input
        self.count = count

        return digest


    def hexdigest(self):
        """Terminate and return digest in HEX form.

        Like digest() except the digest is returned as a string of
        length 32, containing only hexadecimal digits. This may be
        used to exchange the value safely in email or other non-
        binary environments.
        """
        return ''.join(['%02x' % ord(c) for c in self.digest()])

    def copy(self):
        """Return a clone object.

        Return a copy ('clone') of the md5 object. This can be used
        to efficiently compute the digests of strings that share
        a common initial substring.
        """

        return copy.deepcopy(self)


# ======================================================================
# Mimic Python top-level functions from standard library API
# for consistency with the _sha module of the standard library.
# ======================================================================

# These are mandatory variables in the module. They have constant values
# in the SHA standard.

digest_size = 20
digestsize = 20
blocksize = 1

def new(arg=None):
    """Return a new sha crypto object.

    If arg is present, the method call update(arg) is made.
    """

    crypto = sha()
    if arg:
        crypto.update(arg)

    return crypto


if __name__ == "__main__":
    a_str = "just a test string"

    assert 'da39a3ee5e6b4b0d3255bfef95601890afd80709' == new().hexdigest()
    assert '3f0cf2e3d9e5903e839417dfc47fed6bfa6457f6' == new(a_str).hexdigest()
    assert '0852b254078fe3772568a4aba37b917f3d4066ba' == new(a_str*7).hexdigest()

    s = new(a_str)
    s.update(a_str)
    assert '8862c1b50967f39d3db6bdc2877d9ccebd3102e5' == s.hexdigest()
