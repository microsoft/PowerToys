# urllib3/util/__init__.py
# Copyright 2008-2014 Andrey Petrov and contributors (see CONTRIBUTORS.txt)
#
# This module is part of urllib3 and is released under
# the MIT License: http://www.opensource.org/licenses/mit-license.php

from .connection import is_connection_dropped
from .request import make_headers
from .response import is_fp_closed
from .ssl_ import (
    SSLContext,
    HAS_SNI,
    assert_fingerprint,
    resolve_cert_reqs,
    resolve_ssl_version,
    ssl_wrap_socket,
)
from .timeout import (
    current_time,
    Timeout,
)
from .url import (
    get_host,
    parse_url,
    split_first,
    Url,
)
