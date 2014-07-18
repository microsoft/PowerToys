from __future__ import absolute_import, division, unicode_literals

from .py import Trie as PyTrie

Trie = PyTrie

try:
    from .datrie import Trie as DATrie
except ImportError:
    pass
else:
    Trie = DATrie
