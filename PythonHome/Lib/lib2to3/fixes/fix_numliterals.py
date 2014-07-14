"""Fixer that turns 1L into 1, 0755 into 0o755.
"""
# Copyright 2007 Georg Brandl.
# Licensed to PSF under a Contributor Agreement.

# Local imports
from ..pgen2 import token
from .. import fixer_base
from ..fixer_util import Number


class FixNumliterals(fixer_base.BaseFix):
    # This is so simple that we don't need the pattern compiler.

    _accept_type = token.NUMBER

    def match(self, node):
        # Override
        return (node.value.startswith(u"0") or node.value[-1] in u"Ll")

    def transform(self, node, results):
        val = node.value
        if val[-1] in u'Ll':
            val = val[:-1]
        elif val.startswith(u'0') and val.isdigit() and len(set(val)) > 1:
            val = u"0o" + val[1:]

        return Number(val, prefix=node.prefix)
