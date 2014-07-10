r"""Fixer for unicode.

* Changes unicode to str and unichr to chr.

* If "...\u..." is not unicode literal change it into "...\\u...".

* Change u"..." into "...".

"""

from ..pgen2 import token
from .. import fixer_base

_mapping = {u"unichr" : u"chr", u"unicode" : u"str"}

class FixUnicode(fixer_base.BaseFix):
    BM_compatible = True
    PATTERN = "STRING | 'unicode' | 'unichr'"

    def start_tree(self, tree, filename):
        super(FixUnicode, self).start_tree(tree, filename)
        self.unicode_literals = 'unicode_literals' in tree.future_features

    def transform(self, node, results):
        if node.type == token.NAME:
            new = node.clone()
            new.value = _mapping[node.value]
            return new
        elif node.type == token.STRING:
            val = node.value
            if not self.unicode_literals and val[0] in u'\'"' and u'\\' in val:
                val = ur'\\'.join([
                    v.replace(u'\\u', ur'\\u').replace(u'\\U', ur'\\U')
                    for v in val.split(ur'\\')
                ])
            if val[0] in u'uU':
                val = val[1:]
            if val == node.value:
                return node
            new = node.clone()
            new.value = val
            return new
