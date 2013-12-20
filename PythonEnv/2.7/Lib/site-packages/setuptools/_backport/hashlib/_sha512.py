"""
This code was Ported from CPython's sha512module.c
"""

import struct

SHA_BLOCKSIZE = 128
SHA_DIGESTSIZE = 64


def new_shaobject():
    return {
        'digest': [0]*8,
        'count_lo': 0,
        'count_hi': 0,
        'data': [0]* SHA_BLOCKSIZE,
        'local': 0,
        'digestsize': 0
    }

ROR64 = lambda x, y: (((x & 0xffffffffffffffff) >> (y & 63)) | (x << (64 - (y & 63)))) & 0xffffffffffffffff
Ch = lambda x, y, z: (z ^ (x & (y ^ z)))
Maj = lambda x, y, z: (((x | y) & z) | (x & y))
S = lambda x, n: ROR64(x, n)
R = lambda x, n: (x & 0xffffffffffffffff) >> n
Sigma0 = lambda x: (S(x, 28) ^ S(x, 34) ^ S(x, 39))
Sigma1 = lambda x: (S(x, 14) ^ S(x, 18) ^ S(x, 41))
Gamma0 = lambda x: (S(x, 1) ^ S(x, 8) ^ R(x, 7))
Gamma1 = lambda x: (S(x, 19) ^ S(x, 61) ^ R(x, 6))

def sha_transform(sha_info):
    W = []

    d = sha_info['data']
    for i in xrange(0,16):
        W.append( (d[8*i]<<56) + (d[8*i+1]<<48) + (d[8*i+2]<<40) + (d[8*i+3]<<32) + (d[8*i+4]<<24) + (d[8*i+5]<<16) + (d[8*i+6]<<8) + d[8*i+7])

    for i in xrange(16,80):
        W.append( (Gamma1(W[i - 2]) + W[i - 7] + Gamma0(W[i - 15]) + W[i - 16]) & 0xffffffffffffffff )

    ss = sha_info['digest'][:]

    def RND(a,b,c,d,e,f,g,h,i,ki):
        t0 = (h + Sigma1(e) + Ch(e, f, g) + ki + W[i]) & 0xffffffffffffffff
        t1 = (Sigma0(a) + Maj(a, b, c)) & 0xffffffffffffffff
        d = (d + t0) & 0xffffffffffffffff
        h = (t0 + t1) & 0xffffffffffffffff
        return d & 0xffffffffffffffff, h & 0xffffffffffffffff

    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],0,0x428a2f98d728ae22)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],1,0x7137449123ef65cd)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],2,0xb5c0fbcfec4d3b2f)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],3,0xe9b5dba58189dbbc)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],4,0x3956c25bf348b538)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],5,0x59f111f1b605d019)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],6,0x923f82a4af194f9b)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],7,0xab1c5ed5da6d8118)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],8,0xd807aa98a3030242)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],9,0x12835b0145706fbe)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],10,0x243185be4ee4b28c)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],11,0x550c7dc3d5ffb4e2)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],12,0x72be5d74f27b896f)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],13,0x80deb1fe3b1696b1)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],14,0x9bdc06a725c71235)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],15,0xc19bf174cf692694)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],16,0xe49b69c19ef14ad2)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],17,0xefbe4786384f25e3)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],18,0x0fc19dc68b8cd5b5)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],19,0x240ca1cc77ac9c65)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],20,0x2de92c6f592b0275)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],21,0x4a7484aa6ea6e483)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],22,0x5cb0a9dcbd41fbd4)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],23,0x76f988da831153b5)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],24,0x983e5152ee66dfab)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],25,0xa831c66d2db43210)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],26,0xb00327c898fb213f)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],27,0xbf597fc7beef0ee4)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],28,0xc6e00bf33da88fc2)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],29,0xd5a79147930aa725)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],30,0x06ca6351e003826f)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],31,0x142929670a0e6e70)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],32,0x27b70a8546d22ffc)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],33,0x2e1b21385c26c926)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],34,0x4d2c6dfc5ac42aed)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],35,0x53380d139d95b3df)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],36,0x650a73548baf63de)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],37,0x766a0abb3c77b2a8)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],38,0x81c2c92e47edaee6)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],39,0x92722c851482353b)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],40,0xa2bfe8a14cf10364)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],41,0xa81a664bbc423001)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],42,0xc24b8b70d0f89791)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],43,0xc76c51a30654be30)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],44,0xd192e819d6ef5218)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],45,0xd69906245565a910)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],46,0xf40e35855771202a)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],47,0x106aa07032bbd1b8)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],48,0x19a4c116b8d2d0c8)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],49,0x1e376c085141ab53)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],50,0x2748774cdf8eeb99)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],51,0x34b0bcb5e19b48a8)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],52,0x391c0cb3c5c95a63)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],53,0x4ed8aa4ae3418acb)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],54,0x5b9cca4f7763e373)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],55,0x682e6ff3d6b2b8a3)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],56,0x748f82ee5defb2fc)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],57,0x78a5636f43172f60)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],58,0x84c87814a1f0ab72)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],59,0x8cc702081a6439ec)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],60,0x90befffa23631e28)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],61,0xa4506cebde82bde9)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],62,0xbef9a3f7b2c67915)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],63,0xc67178f2e372532b)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],64,0xca273eceea26619c)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],65,0xd186b8c721c0c207)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],66,0xeada7dd6cde0eb1e)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],67,0xf57d4f7fee6ed178)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],68,0x06f067aa72176fba)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],69,0x0a637dc5a2c898a6)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],70,0x113f9804bef90dae)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],71,0x1b710b35131c471b)
    ss[3], ss[7] = RND(ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],72,0x28db77f523047d84)
    ss[2], ss[6] = RND(ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],73,0x32caab7b40c72493)
    ss[1], ss[5] = RND(ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],ss[5],74,0x3c9ebe0a15c9bebc)
    ss[0], ss[4] = RND(ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],ss[4],75,0x431d67c49c100d4c)
    ss[7], ss[3] = RND(ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],ss[3],76,0x4cc5d4becb3e42b6)
    ss[6], ss[2] = RND(ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],ss[2],77,0x597f299cfc657e2a)
    ss[5], ss[1] = RND(ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],ss[1],78,0x5fcb6fab3ad6faec)
    ss[4], ss[0] = RND(ss[1],ss[2],ss[3],ss[4],ss[5],ss[6],ss[7],ss[0],79,0x6c44198c4a475817)

    dig = []
    for i, x in enumerate(sha_info['digest']):
        dig.append( (x + ss[i]) & 0xffffffffffffffff )
    sha_info['digest'] = dig

