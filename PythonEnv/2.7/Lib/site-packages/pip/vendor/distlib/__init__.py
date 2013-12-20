# -*- coding: utf-8 -*-
#
# Copyright (C) 2012-2013 Vinay Sajip.
# Licensed to the Python Software Foundation under a contributor agreement.
# See LICENSE.txt and CONTRIBUTORS.txt.
#
import logging

__version__ = '0.1.1'

class DistlibException(Exception):
    pass

try:
    from logging import NullHandler
except ImportError: # pragma: no cover
    class NullHandler(logging.Handler):
        def handle(self, record): pass
        def emit(self, record): pass

logger = logging.getLogger(__name__)
logger.addHandler(NullHandler())
