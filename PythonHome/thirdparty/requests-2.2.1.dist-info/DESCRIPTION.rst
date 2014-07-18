   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

Description: Requests: HTTP for Humans
        =========================
        
        .. image:: https://badge.fury.io/py/requests.png
            :target: http://badge.fury.io/py/requests
        
        .. image:: https://pypip.in/d/requests/badge.png
                :target: https://crate.io/packages/requests/
        
        
        Requests is an Apache2 Licensed HTTP library, written in Python, for human
        beings.
        
        Most existing Python modules for sending HTTP requests are extremely
        verbose and cumbersome. Python's builtin urllib2 module provides most of
        the HTTP capabilities you should need, but the api is thoroughly broken.
        It requires an enormous amount of work (even method overrides) to
        perform the simplest of tasks.
        
        Things shouldn't be this way. Not in Python.
        
        .. code-block:: pycon
        
            >>> r = requests.get('https://api.github.com', auth=('user', 'pass'))
            >>> r.status_code
            204
            >>> r.headers['content-type']
            'application/json'
            >>> r.text
            ...
        
        See `the same code, without Requests <https://gist.github.com/973705>`_.
        
        Requests allow you to send HTTP/1.1 requests. You can add headers, form data,
        multipart files, and parameters with simple Python dictionaries, and access the
        response data in the same way. It's powered by httplib and `urllib3
        <https://github.com/shazow/urllib3>`_, but it does all the hard work and crazy
        hacks for you.
        
        
        Features
        --------
        
        - International Domains and URLs
        - Keep-Alive & Connection Pooling
        - Sessions with Cookie Persistence
        - Browser-style SSL Verification
        - Basic/Digest Authentication
        - Elegant Key/Value Cookies
        - Automatic Decompression
        - Unicode Response Bodies
        - Multipart File Uploads
        - Connection Timeouts
        - Thread-safety
        - HTTP(S) proxy support
        
        
        Installation
        ------------
        
        To install Requests, simply:
        
        .. code-block:: bash
        
            $ pip install requests
        
        Or, if you absolutely must:
        
        .. code-block:: bash
        
            $ easy_install requests
        
        But, you really shouldn't do that.
        
        
        Documentation
        -------------
        
        Documentation is available at http://docs.python-requests.org/.
        
        
        Contribute
        ----------
        
        #. Check for open issues or open a fresh issue to start a discussion around a feature idea or a bug. There is a `Contributor Friendly`_ tag for issues that should be ideal for people who are not very familiar with the codebase yet.
        #. If you feel uncomfortable or uncertain about an issue or your changes, feel free to email @sigmavirus24 and he will happily help you via email, Skype, remote pairing or whatever you are comfortable with.
        #. Fork `the repository`_ on GitHub to start making your changes to the **master** branch (or branch off of it).
        #. Write a test which shows that the bug was fixed or that the feature works as expected.
        #. Send a pull request and bug the maintainer until it gets merged and published. :) Make sure to add yourself to AUTHORS_.
        
        .. _`the repository`: http://github.com/kennethreitz/requests
        .. _AUTHORS: https://github.com/kennethreitz/requests/blob/master/AUTHORS.rst
        .. _Contributor Friendly: https://github.com/kennethreitz/requests/issues?direction=desc&labels=Contributor+Friendly&page=1&sort=updated&state=open
        
        
        .. :changelog:
        
        Release History
        ---------------
        
        2.2.1 (2014-01-23)
        ++++++++++++++++++
        
        **Bugfixes**
        
        - Fixes incorrect parsing of proxy credentials that contain a literal or encoded '#' character.
        - Assorted urllib3 fixes.
        
        2.2.0 (2014-01-09)
        ++++++++++++++++++
        
        **API Changes**
        
        - New exception: ``ContentDecodingError``. Raised instead of ``urllib3``
          ``DecodeError`` exceptions.
        
        **Bugfixes**
        
        - Avoid many many exceptions from the buggy implementation of ``proxy_bypass`` on OS X in Python 2.6.
        - Avoid crashing when attempting to get authentication credentials from ~/.netrc when running as a user without a home directory.
        - Use the correct pool size for pools of connections to proxies.
        - Fix iteration of ``CookieJar`` objects.
        - Ensure that cookies are persisted over redirect.
        - Switch back to using chardet, since it has merged with charade.
        
        2.1.0 (2013-12-05)
        ++++++++++++++++++
        
        - Updated CA Bundle, of course.
        - Cookies set on individual Requests through a ``Session`` (e.g. via ``Session.get()``) are no longer persisted to the ``Session``.
        - Clean up connections when we hit problems during chunked upload, rather than leaking them.
        - Return connections to the pool when a chunked upload is successful, rather than leaking it.
        - Match the HTTPbis recommendation for HTTP 301 redirects.
        - Prevent hanging when using streaming uploads and Digest Auth when a 401 is received.
        - Values of headers set by Requests are now always the native string type.
        - Fix previously broken SNI support.
        - Fix accessing HTTP proxies using proxy authentication.
        - Unencode HTTP Basic usernames and passwords extracted from URLs.
        - Support for IP address ranges for no_proxy environment variable
        - Parse headers correctly when users override the default ``Host:`` header.
        - Avoid munging the URL in case of case-sensitive servers.
        - Looser URL handling for non-HTTP/HTTPS urls.
        - Accept unicode methods in Python 2.6 and 2.7.
        - More resilient cookie handling.
        - Make ``Response`` objects pickleable.
        - Actually added MD5-sess to Digest Auth instead of pretending to like last time.
        - Updated internal urllib3.
        - Fixed @Lukasa's lack of taste.
        
        2.0.1 (2013-10-24)
        ++++++++++++++++++
        
        - Updated included CA Bundle with new mistrusts and automated process for the future
        - Added MD5-sess to Digest Auth
        - Accept per-file headers in multipart file POST messages.
        - Fixed: Don't send the full URL on CONNECT messages.
        - Fixed: Correctly lowercase a redirect scheme.
        - Fixed: Cookies not persisted when set via functional API.
        - Fixed: Translate urllib3 ProxyError into a requests ProxyError derived from ConnectionError.
        - Updated internal urllib3 and chardet.
        
        2.0.0 (2013-09-24)
        ++++++++++++++++++
        
        **API Changes:**
        
        - Keys in the Headers dictionary are now native strings on all Python versions,
          i.e. bytestrings on Python 2, unicode on Python 3.
        - Proxy URLs now *must* have an explicit scheme. A ``MissingSchema`` exception
          will be raised if they don't.
        - Timeouts now apply to read time if ``Stream=False``.
        - ``RequestException`` is now a subclass of ``IOError``, not ``RuntimeError``.
        - Added new method to ``PreparedRequest`` objects: ``PreparedRequest.copy()``.
        - Added new method to ``Session`` objects: ``Session.update_request()``. This
          method updates a ``Request`` object with the data (e.g. cookies) stored on
          the ``Session``.
        - Added new method to ``Session`` objects: ``Session.prepare_request()``. This
          method updates and prepares a ``Request`` object, and returns the
          corresponding ``PreparedRequest`` object.
        - Added new method to ``HTTPAdapter`` objects: ``HTTPAdapter.proxy_headers()``.
          This should not be called directly, but improves the subclass interface.
        - ``httplib.IncompleteRead`` exceptions caused by incorrect chunked encoding
          will now raise a Requests ``ChunkedEncodingError`` instead.
        - Invalid percent-escape sequences now cause a Requests ``InvalidURL``
          exception to be raised.
        - HTTP 208 no longer uses reason phrase ``"im_used"``. Correctly uses
          ``"already_reported"``.
        - HTTP 226 reason added (``"im_used"``).
        
        **Bugfixes:**
        
        - Vastly improved proxy support, including the CONNECT verb. Special thanks to
          the many contributors who worked towards this improvement.
        - Cookies are now properly managed when 401 authentication responses are
          received.
        - Chunked encoding fixes.
        - Support for mixed case schemes.
        - Better handling of streaming downloads.
        - Retrieve environment proxies from more locations.
        - Minor cookies fixes.
        - Improved redirect behaviour.
        - Improved streaming behaviour, particularly for compressed data.
        - Miscellaneous small Python 3 text encoding bugs.
        - ``.netrc`` no longer overrides explicit auth.
        - Cookies set by hooks are now correctly persisted on Sessions.
        - Fix problem with cookies that specify port numbers in their host field.
        - ``BytesIO`` can be used to perform streaming uploads.
        - More generous parsing of the ``no_proxy`` environment variable.
        - Non-string objects can be passed in data values alongside files.
        
        1.2.3 (2013-05-25)
        ++++++++++++++++++
        
        - Simple packaging fix
        
        
        1.2.2 (2013-05-23)
        ++++++++++++++++++
        
        - Simple packaging fix
        
        
        1.2.1 (2013-05-20)
        ++++++++++++++++++
        
        - Python 3.3.2 compatibility
        - Always percent-encode location headers
        - Fix connection adapter matching to be most-specific first
        - new argument to the default connection adapter for passing a block argument
        - prevent a KeyError when there's no link headers
        
        1.2.0 (2013-03-31)
        ++++++++++++++++++
        
        - Fixed cookies on sessions and on requests
        - Significantly change how hooks are dispatched - hooks now receive all the
          arguments specified by the user when making a request so hooks can make a
          secondary request with the same parameters. This is especially necessary for
          authentication handler authors
        - certifi support was removed
        - Fixed bug where using OAuth 1 with body ``signature_type`` sent no data
        - Major proxy work thanks to @Lukasa including parsing of proxy authentication
          from the proxy url
        - Fix DigestAuth handling too many 401s
        - Update vendored urllib3 to include SSL bug fixes
        - Allow keyword arguments to be passed to ``json.loads()`` via the
          ``Response.json()`` method
        - Don't send ``Content-Length`` header by default on ``GET`` or ``HEAD``
          requests
        - Add ``elapsed`` attribute to ``Response`` objects to time how long a request
          took.
        - Fix ``RequestsCookieJar``
        - Sessions and Adapters are now picklable, i.e., can be used with the
          multiprocessing library
        - Update charade to version 1.0.3
        
        The change in how hooks are dispatched will likely cause a great deal of
        issues.
        
        1.1.0 (2013-01-10)
        ++++++++++++++++++
        
        - CHUNKED REQUESTS
        - Support for iterable response bodies
        - Assume servers persist redirect params
        - Allow explicit content types to be specified for file data
        - Make merge_kwargs case-insensitive when looking up keys
        
        1.0.3 (2012-12-18)
        ++++++++++++++++++
        
        - Fix file upload encoding bug
        - Fix cookie behavior
        
        1.0.2 (2012-12-17)
        ++++++++++++++++++
        
        - Proxy fix for HTTPAdapter.
        
        1.0.1 (2012-12-17)
        ++++++++++++++++++
        
        - Cert verification exception bug.
        - Proxy fix for HTTPAdapter.
        
        1.0.0 (2012-12-17)
        ++++++++++++++++++
        
        - Massive Refactor and Simplification
        - Switch to Apache 2.0 license
        - Swappable Connection Adapters
        - Mountable Connection Adapters
        - Mutable ProcessedRequest chain
        - /s/prefetch/stream
        - Removal of all configuration
        - Standard library logging
        - Make Response.json() callable, not property.
        - Usage of new charade project, which provides python 2 and 3 simultaneous chardet.
        - Removal of all hooks except 'response'
        - Removal of all authentication helpers (OAuth, Kerberos)
        
        This is not a backwards compatible change.
        
        0.14.2 (2012-10-27)
        +++++++++++++++++++
        
        - Improved mime-compatible JSON handling
        - Proxy fixes
        - Path hack fixes
        - Case-Insensistive Content-Encoding headers
        - Support for CJK parameters in form posts
        
        
        0.14.1 (2012-10-01)
        +++++++++++++++++++
        
        - Python 3.3 Compatibility
        - Simply default accept-encoding
        - Bugfixes
        
        
        0.14.0 (2012-09-02)
        ++++++++++++++++++++
        
        - No more iter_content errors if already downloaded.
        
        0.13.9 (2012-08-25)
        +++++++++++++++++++
        
        - Fix for OAuth + POSTs
        - Remove exception eating from dispatch_hook
        - General bugfixes
        
        0.13.8 (2012-08-21)
        +++++++++++++++++++
        
        - Incredible Link header support :)
        
        0.13.7 (2012-08-19)
        +++++++++++++++++++
        
        - Support for (key, value) lists everywhere.
        - Digest Authentication improvements.
        - Ensure proxy exclusions work properly.
        - Clearer UnicodeError exceptions.
        - Automatic casting of URLs to tsrings (fURL and such)
        - Bugfixes.
        
        0.13.6 (2012-08-06)
        +++++++++++++++++++
        
        - Long awaited fix for hanging connections!
        
        0.13.5 (2012-07-27)
        +++++++++++++++++++
        
        - Packaging fix
        
        0.13.4 (2012-07-27)
        +++++++++++++++++++
        
        - GSSAPI/Kerberos authentication!
        - App Engine 2.7 Fixes!
        - Fix leaking connections (from urllib3 update)
        - OAuthlib path hack fix
        - OAuthlib URL parameters fix.
        
        0.13.3 (2012-07-12)
        +++++++++++++++++++
        
        - Use simplejson if available.
        - Do not hide SSLErrors behind Timeouts.
        - Fixed param handling with urls containing fragments.
        - Significantly improved information in User Agent.
        - client certificates are ignored when verify=False
        
        0.13.2 (2012-06-28)
        +++++++++++++++++++
        
        - Zero dependencies (once again)!
        - New: Response.reason
        - Sign querystring parameters in OAuth 1.0
        - Client certificates no longer ignored when verify=False
        - Add openSUSE certificate support
        
        0.13.1 (2012-06-07)
        +++++++++++++++++++
        
        - Allow passing a file or file-like object as data.
        - Allow hooks to return responses that indicate errors.
        - Fix Response.text and Response.json for body-less responses.
        
        0.13.0 (2012-05-29)
        +++++++++++++++++++
        
        - Removal of Requests.async in favor of `grequests <https://github.com/kennethreitz/grequests>`_
        - Allow disabling of cookie persistiance.
        - New implimentation of safe_mode
        - cookies.get now supports default argument
        - Session cookies not saved when Session.request is called with return_response=False
        - Env: no_proxy support.
        - RequestsCookieJar improvements.
        - Various bug fixes.
        
        0.12.1 (2012-05-08)
        +++++++++++++++++++
        
        - New ``Response.json`` property.
        - Ability to add string file uploads.
        - Fix out-of-range issue with iter_lines.
        - Fix iter_content default size.
        - Fix POST redirects containing files.
        
        0.12.0 (2012-05-02)
        +++++++++++++++++++
        
        - EXPERIMENTAL OAUTH SUPPORT!
        - Proper CookieJar-backed cookies interface with awesome dict-like interface.
        - Speed fix for non-iterated content chunks.
        - Move ``pre_request`` to a more usable place.
        - New ``pre_send`` hook.
        - Lazily encode data, params, files.
        - Load system Certificate Bundle if ``certify`` isn't available.
        - Cleanups, fixes.
        
        0.11.2 (2012-04-22)
        +++++++++++++++++++
        
        - Attempt to use the OS's certificate bundle if ``certifi`` isn't available.
        - Infinite digest auth redirect fix.
        - Multi-part file upload improvements.
        - Fix decoding of invalid %encodings in URLs.
        - If there is no content in a response don't throw an error the second time that content is attempted to be read.
        - Upload data on redirects.
        
        0.11.1 (2012-03-30)
        +++++++++++++++++++
        
        * POST redirects now break RFC to do what browsers do: Follow up with a GET.
        * New ``strict_mode`` configuration to disable new redirect behavior.
        
        
        0.11.0 (2012-03-14)
        +++++++++++++++++++
        
        * Private SSL Certificate support
        * Remove select.poll from Gevent monkeypatching
        * Remove redundant generator for chunked transfer encoding
        * Fix: Response.ok raises Timeout Exception in safe_mode
        
        0.10.8 (2012-03-09)
        +++++++++++++++++++
        
        * Generate chunked ValueError fix
        * Proxy configuration by environment variables
        * Simplification of iter_lines.
        * New `trust_env` configuration for disabling system/environment hints.
        * Suppress cookie errors.
        
        0.10.7 (2012-03-07)
        +++++++++++++++++++
        
        * `encode_uri` = False
        
        0.10.6 (2012-02-25)
        +++++++++++++++++++
        
        * Allow '=' in cookies.
        
        0.10.5 (2012-02-25)
        +++++++++++++++++++
        
        * Response body with 0 content-length fix.
        * New async.imap.
        * Don't fail on netrc.
        
        
        0.10.4 (2012-02-20)
        +++++++++++++++++++
        
        * Honor netrc.
        
        0.10.3 (2012-02-20)
        +++++++++++++++++++
        
        * HEAD requests don't follow redirects anymore.
        * raise_for_status() doesn't raise for 3xx anymore.
        * Make Session objects picklable.
        * ValueError for invalid schema URLs.
        
        0.10.2 (2012-01-15)
        +++++++++++++++++++
        
        * Vastly improved URL quoting.
        * Additional allowed cookie key values.
        * Attempted fix for "Too many open files" Error
        * Replace unicode errors on first pass, no need for second pass.
        * Append '/' to bare-domain urls before query insertion.
        * Exceptions now inherit from RuntimeError.
        * Binary uploads + auth fix.
        * Bugfixes.
        
        
        0.10.1 (2012-01-23)
        +++++++++++++++++++
        
        * PYTHON 3 SUPPORT!
        * Dropped 2.5 Support. (*Backwards Incompatible*)
        
        0.10.0 (2012-01-21)
        +++++++++++++++++++
        
        * ``Response.content`` is now bytes-only. (*Backwards Incompatible*)
        * New ``Response.text`` is unicode-only.
        * If no ``Response.encoding`` is specified and ``chardet`` is available, ``Respoonse.text`` will guess an encoding.
        * Default to ISO-8859-1 (Western) encoding for "text" subtypes.
        * Removal of `decode_unicode`. (*Backwards Incompatible*)
        * New multiple-hooks system.
        * New ``Response.register_hook`` for registering hooks within the pipeline.
        * ``Response.url`` is now Unicode.
        
        0.9.3 (2012-01-18)
        ++++++++++++++++++
        
        * SSL verify=False bugfix (apparent on windows machines).
        
        0.9.2 (2012-01-18)
        ++++++++++++++++++
        
        * Asynchronous async.send method.
        * Support for proper chunk streams with boundaries.
        * session argument for Session classes.
        * Print entire hook tracebacks, not just exception instance.
        * Fix response.iter_lines from pending next line.
        * Fix but in HTTP-digest auth w/ URI having query strings.
        * Fix in Event Hooks section.
        * Urllib3 update.
        
        
        0.9.1 (2012-01-06)
        ++++++++++++++++++
        
        * danger_mode for automatic Response.raise_for_status()
        * Response.iter_lines refactor
        
        0.9.0 (2011-12-28)
        ++++++++++++++++++
        
        * verify ssl is default.
        
        
        0.8.9 (2011-12-28)
        ++++++++++++++++++
        
        * Packaging fix.
        
        
        0.8.8 (2011-12-28)
        ++++++++++++++++++
        
        * SSL CERT VERIFICATION!
        * Release of Cerifi: Mozilla's cert list.
        * New 'verify' argument for SSL requests.
        * Urllib3 update.
        
        0.8.7 (2011-12-24)
        ++++++++++++++++++
        
        * iter_lines last-line truncation fix
        * Force safe_mode for async requests
        * Handle safe_mode exceptions more consistently
        * Fix iteration on null responses in safe_mode
        
        0.8.6 (2011-12-18)
        ++++++++++++++++++
        
        * Socket timeout fixes.
        * Proxy Authorization support.
        
        0.8.5 (2011-12-14)
        ++++++++++++++++++
        
        * Response.iter_lines!
        
        0.8.4 (2011-12-11)
        ++++++++++++++++++
        
        * Prefetch bugfix.
        * Added license to installed version.
        
        0.8.3 (2011-11-27)
        ++++++++++++++++++
        
        * Converted auth system to use simpler callable objects.
        * New session parameter to API methods.
        * Display full URL while logging.
        
        0.8.2 (2011-11-19)
        ++++++++++++++++++
        
        * New Unicode decoding system, based on over-ridable `Response.encoding`.
        * Proper URL slash-quote handling.
        * Cookies with ``[``, ``]``, and ``_`` allowed.
        
        0.8.1 (2011-11-15)
        ++++++++++++++++++
        
        * URL Request path fix
        * Proxy fix.
        * Timeouts fix.
        
        0.8.0 (2011-11-13)
        ++++++++++++++++++
        
        * Keep-alive support!
        * Complete removal of Urllib2
        * Complete removal of Poster
        * Complete removal of CookieJars
        * New ConnectionError raising
        * Safe_mode for error catching
        * prefetch parameter for request methods
        * OPTION method
        * Async pool size throttling
        * File uploads send real names
        * Vendored in urllib3
        
        0.7.6 (2011-11-07)
        ++++++++++++++++++
        
        * Digest authentication bugfix (attach query data to path)
        
        0.7.5 (2011-11-04)
        ++++++++++++++++++
        
        * Response.content = None if there was an invalid repsonse.
        * Redirection auth handling.
        
        0.7.4 (2011-10-26)
        ++++++++++++++++++
        
        * Session Hooks fix.
        
        0.7.3 (2011-10-23)
        ++++++++++++++++++
        
        * Digest Auth fix.
        
        
        0.7.2 (2011-10-23)
        ++++++++++++++++++
        
        * PATCH Fix.
        
        
        0.7.1 (2011-10-23)
        ++++++++++++++++++
        
        * Move away from urllib2 authentication handling.
        * Fully Remove AuthManager, AuthObject, &c.
        * New tuple-based auth system with handler callbacks.
        
        
        0.7.0 (2011-10-22)
        ++++++++++++++++++
        
        * Sessions are now the primary interface.
        * Deprecated InvalidMethodException.
        * PATCH fix.
        * New config system (no more global settings).
        
        
        0.6.6 (2011-10-19)
        ++++++++++++++++++
        
        * Session parameter bugfix (params merging).
        
        
        0.6.5 (2011-10-18)
        ++++++++++++++++++
        
        * Offline (fast) test suite.
        * Session dictionary argument merging.
        
        
        0.6.4 (2011-10-13)
        ++++++++++++++++++
        
        * Automatic decoding of unicode, based on HTTP Headers.
        * New ``decode_unicode`` setting.
        * Removal of ``r.read/close`` methods.
        * New ``r.faw`` interface for advanced response usage.*
        * Automatic expansion of parameterized headers.
        
        
        0.6.3 (2011-10-13)
        ++++++++++++++++++
        
        * Beautiful ``requests.async`` module, for making async requests w/ gevent.
        
        
        0.6.2 (2011-10-09)
        ++++++++++++++++++
        
        * GET/HEAD obeys allow_redirects=False.
        
        
        0.6.1 (2011-08-20)
        ++++++++++++++++++
        
        * Enhanced status codes experience ``\o/``
        * Set a maximum number of redirects (``settings.max_redirects``)
        * Full Unicode URL support
        * Support for protocol-less redirects.
        * Allow for arbitrary request types.
        * Bugfixes
        
        
        0.6.0 (2011-08-17)
        ++++++++++++++++++
        
        * New callback hook system
        * New persistient sessions object and context manager
        * Transparent Dict-cookie handling
        * Status code reference object
        * Removed Response.cached
        * Added Response.request
        * All args are kwargs
        * Relative redirect support
        * HTTPError handling improvements
        * Improved https testing
        * Bugfixes
        
        
        0.5.1 (2011-07-23)
        ++++++++++++++++++
        
        * International Domain Name Support!
        * Access headers without fetching entire body (``read()``)
        * Use lists as dicts for parameters
        * Add Forced Basic Authentication
        * Forced Basic is default authentication type
        * ``python-requests.org`` default User-Agent header
        * CaseInsensitiveDict lower-case caching
        * Response.history bugfix
        
        
        0.5.0 (2011-06-21)
        ++++++++++++++++++
        
        * PATCH Support
        * Support for Proxies
        * HTTPBin Test Suite
        * Redirect Fixes
        * settings.verbose stream writing
        * Querystrings for all methods
        * URLErrors (Connection Refused, Timeout, Invalid URLs) are treated as explicity raised
          ``r.requests.get('hwe://blah'); r.raise_for_status()``
        
        
        0.4.1 (2011-05-22)
        ++++++++++++++++++
        
        * Improved Redirection Handling
        * New 'allow_redirects' param for following non-GET/HEAD Redirects
        * Settings module refactoring
        
        
        0.4.0 (2011-05-15)
        ++++++++++++++++++
        
        * Response.history: list of redirected responses
        * Case-Insensitive Header Dictionaries!
        * Unicode URLs
        
        
        0.3.4 (2011-05-14)
        ++++++++++++++++++
        
        * Urllib2 HTTPAuthentication Recursion fix (Basic/Digest)
        * Internal Refactor
        * Bytes data upload Bugfix
        
        
        
        0.3.3 (2011-05-12)
        ++++++++++++++++++
        
        * Request timeouts
        * Unicode url-encoded data
        * Settings context manager and module
        
        
        0.3.2 (2011-04-15)
        ++++++++++++++++++
        
        * Automatic Decompression of GZip Encoded Content
        * AutoAuth Support for Tupled HTTP Auth
        
        
        0.3.1 (2011-04-01)
        ++++++++++++++++++
        
        * Cookie Changes
        * Response.read()
        * Poster fix
        
        
        0.3.0 (2011-02-25)
        ++++++++++++++++++
        
        * Automatic Authentication API Change
        * Smarter Query URL Parameterization
        * Allow file uploads and POST data together
        * New Authentication Manager System
            - Simpler Basic HTTP System
            - Supports all build-in urllib2 Auths
            - Allows for custom Auth Handlers
        
        
        0.2.4 (2011-02-19)
        ++++++++++++++++++
        
        * Python 2.5 Support
        * PyPy-c v1.4 Support
        * Auto-Authentication tests
        * Improved Request object constructor
        
        0.2.3 (2011-02-15)
        ++++++++++++++++++
        
        * New HTTPHandling Methods
            - Response.__nonzero__ (false if bad HTTP Status)
            - Response.ok (True if expected HTTP Status)
            - Response.error (Logged HTTPError if bad HTTP Status)
            - Response.raise_for_status() (Raises stored HTTPError)
        
        
        0.2.2 (2011-02-14)
        ++++++++++++++++++
        
        * Still handles request in the event of an HTTPError. (Issue #2)
        * Eventlet and Gevent Monkeypatch support.
        * Cookie Support (Issue #1)
        
        
        0.2.1 (2011-02-14)
        ++++++++++++++++++
        
        * Added file attribute to POST and PUT requests for multipart-encode file uploads.
        * Added Request.url attribute for context and redirects
        
        
        0.2.0 (2011-02-14)
        ++++++++++++++++++
        
        * Birth!
        
        
        0.0.1 (2011-02-13)
        ++++++++++++++++++
        
        * Frustration
        * Conception
        
        
Platform: UNKNOWN
Classifier: Development Status :: 5 - Production/Stable
Classifier: Intended Audience :: Developers
Classifier: Natural Language :: English
Classifier: License :: OSI Approved :: Apache Software License
Classifier: Programming Language :: Python
Classifier: Programming Language :: Python :: 2.6
Classifier: Programming Language :: Python :: 2.7
Classifier: Programming Language :: Python :: 3
Classifier: Programming Language :: Python :: 3.3