def sha_init():
    sha_info = new_shaobject()
    sha_info['digest'] = [ 0x6a09e667f3bcc908, 0xbb67ae8584caa73b, 0x3c6ef372fe94f82b, 0xa54ff53a5f1d36f1, 0x510e527fade682d1, 0x9b05688c2b3e6c1f, 0x1f83d9abfb41bd6b, 0x5be0cd19137e2179]
    sha_info['count_lo'] = 0
    sha_info['count_hi'] = 0
    sha_info['local'] = 0
    sha_info['digestsize'] = 64
    return sha_info

def sha384_init():
    sha_info = new_shaobject()
    sha_info['digest'] = [ 0xcbbb9d5dc1059ed8, 0x629a292a367cd507, 0x9159015a3070dd17, 0x152fecd8f70e5939, 0x67332667ffc00b31, 0x8eb44a8768581511, 0xdb0c2e0d64f98fa7, 0x47b5481dbefa4fa4]
    sha_info['count_lo'] = 0
    sha_info['count_hi'] = 0
    sha_info['local'] = 0
    sha_info['digestsize'] = 48
    return sha_info

def getbuf(s):
    if isinstance(s, str):
        return s
    elif isinstance(s, unicode):
        return str(s)
    else:
        return buffer(s)

def sha_update(sha_info, buffer):
    count = len(buffer)
    buffer_idx = 0
    clo = (sha_info['count_lo'] + (count << 3)) & 0xffffffff
    if clo < sha_info['count_lo']:
        sha_info['count_hi'] += 1
    sha_info['count_lo'] = clo

    sha_info['count_hi'] += (count >> 29)

    if sha_info['local']:
        i = SHA_BLOCKSIZE - sha_info['local']
        if i > count:
            i = count

        # copy buffer
        for x in enumerate(buffer[buffer_idx:buffer_idx+i]):
            sha_info['data'][sha_info['local']+x[0]] = struct.unpack('B', x[1])[0]

        count -= i
        buffer_idx += i

        sha_info['local'] += i
        if sha_info['local'] == SHA_BLOCKSIZE:
            sha_transform(sha_info)
            sha_info['local'] = 0
        else:
            return

    while count >= SHA_BLOCKSIZE:
        # copy buffer
        sha_info['data'] = [struct.unpack('B',c)[0] for c in buffer[buffer_idx:buffer_idx + SHA_BLOCKSIZE]]
        count -= SHA_BLOCKSIZE
        buffer_idx += SHA_BLOCKSIZE
        sha_transform(sha_info)

    # copy buffer
    pos = sha_info['local']
    sha_info['data'][pos:pos+count] = [struct.unpack('B',c)[0] for c in buffer[buffer_idx:buffer_idx + count]]
    sha_info['local'] = count

