#
# Module which supports allocation of memory from an mmap
#
# multiprocessing/heap.py
#
# Copyright (c) 2006-2008, R Oudkerk
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions
# are met:
#
# 1. Redistributions of source code must retain the above copyright
#    notice, this list of conditions and the following disclaimer.
# 2. Redistributions in binary form must reproduce the above copyright
#    notice, this list of conditions and the following disclaimer in the
#    documentation and/or other materials provided with the distribution.
# 3. Neither the name of author nor the names of any contributors may be
#    used to endorse or promote products derived from this software
#    without specific prior written permission.
#
# THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS "AS IS" AND
# ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
# IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
# ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
# FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
# DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
# OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
# HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
# LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
# OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
# SUCH DAMAGE.
#

import bisect
import mmap
import tempfile
import os
import sys
import threading
import itertools

import _multiprocessing
from multiprocessing.util import Finalize, info
from multiprocessing.forking import assert_spawning

__all__ = ['BufferWrapper']

#
# Inheirtable class which wraps an mmap, and from which blocks can be allocated
#

if sys.platform == 'win32':

    from _multiprocessing import win32

    class Arena(object):

        _counter = itertools.count()

        def __init__(self, size):
            self.size = size
            self.name = 'pym-%d-%d' % (os.getpid(), Arena._counter.next())
            self.buffer = mmap.mmap(-1, self.size, tagname=self.name)
            assert win32.GetLastError() == 0, 'tagname already in use'
            self._state = (self.size, self.name)

        def __getstate__(self):
            assert_spawning(self)
            return self._state

        def __setstate__(self, state):
            self.size, self.name = self._state = state
            self.buffer = mmap.mmap(-1, self.size, tagname=self.name)
            assert win32.GetLastError() == win32.ERROR_ALREADY_EXISTS

else:

    class Arena(object):

        def __init__(self, size):
            self.buffer = mmap.mmap(-1, size)
            self.size = size
            self.name = None

#
# Class allowing allocation of chunks of memory from arenas
#

class Heap(object):

    _alignment = 8

    def __init__(self, size=mmap.PAGESIZE):
        self._lastpid = os.getpid()
        self._lock = threading.Lock()
        self._size = size
        self._lengths = []
        self._len_to_seq = {}
        self._start_to_block = {}
        self._stop_to_block = {}
        self._allocated_blocks = set()
        self._arenas = []
        # list of pending blocks to free - see free() comment below
        self._pending_free_blocks = []

    @staticmethod
    def _roundup(n, alignment):
        # alignment must be a power of 2
        mask = alignment - 1
        return (n + mask) & ~mask

    def _malloc(self, size):
        # returns a large enough block -- it might be much larger
        i = bisect.bisect_left(self._lengths, size)
        if i == len(self._lengths):
            length = self._roundup(max(self._size, size), mmap.PAGESIZE)
            self._size *= 2
            info('allocating a new mmap of length %d', length)
            arena = Arena(length)
            self._arenas.append(arena)
            return (arena, 0, length)
        else:
            length = self._lengths[i]
            seq = self._len_to_seq[length]
            block = seq.pop()
            if not seq:
                del self._len_to_seq[length], self._lengths[i]

        (arena, start, stop) = block
        del self._start_to_block[(arena, start)]
        del self._stop_to_block[(arena, stop)]
        return block

    def _free(self, block):
        # free location and try to merge with neighbours
        (arena, start, stop) = block

        try:
            prev_block = self._stop_to_block[(arena, start)]
        except KeyError:
            pass
        else:
            start, _ = self._absorb(prev_block)

        try:
            next_block = self._start_to_block[(arena, stop)]
        except KeyError:
            pass
        else:
            _, stop = self._absorb(next_block)

        block = (arena, start, stop)
        length = stop - start

        try:
            self._len_to_seq[length].append(block)
        except KeyError:
            self._len_to_seq[length] = [block]
            bisect.insort(self._lengths, length)

        self._start_to_block[(arena, start)] = block
        self._stop_to_block[(arena, stop)] = block

    def _absorb(self, block):
        # deregister this block so it can be merged with a neighbour
        (arena, start, stop) = block
        del self._start_to_block[(arena, start)]
        del self._stop_to_block[(arena, stop)]

        length = stop - start
        seq = self._len_to_seq[length]
        seq.remove(block)
        if not seq:
            del self._len_to_seq[length]
            self._lengths.remove(length)

        return start, stop

    def _free_pending_blocks(self):
        # Free all the blocks in the pending list - called with the lock held.
        while True:
            try:
                block = self._pending_free_blocks.pop()
            except IndexError:
                break
            self._allocated_blocks.remove(block)
            self._free(block)

    def free(self, block):
        # free a block returned by malloc()
        # Since free() can be called asynchronously by the GC, it could happen
        # that it's called while self._lock is held: in that case,
        # self._lock.acquire() would deadlock (issue #12352). To avoid that, a
        # trylock is used instead, and if the lock can't be acquired
        # immediately, the block is added to a list of blocks to be freed
        # synchronously sometimes later from malloc() or free(), by calling
        # _free_pending_blocks() (appending and retrieving from a list is not
        # strictly thread-safe but under cPython it's atomic thanks to the GIL).
        assert os.getpid() == self._lastpid
        if not self._lock.acquire(False):
            # can't acquire the lock right now, add the block to the list of
            # pending blocks to free
            self._pending_free_blocks.append(block)
        else:
            # we hold the lock
            try:
                self._free_pending_blocks()
                self._allocated_blocks.remove(block)
                self._free(block)
            finally:
                self._lock.release()

    def malloc(self, size):
        # return a block of right size (possibly rounded up)
        assert 0 <= size < sys.maxint
        if os.getpid() != self._lastpid:
            self.__init__()                     # reinitialize after fork
        self._lock.acquire()
        self._free_pending_blocks()
        try:
            size = self._roundup(max(size,1), self._alignment)
            (arena, start, stop) = self._malloc(size)
            new_stop = start + size
            if new_stop < stop:
                self._free((arena, new_stop, stop))
            block = (arena, start, new_stop)
            self._allocated_blocks.add(block)
            return block
        finally:
            self._lock.release()

#
# Class representing a chunk of an mmap -- can be inherited
#

class BufferWrapper(object):

    _heap = Heap()

    def __init__(self, size):
        assert 0 <= size < sys.maxint
        block = BufferWrapper._heap.malloc(size)
        self._state = (block, size)
        Finalize(self, BufferWrapper._heap.free, args=(block,))

    def get_address(self):
        (arena, start, stop), size = self._state
        address, length = _multiprocessing.address_of_buffer(arena.buffer)
        assert size <= length
        return address + start

    def get_size(self):
        return self._state[1]
