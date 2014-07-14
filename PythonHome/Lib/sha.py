# $Id$
#
#  Copyright (C) 2005   Gregory P. Smith (greg@krypto.org)
#  Licensed to PSF under a Contributor Agreement.

import warnings
warnings.warn("the sha module is deprecated; use the hashlib module instead",
                DeprecationWarning, 2)

from hashlib import sha1 as sha
new = sha

blocksize = 1        # legacy value (wrong in any useful sense)
digest_size = 20
digestsize = 20