def sha_final(sha_info):
    lo_bit_count = sha_info['count_lo']
    hi_bit_count = sha_info['count_hi']
    count = (lo_bit_count >> 3) & 0x7f
    sha_info['data'][count] = 0x80;
    count += 1
    if count > SHA_BLOCKSIZE - 16:
        # zero the bytes in data after the count
        sha_info['data'] = sha_info['data'][:count] + ([0] * (SHA_BLOCKSIZE - count))
        sha_transform(sha_info)
        # zero bytes in data
        sha_info['data'] = [0] * SHA_BLOCKSIZE
    else:
        sha_info['data'] = sha_info['data'][:count] + ([0] * (SHA_BLOCKSIZE - count))

    sha_info['data'][112] = 0;
    sha_info['data'][113] = 0;
    sha_info['data'][114] = 0;
    sha_info['data'][115] = 0;
    sha_info['data'][116] = 0;
    sha_info['data'][117] = 0;
    sha_info['data'][118] = 0;
    sha_info['data'][119] = 0;

    sha_info['data'][120] = (hi_bit_count >> 24) & 0xff
    sha_info['data'][121] = (hi_bit_count >> 16) & 0xff
    sha_info['data'][122] = (hi_bit_count >>  8) & 0xff
    sha_info['data'][123] = (hi_bit_count >>  0) & 0xff
    sha_info['data'][124] = (lo_bit_count >> 24) & 0xff
    sha_info['data'][125] = (lo_bit_count >> 16) & 0xff
    sha_info['data'][126] = (lo_bit_count >>  8) & 0xff
    sha_info['data'][127] = (lo_bit_count >>  0) & 0xff

    sha_transform(sha_info)

    dig = []
    for i in sha_info['digest']:
        dig.extend([ ((i>>56) & 0xff), ((i>>48) & 0xff), ((i>>40) & 0xff), ((i>>32) & 0xff), ((i>>24) & 0xff), ((i>>16) & 0xff), ((i>>8) & 0xff), (i & 0xff) ])
    return ''.join([chr(i) for i in dig])

class sha512(object):
    digest_size = digestsize = SHA_DIGESTSIZE
    block_size = SHA_BLOCKSIZE

    def __init__(self, s=None):
        self._sha = sha_init()
        if s:
            sha_update(self._sha, getbuf(s))

    def update(self, s):
        sha_update(self._sha, getbuf(s))

    def digest(self):
        return sha_final(self._sha.copy())[:self._sha['digestsize']]

    def hexdigest(self):
        return ''.join(['%.2x' % ord(i) for i in self.digest()])

    def copy(self):
        new = sha512.__new__(sha512)
        new._sha = self._sha.copy()
        return new

class sha384(sha512):
    digest_size = digestsize = 48

    def __init__(self, s=None):
        self._sha = sha384_init()
        if s:
            sha_update(self._sha, getbuf(s))

    def copy(self):
        new = sha384.__new__(sha384)
        new._sha = self._sha.copy()
        return new

if __name__ == "__main__":
    a_str = "just a test string"

    assert sha512().hexdigest() == "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e"
    assert sha512(a_str).hexdigest() == "68be4c6664af867dd1d01c8d77e963d87d77b702400c8fabae355a41b8927a5a5533a7f1c28509bbd65c5f3ac716f33be271fbda0ca018b71a84708c9fae8a53"
    assert sha512(a_str*7).hexdigest() == "3233acdbfcfff9bff9fc72401d31dbffa62bd24e9ec846f0578d647da73258d9f0879f7fde01fe2cc6516af3f343807fdef79e23d696c923d79931db46bf1819"

    s = sha512(a_str)
    s.update(a_str)
    assert s.hexdigest() == "341aeb668730bbb48127d5531115f3c39d12cb9586a6ca770898398aff2411087cfe0b570689adf328cddeb1f00803acce6737a19f310b53bbdb0320828f75bb"
