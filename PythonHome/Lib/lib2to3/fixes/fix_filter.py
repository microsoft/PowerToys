# Copyright 2007 Google, Inc. All Rights Reserved.
# Licensed to PSF under a Contributor Agreement.

"""Fixer that changes filter(F, X) into list(filter(F, X)).

We avoid the transformation if the filter() call is directly contained
in iter(<>), list(<>), tuple(<>), sorted(<>), ...join(<>), or
for V in <>:.

NOTE: This is still not correct if the original code was depending on
filter(F, X) to return a string if X is a string and a tuple if X is a
tuple.  That would require type inference, which we don't do.  Let
Python 2.6 figure it out.
"""

# Local imports
from ..pgen2 import token
from .. import fixer_base
from ..fixer_util import Name, Call, ListComp, in_special_context

class FixFilter(fixer_base.ConditionalFix):
    BM_compatible = True

    PATTERN = """
    filter_lambda=power<
        'filter'
        trailer<
            '('
            arglist<
                lambdef< 'lambda'
                         (fp=NAME | vfpdef< '(' fp=NAME ')'> ) ':' xp=any
                >
                ','
                it=any
            >
            ')'
        >
    >
    |
    power<
        'filter'
        trailer< '(' arglist< none='None' ',' seq=any > ')' >
    >
    |
    power<
        'filter'
        args=trailer< '(' [any] ')' >
    >
    """

    skip_on = "future_builtins.filter"

    def transform(self, node, results):
        if self.should_skip(node):
            return

        if "filter_lambda" in results:
            new = ListComp(results.get("fp").clone(),
                           results.get("fp").clone(),
                           results.get("it").clone(),
                           results.get("xp").clone())

        elif "none" in results:
            new = ListComp(Name(u"_f"),
                           Name(u"_f"),
                           results["seq"].clone(),
                           Name(u"_f"))

        else:
            if in_special_context(node):
                return None
            new = node.clone()
            new.prefix = u""
            new = Call(Name(u"list"), [new])
        new.prefix = node.prefix
        return new
