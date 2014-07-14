from __future__ import absolute_import, division, unicode_literals

from . import _base

try:
    from collections import OrderedDict
except ImportError:
    from ordereddict import OrderedDict


class Filter(_base.Filter):
    def __iter__(self):
        for token in _base.Filter.__iter__(self):
            if token["type"] in ("StartTag", "EmptyTag"):
                attrs = OrderedDict()
                for name, value in sorted(token["data"].items(),
                                          key=lambda x: x[0]):
                    attrs[name] = value
                token["data"] = attrs
            yield token
