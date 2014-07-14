from __future__ import absolute_import, division, unicode_literals

from .. import treewalkers

from .htmlserializer import HTMLSerializer


def serialize(input, tree="etree", format="html", encoding=None,
              **serializer_opts):
    # XXX: Should we cache this?
    walker = treewalkers.getTreeWalker(tree)
    if format == "html":
        s = HTMLSerializer(**serializer_opts)
    else:
        raise ValueError("type must be html")
    return s.render(walker(input), encoding)
