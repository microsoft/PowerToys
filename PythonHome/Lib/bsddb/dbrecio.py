
"""
File-like objects that read from or write to a bsddb record.

This implements (nearly) all stdio methods.

f = DBRecIO(db, key, txn=None)
f.close()           # explicitly release resources held
flag = f.isatty()   # always false
pos = f.tell()      # get current position
f.seek(pos)         # set current position
f.seek(pos, mode)   # mode 0: absolute; 1: relative; 2: relative to EOF
buf = f.read()      # read until EOF
buf = f.read(n)     # read up to n bytes
f.truncate([size])  # truncate file at to at most size (default: current pos)
f.write(buf)        # write at current position
f.writelines(list)  # for line in list: f.write(line)

Notes:
- fileno() is left unimplemented so that code which uses it triggers
  an exception early.
- There's a simple test set (see end of this file) - not yet updated
  for DBRecIO.
- readline() is not implemented yet.


From:
    Itamar Shtull-Trauring <itamar@maxnm.com>
"""

import errno
import string

class DBRecIO:
    def __init__(self, db, key, txn=None):
        self.db = db
        self.key = key
        self.txn = txn
        self.len = None
        self.pos = 0
        self.closed = 0
        self.softspace = 0

    def close(self):
        if not self.closed:
            self.closed = 1
            del self.db, self.txn

    def isatty(self):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        return 0

    def seek(self, pos, mode = 0):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        if mode == 1:
            pos = pos + self.pos
        elif mode == 2:
            pos = pos + self.len
        self.pos = max(0, pos)

    def tell(self):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        return self.pos

    def read(self, n = -1):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        if n < 0:
            newpos = self.len
        else:
            newpos = min(self.pos+n, self.len)

        dlen = newpos - self.pos

        r = self.db.get(self.key, txn=self.txn, dlen=dlen, doff=self.pos)
        self.pos = newpos
        return r

    __fixme = """
    def readline(self, length=None):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        if self.buflist:
            self.buf = self.buf + string.joinfields(self.buflist, '')
            self.buflist = []
        i = string.find(self.buf, '\n', self.pos)
        if i < 0:
            newpos = self.len
        else:
            newpos = i+1
        if length is not None:
            if self.pos + length < newpos:
                newpos = self.pos + length
        r = self.buf[self.pos:newpos]
        self.pos = newpos
        return r

    def readlines(self, sizehint = 0):
        total = 0
        lines = []
        line = self.readline()
        while line:
            lines.append(line)
            total += len(line)
            if 0 < sizehint <= total:
                break
            line = self.readline()
        return lines
    """

    def truncate(self, size=None):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        if size is None:
            size = self.pos
        elif size < 0:
            raise IOError(errno.EINVAL,
                                      "Negative size not allowed")
        elif size < self.pos:
            self.pos = size
        self.db.put(self.key, "", txn=self.txn, dlen=self.len-size, doff=size)

    def write(self, s):
        if self.closed:
            raise ValueError, "I/O operation on closed file"
        if not s: return
        if self.pos > self.len:
            self.buflist.append('\0'*(self.pos - self.len))
            self.len = self.pos
        newpos = self.pos + len(s)
        self.db.put(self.key, s, txn=self.txn, dlen=len(s), doff=self.pos)
        self.pos = newpos

    def writelines(self, list):
        self.write(string.joinfields(list, ''))

    def flush(self):
        if self.closed:
            raise ValueError, "I/O operation on closed file"


"""
# A little test suite

def _test():
    import sys
    if sys.argv[1:]:
        file = sys.argv[1]
    else:
        file = '/etc/passwd'
    lines = open(file, 'r').readlines()
    text = open(file, 'r').read()
    f = StringIO()
    for line in lines[:-2]:
        f.write(line)
    f.writelines(lines[-2:])
    if f.getvalue() != text:
        raise RuntimeError, 'write failed'
    length = f.tell()
    print 'File length =', length
    f.seek(len(lines[0]))
    f.write(lines[1])
    f.seek(0)
    print 'First line =', repr(f.readline())
    here = f.tell()
    line = f.readline()
    print 'Second line =', repr(line)
    f.seek(-len(line), 1)
    line2 = f.read(len(line))
    if line != line2:
        raise RuntimeError, 'bad result after seek back'
    f.seek(len(line2), 1)
    list = f.readlines()
    line = list[-1]
    f.seek(f.tell() - len(line))
    line2 = f.read()
    if line != line2:
        raise RuntimeError, 'bad result after seek back from EOF'
    print 'Read', len(list), 'more lines'
    print 'File length =', f.tell()
    if f.tell() != length:
        raise RuntimeError, 'bad length'
    f.close()

if __name__ == '__main__':
    _test()
"""
