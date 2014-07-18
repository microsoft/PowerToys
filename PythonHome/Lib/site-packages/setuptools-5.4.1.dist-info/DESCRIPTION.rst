===============================
Installing and Using Setuptools
===============================

.. contents:: **Table of Contents**


-------------------------
Installation Instructions
-------------------------

The recommended way to bootstrap setuptools on any system is to download
`ez_setup.py`_ and run it using the target Python environment. Different
operating systems have different recommended techniques to accomplish this
basic routine, so below are some examples to get you started.

Setuptools requires Python 2.6 or later. To install setuptools
on Python 2.4 or Python 2.5, use the `bootstrap script for Setuptools 1.x
<https://bitbucket.org/pypa/setuptools/raw/bootstrap-py24/ez_setup.py>`_.

The link provided to ez_setup.py is a bookmark to bootstrap script for the
latest known stable release.

.. _ez_setup.py: https://bootstrap.pypa.io/ez_setup.py

Windows 8 (Powershell)
======================

For best results, uninstall previous versions FIRST (see `Uninstalling`_).

Using Windows 8 or later, it's possible to install with one simple Powershell
command. Start up Powershell and paste this command::

    > (Invoke-WebRequest https://bootstrap.pypa.io/ez_setup.py).Content | python -

You must start the Powershell with Administrative privileges or you may choose
to install a user-local installation::

    > (Invoke-WebRequest https://bootstrap.pypa.io/ez_setup.py).Content | python - --user

If you have Python 3.3 or later, you can use the ``py`` command to install to
different Python versions. For example, to install to Python 3.3 if you have
Python 2.7 installed::

    > (Invoke-WebRequest https://bootstrap.pypa.io/ez_setup.py).Content | py -3 -

The recommended way to install setuptools on Windows is to download
`ez_setup.py`_ and run it. The script will download the appropriate .egg
file and install it for you.

Once installation is complete, you will find an ``easy_install`` program in
your Python ``Scripts`` subdirectory.  For simple invocation and best results,
add this directory to your ``PATH`` environment variable, if it is not already
present. If you did a user-local install, the ``Scripts`` subdirectory is
``$env:APPDATA\Python\Scripts``.


Windows 7 (or graphical install)
================================

For Windows 7 and earlier, download `ez_setup.py`_ using your favorite web
browser or other technique and "run" that file.


Unix (wget)
===========

Most Linux distributions come with wget.

Download `ez_setup.py`_ and run it using the target Python version. The script
will download the appropriate version and install it for you::

    > wget https://bootstrap.pypa.io/ez_setup.py -O - | python

Note that you will may need to invoke the command with superuser privileges to
install to the system Python::

    > wget https://bootstrap.pypa.io/ez_setup.py -O - | sudo python

Alternatively, Setuptools may be installed to a user-local path::

    > wget https://bootstrap.pypa.io/ez_setup.py -O - | python - --user

Unix including Mac OS X (curl)
==============================

If your system has curl installed, follow the ``wget`` instructions but
replace ``wget`` with ``curl`` and ``-O`` with ``-o``. For example::

    > curl https://bootstrap.pypa.io/ez_setup.py -o - | python


Advanced Installation
=====================

For more advanced installation options, such as installing to custom
locations or prefixes, download and extract the source
tarball from `Setuptools on PyPI <https://pypi.python.org/pypi/setuptools>`_
and run setup.py with any supported distutils and Setuptools options.
For example::

    setuptools-x.x$ python setup.py install --prefix=/opt/setuptools

Use ``--help`` to get a full options list, but we recommend consulting
the `EasyInstall manual`_ for detailed instructions, especially `the section
on custom installation locations`_.

.. _EasyInstall manual: https://pythonhosted.org/setuptools/EasyInstall
.. _the section on custom installation locations: https://pythonhosted.org/setuptools/EasyInstall#custom-installation-locations


Downloads
=========

All setuptools downloads can be found at `the project's home page in the Python
Package Index`_.  Scroll to the very bottom of the page to find the links.

.. _the project's home page in the Python Package Index: https://pypi.python.org/pypi/setuptools

In addition to the PyPI downloads, the development version of ``setuptools``
is available from the `Bitbucket repo`_, and in-development versions of the
`0.6 branch`_ are available as well.

.. _Bitbucket repo: https://bitbucket.org/pypa/setuptools/get/default.tar.gz#egg=setuptools-dev
.. _0.6 branch: http://svn.python.org/projects/sandbox/branches/setuptools-0.6/#egg=setuptools-dev06

Uninstalling
============

On Windows, if Setuptools was installed using an ``.exe`` or ``.msi``
installer, simply use the uninstall feature of "Add/Remove Programs" in the
Control Panel.

Otherwise, to uninstall Setuptools or Distribute, regardless of the Python
version, delete all ``setuptools*`` and ``distribute*`` files and
directories from your system's ``site-packages`` directory
(and any other ``sys.path`` directories) FIRST.

If you are upgrading or otherwise plan to re-install Setuptools or Distribute,
nothing further needs to be done. If you want to completely remove Setuptools,
you may also want to remove the 'easy_install' and 'easy_install-x.x' scripts
and associated executables installed to the Python scripts directory.

--------------------------------
Using Setuptools and EasyInstall
--------------------------------

Here are some of the available manuals, tutorials, and other resources for
learning about Setuptools, Python Eggs, and EasyInstall:

* `The EasyInstall user's guide and reference manual`_
* `The setuptools Developer's Guide`_
* `The pkg_resources API reference`_
* `Package Compatibility Notes`_ (user-maintained)
* `The Internal Structure of Python Eggs`_

Questions, comments, and bug reports should be directed to the `distutils-sig
mailing list`_.  If you have written (or know of) any tutorials, documentation,
plug-ins, or other resources for setuptools users, please let us know about
them there, so this reference list can be updated.  If you have working,
*tested* patches to correct problems or add features, you may submit them to
the `setuptools bug tracker`_.

.. _setuptools bug tracker: https://bitbucket.org/pypa/setuptools/issues
.. _Package Compatibility Notes: https://pythonhosted.org/setuptools/PackageNotes
.. _The Internal Structure of Python Eggs: https://pythonhosted.org/setuptools/formats.html
.. _The setuptools Developer's Guide: https://pythonhosted.org/setuptools/setuptools.html
.. _The pkg_resources API reference: https://pythonhosted.org/setuptools/pkg_resources.html
.. _The EasyInstall user's guide and reference manual: https://pythonhosted.org/setuptools/easy_install.html
.. _distutils-sig mailing list: http://mail.python.org/pipermail/distutils-sig/


-------
Credits
-------

* The original design for the ``.egg`` format and the ``pkg_resources`` API was
  co-created by Phillip Eby and Bob Ippolito.  Bob also implemented the first
  version of ``pkg_resources``, and supplied the OS X operating system version
  compatibility algorithm.

* Ian Bicking implemented many early "creature comfort" features of
  easy_install, including support for downloading via Sourceforge and
  Subversion repositories.  Ian's comments on the Web-SIG about WSGI
  application deployment also inspired the concept of "entry points" in eggs,
  and he has given talks at PyCon and elsewhere to inform and educate the
  community about eggs and setuptools.

* Jim Fulton contributed time and effort to build automated tests of various
  aspects of ``easy_install``, and supplied the doctests for the command-line
  ``.exe`` wrappers on Windows.

* Phillip J. Eby is the seminal author of setuptools, and
  first proposed the idea of an importable binary distribution format for
  Python application plug-ins.

* Significant parts of the implementation of setuptools were funded by the Open
  Source Applications Foundation, to provide a plug-in infrastructure for the
  Chandler PIM application.  In addition, many OSAF staffers (such as Mike
  "Code Bear" Taylor) contributed their time and stress as guinea pigs for the
  use of eggs and setuptools, even before eggs were "cool".  (Thanks, guys!)

* Tarek Ziadé is the principal author of the Distribute fork, which
  re-invigorated the community on the project, encouraged renewed innovation,
  and addressed many defects.

* Since the merge with Distribute, Jason R. Coombs is the
  maintainer of setuptools.  The project is maintained in coordination with
  the Python Packaging Authority (PyPA) and the larger Python community.

.. _files:

=======
CHANGES
=======

-----
5.4.1
-----

* `Python #7776 <http://bugs.python.org/issue7776>`_: (ssl_support) Correct usage of host for validation when
  tunneling for HTTPS.

---
5.4
---

* `Issue #154 <https://bitbucket.org/pypa/setuptools/issue/154>`_: ``pkg_resources`` will now cache the zip manifests rather than
  re-processing the same file from disk multiple times, but only if the
  environment variable ``PKG_RESOURCES_CACHE_ZIP_MANIFESTS`` is set. Clients
  that package many modules in the same zip file will see some improvement
  in startup time by enabling this feature. This feature is not enabled by
  default because it causes a substantial increase in memory usage.

---
5.3
---

* `Issue #185 <https://bitbucket.org/pypa/setuptools/issue/185>`_: Make svn tagging work on the new style SVN metadata.
  Thanks cazabon!
* Prune revision control directories (e.g .svn) from base path
  as well as sub-directories.

---
5.2
---

* Added a `Developer Guide
  <https://pythonhosted.org/setuptools/developer-guide.html>`_ to the official
  documentation.
* Some code refactoring and cleanup was done with no intended behavioral
  changes.
* During install_egg_info, the generated lines for namespace package .pth
  files are now processed even during a dry run.

---
5.1
---

* `Issue #202 <https://bitbucket.org/pypa/setuptools/issue/202>`_: Implemented more robust cache invalidation for the ZipImporter,
  building on the work in `Issue #168 <https://bitbucket.org/pypa/setuptools/issue/168>`_. Special thanks to Jurko Gospodnetic and
  PJE.

-----
5.0.2
-----

* `Issue #220 <https://bitbucket.org/pypa/setuptools/issue/220>`_: Restored script templates.

-----
5.0.1
-----

* Renamed script templates to end with .tmpl now that they no longer need
  to be processed by 2to3. Fixes spurious syntax errors during build/install.

---
5.0
---

* `Issue #218 <https://bitbucket.org/pypa/setuptools/issue/218>`_: Re-release of 3.8.1 to signal that it supersedes 4.x.
* Incidentally, script templates were updated not to include the triple-quote
  escaping.

-------------------------
3.7.1 and 3.8.1 and 4.0.1
-------------------------

* `Issue #213 <https://bitbucket.org/pypa/setuptools/issue/213>`_: Use legacy StringIO behavior for compatibility under pbr.
* `Issue #218 <https://bitbucket.org/pypa/setuptools/issue/218>`_: Setuptools 3.8.1 superseded 4.0.1, and 4.x was removed
  from the available versions to install.

---
4.0
---

* `Issue #210 <https://bitbucket.org/pypa/setuptools/issue/210>`_: ``setup.py develop`` now copies scripts in binary mode rather
  than text mode, matching the behavior of the ``install`` command.

---
3.8
---

* Extend `Issue #197 <https://bitbucket.org/pypa/setuptools/issue/197>`_ workaround to include all Python 3 versions prior to
  3.2.2.

---
3.7
---

* `Issue #193 <https://bitbucket.org/pypa/setuptools/issue/193>`_: Improved handling of Unicode filenames when building manifests.

---
3.6
---

* `Issue #203 <https://bitbucket.org/pypa/setuptools/issue/203>`_: Honor proxy settings for Powershell downloader in the bootstrap
  routine.

-----
3.5.2
-----

* `Issue #168 <https://bitbucket.org/pypa/setuptools/issue/168>`_: More robust handling of replaced zip files and stale caches.
  Fixes ZipImportError complaining about a 'bad local header'.

-----
3.5.1
-----

* `Issue #199 <https://bitbucket.org/pypa/setuptools/issue/199>`_: Restored ``install._install`` for compatibility with earlier
  NumPy versions.

---
3.5
---

* `Issue #195 <https://bitbucket.org/pypa/setuptools/issue/195>`_: Follow symbolic links in find_packages (restoring behavior
  broken in 3.4).
* `Issue #197 <https://bitbucket.org/pypa/setuptools/issue/197>`_: On Python 3.1, PKG-INFO is now saved in a UTF-8 encoding instead
  of ``sys.getpreferredencoding`` to match the behavior on Python 2.6-3.4.
* `Issue #192 <https://bitbucket.org/pypa/setuptools/issue/192>`_: Preferred bootstrap location is now
  https://bootstrap.pypa.io/ez_setup.py (mirrored from former location).

-----
3.4.4
-----

* `Issue #184 <https://bitbucket.org/pypa/setuptools/issue/184>`_: Correct failure where find_package over-matched packages
  when directory traversal isn't short-circuited.

-----
3.4.3
-----

* `Issue #183 <https://bitbucket.org/pypa/setuptools/issue/183>`_: Really fix test command with Python 3.1.

-----
3.4.2
-----

* `Issue #183 <https://bitbucket.org/pypa/setuptools/issue/183>`_: Fix additional regression in test command on Python 3.1.

-----
3.4.1
-----

* `Issue #180 <https://bitbucket.org/pypa/setuptools/issue/180>`_: Fix regression in test command not caught by py.test-run tests.

---
3.4
---

* `Issue #176 <https://bitbucket.org/pypa/setuptools/issue/176>`_: Add parameter to the test command to support a custom test
  runner: --test-runner or -r.
* `Issue #177 <https://bitbucket.org/pypa/setuptools/issue/177>`_: Now assume most common invocation to install command on
  platforms/environments without stack support (issuing a warning). Setuptools
  now installs naturally on IronPython. Behavior on CPython should be
  unchanged.

---
3.3
---

* Add ``include`` parameter to ``setuptools.find_packages()``.

---
3.2
---

* `Pull Request #39 <https://bitbucket.org/pypa/setuptools/pull-request/39>`_: Add support for C++ targets from Cython ``.pyx`` files.
* `Issue #162 <https://bitbucket.org/pypa/setuptools/issue/162>`_: Update dependency on certifi to 1.0.1.
* `Issue #164 <https://bitbucket.org/pypa/setuptools/issue/164>`_: Update dependency on wincertstore to 0.2.

---
3.1
---

* `Issue #161 <https://bitbucket.org/pypa/setuptools/issue/161>`_: Restore Features functionality to allow backward compatibility
  (for Features) until the uses of that functionality is sufficiently removed.

-----
3.0.2
-----

* Correct typo in previous bugfix.

-----
3.0.1
-----

* `Issue #157 <https://bitbucket.org/pypa/setuptools/issue/157>`_: Restore support for Python 2.6 in bootstrap script where
  ``zipfile.ZipFile`` does not yet have support for context managers.

---
3.0
---

* `Issue #125 <https://bitbucket.org/pypa/setuptools/issue/125>`_: Prevent Subversion support from creating a ~/.subversion
  directory just for checking the presence of a Subversion repository.
* `Issue #12 <https://bitbucket.org/pypa/setuptools/issue/12>`_: Namespace packages are now imported lazily.  That is, the mere
  declaration of a namespace package in an egg on ``sys.path`` no longer
  causes it to be imported when ``pkg_resources`` is imported.  Note that this
  change means that all of a namespace package's ``__init__.py`` files must
  include a ``declare_namespace()`` call in order to ensure that they will be
  handled properly at runtime.  In 2.x it was possible to get away without
  including the declaration, but only at the cost of forcing namespace
  packages to be imported early, which 3.0 no longer does.
* `Issue #148 <https://bitbucket.org/pypa/setuptools/issue/148>`_: When building (bdist_egg), setuptools no longer adds
  ``__init__.py`` files to namespace packages. Any packages that rely on this
  behavior will need to create ``__init__.py`` files and include the
  ``declare_namespace()``.
* `Issue #7 <https://bitbucket.org/pypa/setuptools/issue/7>`_: Setuptools itself is now distributed as a zip archive in addition to
  tar archive. ez_setup.py now uses zip archive. This approach avoids the potential
  security vulnerabilities presented by use of tar archives in ez_setup.py.
  It also leverages the security features added to ZipFile.extract in Python 2.7.4.
* `Issue #65 <https://bitbucket.org/pypa/setuptools/issue/65>`_: Removed deprecated Features functionality.
* `Pull Request #28 <https://bitbucket.org/pypa/setuptools/pull-request/28>`_: Remove backport of ``_bytecode_filenames`` which is
  available in Python 2.6 and later, but also has better compatibility with
  Python 3 environments.
* `Issue #156 <https://bitbucket.org/pypa/setuptools/issue/156>`_: Fix spelling of __PYVENV_LAUNCHER__ variable.

---
2.2
---

* `Issue #141 <https://bitbucket.org/pypa/setuptools/issue/141>`_: Restored fix for allowing setup_requires dependencies to
  override installed dependencies during setup.
* `Issue #128 <https://bitbucket.org/pypa/setuptools/issue/128>`_: Fixed issue where only the first dependency link was honored
  in a distribution where multiple dependency links were supplied.

-----
2.1.2
-----

* `Issue #144 <https://bitbucket.org/pypa/setuptools/issue/144>`_: Read long_description using codecs module to avoid errors
  installing on systems where LANG=C.

-----
2.1.1
-----

* `Issue #139 <https://bitbucket.org/pypa/setuptools/issue/139>`_: Fix regression in re_finder for CVS repos (and maybe Git repos
  as well).

---
2.1
---

* `Issue #129 <https://bitbucket.org/pypa/setuptools/issue/129>`_: Suppress inspection of ``*.whl`` files when searching for files
  in a zip-imported file.
* `Issue #131 <https://bitbucket.org/pypa/setuptools/issue/131>`_: Fix RuntimeError when constructing an egg fetcher.

-----
2.0.2
-----

* Fix NameError during installation with Python implementations (e.g. Jython)
  not containing parser module.
* Fix NameError in ``sdist:re_finder``.

-----
2.0.1
-----

* `Issue #124 <https://bitbucket.org/pypa/setuptools/issue/124>`_: Fixed error in list detection in upload_docs.

---
2.0
---

* `Issue #121 <https://bitbucket.org/pypa/setuptools/issue/121>`_: Exempt lib2to3 pickled grammars from DirectorySandbox.
* `Issue #41 <https://bitbucket.org/pypa/setuptools/issue/41>`_: Dropped support for Python 2.4 and Python 2.5. Clients requiring
  setuptools for those versions of Python should use setuptools 1.x.
* Removed ``setuptools.command.easy_install.HAS_USER_SITE``. Clients
  expecting this boolean variable should use ``site.ENABLE_USER_SITE``
  instead.
* Removed ``pkg_resources.ImpWrapper``. Clients that expected this class
  should use ``pkgutil.ImpImporter`` instead.

-----
1.4.2
-----

* `Issue #116 <https://bitbucket.org/pypa/setuptools/issue/116>`_: Correct TypeError when reading a local package index on Python
  3.

-----
1.4.1
-----

* `Issue #114 <https://bitbucket.org/pypa/setuptools/issue/114>`_: Use ``sys.getfilesystemencoding`` for decoding config in
  ``bdist_wininst`` distributions.

* `Issue #105 <https://bitbucket.org/pypa/setuptools/issue/105>`_ and `Issue #113 <https://bitbucket.org/pypa/setuptools/issue/113>`_: Establish a more robust technique for
  determining the terminal encoding::

    1. Try ``getpreferredencoding``
    2. If that returns US_ASCII or None, try the encoding from
       ``getdefaultlocale``. If that encoding was a "fallback" because Python
       could not figure it out from the environment or OS, encoding remains
       unresolved.
    3. If the encoding is resolved, then make sure Python actually implements
       the encoding.
    4. On the event of an error or unknown codec, revert to fallbacks
       (UTF-8 on Darwin, ASCII on everything else).
    5. On the encoding is 'mac-roman' on Darwin, use UTF-8 as 'mac-roman' was
       a bug on older Python releases.

    On a side note, it would seem that the encoding only matters for when SVN
    does not yet support ``--xml`` and when getting repository and svn version
    numbers. The ``--xml`` technique should yield UTF-8 according to some
    messages on the SVN mailing lists. So if the version numbers are always
    7-bit ASCII clean, it may be best to only support the file parsing methods
    for legacy SVN releases and support for SVN without the subprocess command
    would simple go away as support for the older SVNs does.

---
1.4
---

* `Issue #27 <https://bitbucket.org/pypa/setuptools/issue/27>`_: ``easy_install`` will now use credentials from .pypirc if
  present for connecting to the package index.
* `Pull Request #21 <https://bitbucket.org/pypa/setuptools/pull-request/21>`_: Omit unwanted newlines in ``package_index._encode_auth``
  when the username/password pair length indicates wrapping.

-----
1.3.2
-----

* `Issue #99 <https://bitbucket.org/pypa/setuptools/issue/99>`_: Fix filename encoding issues in SVN support.

-----
1.3.1
-----

* Remove exuberant warning in SVN support when SVN is not used.

---
1.3
---

* Address security vulnerability in SSL match_hostname check as reported in
  `Python #17997 <http://bugs.python.org/issue17997>`_.
* Prefer `backports.ssl_match_hostname
  <https://pypi.python.org/pypi/backports.ssl_match_hostname>`_ for backport
  implementation if present.
* Correct NameError in ``ssl_support`` module (``socket.error``).

---
1.2
---

* `Issue #26 <https://bitbucket.org/pypa/setuptools/issue/26>`_: Add support for SVN 1.7. Special thanks to Philip Thiem for the
  contribution.
* `Issue #93 <https://bitbucket.org/pypa/setuptools/issue/93>`_: Wheels are now distributed with every release. Note that as
  reported in `Issue #108 <https://bitbucket.org/pypa/setuptools/issue/108>`_, as of Pip 1.4, scripts aren't installed properly
  from wheels. Therefore, if using Pip to install setuptools from a wheel,
  the ``easy_install`` command will not be available.
* Setuptools "natural" launcher support, introduced in 1.0, is now officially
  supported.

-----
1.1.7
-----

* Fixed behavior of NameError handling in 'script template (dev).py' (script
  launcher for 'develop' installs).
* ``ez_setup.py`` now ensures partial downloads are cleaned up following
  a failed download.
* `Distribute #363 <https://bitbucket.org/tarek/distribute/issue/363>`_ and `Issue #55 <https://bitbucket.org/pypa/setuptools/issue/55>`_: Skip an sdist test that fails on locales
  other than UTF-8.

-----
1.1.6
-----

* `Distribute #349 <https://bitbucket.org/tarek/distribute/issue/349>`_: ``sandbox.execfile`` now opens the target file in binary
  mode, thus honoring a BOM in the file when compiled.

-----
1.1.5
-----

* `Issue #69 <https://bitbucket.org/pypa/setuptools/issue/69>`_: Second attempt at fix (logic was reversed).

-----
1.1.4
-----

* `Issue #77 <https://bitbucket.org/pypa/setuptools/issue/77>`_: Fix error in upload command (Python 2.4).

-----
1.1.3
-----

* Fix NameError in previous patch.

-----
1.1.2
-----

* `Issue #69 <https://bitbucket.org/pypa/setuptools/issue/69>`_: Correct issue where 404 errors are returned for URLs with
  fragments in them (such as #egg=).

-----
1.1.1
-----

* `Issue #75 <https://bitbucket.org/pypa/setuptools/issue/75>`_: Add ``--insecure`` option to ez_setup.py to accommodate
  environments where a trusted SSL connection cannot be validated.
* `Issue #76 <https://bitbucket.org/pypa/setuptools/issue/76>`_: Fix AttributeError in upload command with Python 2.4.

---
1.1
---

* `Issue #71 <https://bitbucket.org/pypa/setuptools/issue/71>`_ (`Distribute #333 <https://bitbucket.org/tarek/distribute/issue/333>`_): EasyInstall now puts less emphasis on the
  condition when a host is blocked via ``--allow-hosts``.
* `Issue #72 <https://bitbucket.org/pypa/setuptools/issue/72>`_: Restored Python 2.4 compatibility in ``ez_setup.py``.

---
1.0
---

* `Issue #60 <https://bitbucket.org/pypa/setuptools/issue/60>`_: On Windows, Setuptools supports deferring to another launcher,
  such as Vinay Sajip's `pylauncher <https://bitbucket.org/pypa/pylauncher>`_
  (included with Python 3.3) to launch console and GUI scripts and not install
  its own launcher executables. This experimental functionality is currently
  only enabled if  the ``SETUPTOOLS_LAUNCHER`` environment variable is set to
  "natural". In the future, this behavior may become default, but only after
  it has matured and seen substantial adoption. The ``SETUPTOOLS_LAUNCHER``
  also accepts "executable" to force the default behavior of creating launcher
  executables.
* `Issue #63 <https://bitbucket.org/pypa/setuptools/issue/63>`_: Bootstrap script (ez_setup.py) now prefers Powershell, curl, or
  wget for retrieving the Setuptools tarball for improved security of the
  install. The script will still fall back to a simple ``urlopen`` on
  platforms that do not have these tools.
* `Issue #65 <https://bitbucket.org/pypa/setuptools/issue/65>`_: Deprecated the ``Features`` functionality.
* `Issue #52 <https://bitbucket.org/pypa/setuptools/issue/52>`_: In ``VerifyingHTTPSConn``, handle a tunnelled (proxied)
  connection.

Backward-Incompatible Changes
=============================

This release includes a couple of backward-incompatible changes, but most if
not all users will find 1.0 a drop-in replacement for 0.9.

* `Issue #50 <https://bitbucket.org/pypa/setuptools/issue/50>`_: Normalized API of environment marker support. Specifically,
  removed line number and filename from SyntaxErrors when returned from
  `pkg_resources.invalid_marker`. Any clients depending on the specific
  string representation of exceptions returned by that function may need to
  be updated to account for this change.
* `Issue #50 <https://bitbucket.org/pypa/setuptools/issue/50>`_: SyntaxErrors generated by `pkg_resources.invalid_marker` are
  normalized for cross-implementation consistency.
* Removed ``--ignore-conflicts-at-my-risk`` and ``--delete-conflicting``
  options to easy_install. These options have been deprecated since 0.6a11.

-----
0.9.8
-----

* `Issue #53 <https://bitbucket.org/pypa/setuptools/issue/53>`_: Fix NameErrors in `_vcs_split_rev_from_url`.

-----
0.9.7
-----

* `Issue #49 <https://bitbucket.org/pypa/setuptools/issue/49>`_: Correct AttributeError on PyPy where a hashlib.HASH object does
  not have a `.name` attribute.
* `Issue #34 <https://bitbucket.org/pypa/setuptools/issue/34>`_: Documentation now refers to bootstrap script in code repository
  referenced by bookmark.
* Add underscore-separated keys to environment markers (markerlib).

-----
0.9.6
-----

* `Issue #44 <https://bitbucket.org/pypa/setuptools/issue/44>`_: Test failure on Python 2.4 when MD5 hash doesn't have a `.name`
  attribute.

-----
0.9.5
-----

* `Python #17980 <http://bugs.python.org/issue17980>`_: Fix security vulnerability in SSL certificate validation.

-----
0.9.4
-----

* `Issue #43 <https://bitbucket.org/pypa/setuptools/issue/43>`_: Fix issue (introduced in 0.9.1) with version resolution when
  upgrading over other releases of Setuptools.

-----
0.9.3
-----

* `Issue #42 <https://bitbucket.org/pypa/setuptools/issue/42>`_: Fix new ``AttributeError`` introduced in last fix.

-----
0.9.2
-----

* `Issue #42 <https://bitbucket.org/pypa/setuptools/issue/42>`_: Fix regression where blank checksums would trigger an
  ``AttributeError``.

-----
0.9.1
-----

* `Distribute #386 <https://bitbucket.org/tarek/distribute/issue/386>`_: Allow other positional and keyword arguments to os.open.
* Corrected dependency on certifi mis-referenced in 0.9.

---
0.9
---

* `package_index` now validates hashes other than MD5 in download links.

---
0.8
---

* Code base now runs on Python 2.4 - Python 3.3 without Python 2to3
  conversion.

-----
0.7.8
-----

* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Yet another fix for yet another regression.

-----
0.7.7
-----

* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Repair AttributeError created in last release (redo).
* `Issue #30 <https://bitbucket.org/pypa/setuptools/issue/30>`_: Added test for get_cache_path.

-----
0.7.6
-----

* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Repair AttributeError created in last release.

-----
0.7.5
-----

* `Issue #21 <https://bitbucket.org/pypa/setuptools/issue/21>`_: Restore Python 2.4 compatibility in ``test_easy_install``.
* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Merged additional warning from Distribute 0.6.46.
* Now honor the environment variable
  ``SETUPTOOLS_DISABLE_VERSIONED_EASY_INSTALL_SCRIPT`` in addition to the now
  deprecated ``DISTRIBUTE_DISABLE_VERSIONED_EASY_INSTALL_SCRIPT``.

-----
0.7.4
-----

* `Issue #20 <https://bitbucket.org/pypa/setuptools/issue/20>`_: Fix comparison of parsed SVN version on Python 3.

-----
0.7.3
-----

* `Issue #1 <https://bitbucket.org/pypa/setuptools/issue/1>`_: Disable installation of Windows-specific files on non-Windows systems.
* Use new sysconfig module with Python 2.7 or >=3.2.

-----
0.7.2
-----

* `Issue #14 <https://bitbucket.org/pypa/setuptools/issue/14>`_: Use markerlib when the `parser` module is not available.
* `Issue #10 <https://bitbucket.org/pypa/setuptools/issue/10>`_: ``ez_setup.py`` now uses HTTPS to download setuptools from PyPI.

-----
0.7.1
-----

* Fix NameError (`Issue #3 <https://bitbucket.org/pypa/setuptools/issue/3>`_) again - broken in bad merge.

---
0.7
---

* Merged Setuptools and Distribute. See docs/merge.txt for details.

Added several features that were slated for setuptools 0.6c12:

* Index URL now defaults to HTTPS.
* Added experimental environment marker support. Now clients may designate a
  PEP-426 environment marker for "extra" dependencies. Setuptools uses this
  feature in ``setup.py`` for optional SSL and certificate validation support
  on older platforms. Based on Distutils-SIG discussions, the syntax is
  somewhat tentative. There should probably be a PEP with a firmer spec before
  the feature should be considered suitable for use.
* Added support for SSL certificate validation when installing packages from
  an HTTPS service.

-----
0.7b4
-----

* `Issue #3 <https://bitbucket.org/pypa/setuptools/issue/3>`_: Fixed NameError in SSL support.

------
0.6.49
------

* Move warning check in ``get_cache_path`` to follow the directory creation
  to avoid errors when the cache path does not yet exist. Fixes the error
  reported in `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_.

------
0.6.48
------

* Correct AttributeError in ``ResourceManager.get_cache_path`` introduced in
  0.6.46 (redo).

------
0.6.47
------

* Correct AttributeError in ``ResourceManager.get_cache_path`` introduced in
  0.6.46.

------
0.6.46
------

* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Issue a warning if the PYTHON_EGG_CACHE or otherwise
  customized egg cache location specifies a directory that's group- or
  world-writable.

------
0.6.45
------

* `Distribute #379 <https://bitbucket.org/tarek/distribute/issue/379>`_: ``distribute_setup.py`` now traps VersionConflict as well,
  restoring ability to upgrade from an older setuptools version.

------
0.6.44
------

* ``distribute_setup.py`` has been updated to allow Setuptools 0.7 to
  satisfy use_setuptools.

------
0.6.43
------

* `Distribute #378 <https://bitbucket.org/tarek/distribute/issue/378>`_: Restore support for Python 2.4 Syntax (regression in 0.6.42).

------
0.6.42
------

* External links finder no longer yields duplicate links.
* `Distribute #337 <https://bitbucket.org/tarek/distribute/issue/337>`_: Moved site.py to setuptools/site-patch.py (graft of very old
  patch from setuptools trunk which inspired PR `#31 <https://bitbucket.org/pypa/setuptools/issue/31>`_).

------
0.6.41
------

* `Distribute #27 <https://bitbucket.org/tarek/distribute/issue/27>`_: Use public api for loading resources from zip files rather than
  the private method `_zip_directory_cache`.
* Added a new function ``easy_install.get_win_launcher`` which may be used by
  third-party libraries such as buildout to get a suitable script launcher.

------
0.6.40
------

* `Distribute #376 <https://bitbucket.org/tarek/distribute/issue/376>`_: brought back cli.exe and gui.exe that were deleted in the
  previous release.

------
0.6.39
------

* Add support for console launchers on ARM platforms.
* Fix possible issue in GUI launchers where the subsystem was not supplied to
  the linker.
* Launcher build script now refactored for robustness.
* `Distribute #375 <https://bitbucket.org/tarek/distribute/issue/375>`_: Resources extracted from a zip egg to the file system now also
  check the contents of the file against the zip contents during each
  invocation of get_resource_filename.

------
0.6.38
------

* `Distribute #371 <https://bitbucket.org/tarek/distribute/issue/371>`_: The launcher manifest file is now installed properly.

------
0.6.37
------

* `Distribute #143 <https://bitbucket.org/tarek/distribute/issue/143>`_: Launcher scripts, including easy_install itself, are now
  accompanied by a manifest on 32-bit Windows environments to avoid the
  Installer Detection Technology and thus undesirable UAC elevation described
  in `this Microsoft article
  <http://technet.microsoft.com/en-us/library/cc709628%28WS.10%29.aspx>`_.

------
0.6.36
------

* `Pull Request #35 <https://bitbucket.org/pypa/setuptools/pull-request/35>`_: In `Buildout #64 <https://github.com/buildout/buildout/issues/64>`_, it was reported that
  under Python 3, installation of distutils scripts could attempt to copy
  the ``__pycache__`` directory as a file, causing an error, apparently only
  under Windows. Easy_install now skips all directories when processing
  metadata scripts.

------
0.6.35
------


Note this release is backward-incompatible with distribute 0.6.23-0.6.34 in
how it parses version numbers.

* `Distribute #278 <https://bitbucket.org/tarek/distribute/issue/278>`_: Restored compatibility with distribute 0.6.22 and setuptools
  0.6. Updated the documentation to match more closely with the version
  parsing as intended in setuptools 0.6.

------
0.6.34
------

* `Distribute #341 <https://bitbucket.org/tarek/distribute/issue/341>`_: 0.6.33 fails to build under Python 2.4.

------
0.6.33
------

* Fix 2 errors with Jython 2.5.
* Fix 1 failure with Jython 2.5 and 2.7.
* Disable workaround for Jython scripts on Linux systems.
* `Distribute #336 <https://bitbucket.org/tarek/distribute/issue/336>`_: `setup.py` no longer masks failure exit code when tests fail.
* Fix issue in pkg_resources where try/except around a platform-dependent
  import would trigger hook load failures on Mercurial. See pull request 32
  for details.
* `Distribute #341 <https://bitbucket.org/tarek/distribute/issue/341>`_: Fix a ResourceWarning.

------
0.6.32
------

* Fix test suite with Python 2.6.
* Fix some DeprecationWarnings and ResourceWarnings.
* `Distribute #335 <https://bitbucket.org/tarek/distribute/issue/335>`_: Backed out `setup_requires` superceding installed requirements
  until regression can be addressed.

------
0.6.31
------

* `Distribute #303 <https://bitbucket.org/tarek/distribute/issue/303>`_: Make sure the manifest only ever contains UTF-8 in Python 3.
* `Distribute #329 <https://bitbucket.org/tarek/distribute/issue/329>`_: Properly close files created by tests for compatibility with
  Jython.
* Work around `Jython #1980 <http://bugs.jython.org/issue1980>`_ and `Jython #1981 <http://bugs.jython.org/issue1981>`_.
* `Distribute #334 <https://bitbucket.org/tarek/distribute/issue/334>`_: Provide workaround for packages that reference `sys.__stdout__`
  such as numpy does. This change should address
  `virtualenv `#359 <https://bitbucket.org/pypa/setuptools/issue/359>`_ <https://github.com/pypa/virtualenv/issues/359>`_ as long
  as the system encoding is UTF-8 or the IO encoding is specified in the
  environment, i.e.::

     PYTHONIOENCODING=utf8 pip install numpy

* Fix for encoding issue when installing from Windows executable on Python 3.
* `Distribute #323 <https://bitbucket.org/tarek/distribute/issue/323>`_: Allow `setup_requires` requirements to supercede installed
  requirements. Added some new keyword arguments to existing pkg_resources
  methods. Also had to updated how __path__ is handled for namespace packages
  to ensure that when a new egg distribution containing a namespace package is
  placed on sys.path, the entries in __path__ are found in the same order they
  would have been in had that egg been on the path when pkg_resources was
  first imported.

------
0.6.30
------

* `Distribute #328 <https://bitbucket.org/tarek/distribute/issue/328>`_: Clean up temporary directories in distribute_setup.py.
* Fix fatal bug in distribute_setup.py.

------
0.6.29
------

* `Pull Request #14 <https://bitbucket.org/pypa/setuptools/pull-request/14>`_: Honor file permissions in zip files.
* `Distribute #327 <https://bitbucket.org/tarek/distribute/issue/327>`_: Merged pull request `#24 <https://bitbucket.org/pypa/setuptools/issue/24>`_ to fix a dependency problem with pip.
* Merged pull request `#23 <https://bitbucket.org/pypa/setuptools/issue/23>`_ to fix https://github.com/pypa/virtualenv/issues/301.
* If Sphinx is installed, the `upload_docs` command now runs `build_sphinx`
  to produce uploadable documentation.
* `Distribute #326 <https://bitbucket.org/tarek/distribute/issue/326>`_: `upload_docs` provided mangled auth credentials under Python 3.
* `Distribute #320 <https://bitbucket.org/tarek/distribute/issue/320>`_: Fix check for "createable" in distribute_setup.py.
* `Distribute #305 <https://bitbucket.org/tarek/distribute/issue/305>`_: Remove a warning that was triggered during normal operations.
* `Distribute #311 <https://bitbucket.org/tarek/distribute/issue/311>`_: Print metadata in UTF-8 independent of platform.
* `Distribute #303 <https://bitbucket.org/tarek/distribute/issue/303>`_: Read manifest file with UTF-8 encoding under Python 3.
* `Distribute #301 <https://bitbucket.org/tarek/distribute/issue/301>`_: Allow to run tests of namespace packages when using 2to3.
* `Distribute #304 <https://bitbucket.org/tarek/distribute/issue/304>`_: Prevent import loop in site.py under Python 3.3.
* `Distribute #283 <https://bitbucket.org/tarek/distribute/issue/283>`_: Reenable scanning of `*.pyc` / `*.pyo` files on Python 3.3.
* `Distribute #299 <https://bitbucket.org/tarek/distribute/issue/299>`_: The develop command didn't work on Python 3, when using 2to3,
  as the egg link would go to the Python 2 source. Linking to the 2to3'd code
  in build/lib makes it work, although you will have to rebuild the module
  before testing it.
* `Distribute #306 <https://bitbucket.org/tarek/distribute/issue/306>`_: Even if 2to3 is used, we build in-place under Python 2.
* `Distribute #307 <https://bitbucket.org/tarek/distribute/issue/307>`_: Prints the full path when .svn/entries is broken.
* `Distribute #313 <https://bitbucket.org/tarek/distribute/issue/313>`_: Support for sdist subcommands (Python 2.7)
* `Distribute #314 <https://bitbucket.org/tarek/distribute/issue/314>`_: test_local_index() would fail an OS X.
* `Distribute #310 <https://bitbucket.org/tarek/distribute/issue/310>`_: Non-ascii characters in a namespace __init__.py causes errors.
* `Distribute #218 <https://bitbucket.org/tarek/distribute/issue/218>`_: Improved documentation on behavior of `package_data` and
  `include_package_data`. Files indicated by `package_data` are now included
  in the manifest.
* `distribute_setup.py` now allows a `--download-base` argument for retrieving
  distribute from a specified location.

------
0.6.28
------

* `Distribute #294 <https://bitbucket.org/tarek/distribute/issue/294>`_: setup.py can now be invoked from any directory.
* Scripts are now installed honoring the umask.
* Added support for .dist-info directories.
* `Distribute #283 <https://bitbucket.org/tarek/distribute/issue/283>`_: Fix and disable scanning of `*.pyc` / `*.pyo` files on
  Python 3.3.

------
0.6.27
------

* Support current snapshots of CPython 3.3.
* Distribute now recognizes README.rst as a standard, default readme file.
* Exclude 'encodings' modules when removing modules from sys.modules.
  Workaround for `#285 <https://bitbucket.org/pypa/setuptools/issue/285>`_.
* `Distribute #231 <https://bitbucket.org/tarek/distribute/issue/231>`_: Don't fiddle with system python when used with buildout
  (bootstrap.py)

------
0.6.26
------

* `Distribute #183 <https://bitbucket.org/tarek/distribute/issue/183>`_: Symlinked files are now extracted from source distributions.
* `Distribute #227 <https://bitbucket.org/tarek/distribute/issue/227>`_: Easy_install fetch parameters are now passed during the
  installation of a source distribution; now fulfillment of setup_requires
  dependencies will honor the parameters passed to easy_install.

------
0.6.25
------

* `Distribute #258 <https://bitbucket.org/tarek/distribute/issue/258>`_: Workaround a cache issue
* `Distribute #260 <https://bitbucket.org/tarek/distribute/issue/260>`_: distribute_setup.py now accepts the --user parameter for
  Python 2.6 and later.
* `Distribute #262 <https://bitbucket.org/tarek/distribute/issue/262>`_: package_index.open_with_auth no longer throws LookupError
  on Python 3.
* `Distribute #269 <https://bitbucket.org/tarek/distribute/issue/269>`_: AttributeError when an exception occurs reading Manifest.in
  on late releases of Python.
* `Distribute #272 <https://bitbucket.org/tarek/distribute/issue/272>`_: Prevent TypeError when namespace package names are unicode
  and single-install-externally-managed is used. Also fixes PIP issue
  449.
* `Distribute #273 <https://bitbucket.org/tarek/distribute/issue/273>`_: Legacy script launchers now install with Python2/3 support.

------
0.6.24
------

* `Distribute #249 <https://bitbucket.org/tarek/distribute/issue/249>`_: Added options to exclude 2to3 fixers

------
0.6.23
------

* `Distribute #244 <https://bitbucket.org/tarek/distribute/issue/244>`_: Fixed a test
* `Distribute #243 <https://bitbucket.org/tarek/distribute/issue/243>`_: Fixed a test
* `Distribute #239 <https://bitbucket.org/tarek/distribute/issue/239>`_: Fixed a test
* `Distribute #240 <https://bitbucket.org/tarek/distribute/issue/240>`_: Fixed a test
* `Distribute #241 <https://bitbucket.org/tarek/distribute/issue/241>`_: Fixed a test
* `Distribute #237 <https://bitbucket.org/tarek/distribute/issue/237>`_: Fixed a test
* `Distribute #238 <https://bitbucket.org/tarek/distribute/issue/238>`_: easy_install now uses 64bit executable wrappers on 64bit Python
* `Distribute #208 <https://bitbucket.org/tarek/distribute/issue/208>`_: Fixed parsed_versions, it now honors post-releases as noted in the documentation
* `Distribute #207 <https://bitbucket.org/tarek/distribute/issue/207>`_: Windows cli and gui wrappers pass CTRL-C to child python process
* `Distribute #227 <https://bitbucket.org/tarek/distribute/issue/227>`_: easy_install now passes its arguments to setup.py bdist_egg
* `Distribute #225 <https://bitbucket.org/tarek/distribute/issue/225>`_: Fixed a NameError on Python 2.5, 2.4

------
0.6.21
------

* `Distribute #225 <https://bitbucket.org/tarek/distribute/issue/225>`_: FIxed a regression on py2.4

------
0.6.20
------

* `Distribute #135 <https://bitbucket.org/tarek/distribute/issue/135>`_: Include url in warning when processing URLs in package_index.
* `Distribute #212 <https://bitbucket.org/tarek/distribute/issue/212>`_: Fix issue where easy_instal fails on Python 3 on windows installer.
* `Distribute #213 <https://bitbucket.org/tarek/distribute/issue/213>`_: Fix typo in documentation.

------
0.6.19
------

* `Distribute #206 <https://bitbucket.org/tarek/distribute/issue/206>`_: AttributeError: 'HTTPMessage' object has no attribute 'getheaders'

------
0.6.18
------

* `Distribute #210 <https://bitbucket.org/tarek/distribute/issue/210>`_: Fixed a regression introduced by `Distribute #204 <https://bitbucket.org/tarek/distribute/issue/204>`_ fix.

------
0.6.17
------

* Support 'DISTRIBUTE_DISABLE_VERSIONED_EASY_INSTALL_SCRIPT' environment
  variable to allow to disable installation of easy_install-${version} script.
* Support Python >=3.1.4 and >=3.2.1.
* `Distribute #204 <https://bitbucket.org/tarek/distribute/issue/204>`_: Don't try to import the parent of a namespace package in
  declare_namespace
* `Distribute #196 <https://bitbucket.org/tarek/distribute/issue/196>`_: Tolerate responses with multiple Content-Length headers
* `Distribute #205 <https://bitbucket.org/tarek/distribute/issue/205>`_: Sandboxing doesn't preserve working_set. Leads to setup_requires
  problems.

------
0.6.16
------

* Builds sdist gztar even on Windows (avoiding `Distribute #193 <https://bitbucket.org/tarek/distribute/issue/193>`_).
* `Distribute #192 <https://bitbucket.org/tarek/distribute/issue/192>`_: Fixed metadata omitted on Windows when package_dir
  specified with forward-slash.
* `Distribute #195 <https://bitbucket.org/tarek/distribute/issue/195>`_: Cython build support.
* `Distribute #200 <https://bitbucket.org/tarek/distribute/issue/200>`_: Issues with recognizing 64-bit packages on Windows.

------
0.6.15
------

* Fixed typo in bdist_egg
* Several issues under Python 3 has been solved.
* `Distribute #146 <https://bitbucket.org/tarek/distribute/issue/146>`_: Fixed missing DLL files after easy_install of windows exe package.

------
0.6.14
------

* `Distribute #170 <https://bitbucket.org/tarek/distribute/issue/170>`_: Fixed unittest failure. Thanks to Toshio.
* `Distribute #171 <https://bitbucket.org/tarek/distribute/issue/171>`_: Fixed race condition in unittests cause deadlocks in test suite.
* `Distribute #143 <https://bitbucket.org/tarek/distribute/issue/143>`_: Fixed a lookup issue with easy_install.
  Thanks to David and Zooko.
* `Distribute #174 <https://bitbucket.org/tarek/distribute/issue/174>`_: Fixed the edit mode when its used with setuptools itself

------
0.6.13
------

* `Distribute #160 <https://bitbucket.org/tarek/distribute/issue/160>`_: 2.7 gives ValueError("Invalid IPv6 URL")
* `Distribute #150 <https://bitbucket.org/tarek/distribute/issue/150>`_: Fixed using ~/.local even in a --no-site-packages virtualenv
* `Distribute #163 <https://bitbucket.org/tarek/distribute/issue/163>`_: scan index links before external links, and don't use the md5 when
  comparing two distributions

------
0.6.12
------

* `Distribute #149 <https://bitbucket.org/tarek/distribute/issue/149>`_: Fixed various failures on 2.3/2.4

------
0.6.11
------

* Found another case of SandboxViolation - fixed
* `Distribute #15 <https://bitbucket.org/tarek/distribute/issue/15>`_ and `Distribute #48 <https://bitbucket.org/tarek/distribute/issue/48>`_: Introduced a socket timeout of 15 seconds on url openings
* Added indexsidebar.html into MANIFEST.in
* `Distribute #108 <https://bitbucket.org/tarek/distribute/issue/108>`_: Fixed TypeError with Python3.1
* `Distribute #121 <https://bitbucket.org/tarek/distribute/issue/121>`_: Fixed --help install command trying to actually install.
* `Distribute #112 <https://bitbucket.org/tarek/distribute/issue/112>`_: Added an os.makedirs so that Tarek's solution will work.
* `Distribute #133 <https://bitbucket.org/tarek/distribute/issue/133>`_: Added --no-find-links to easy_install
* Added easy_install --user
* `Distribute #100 <https://bitbucket.org/tarek/distribute/issue/100>`_: Fixed develop --user not taking '.' in PYTHONPATH into account
* `Distribute #134 <https://bitbucket.org/tarek/distribute/issue/134>`_: removed spurious UserWarnings. Patch by VanLindberg
* `Distribute #138 <https://bitbucket.org/tarek/distribute/issue/138>`_: cant_write_to_target error when setup_requires is used.
* `Distribute #147 <https://bitbucket.org/tarek/distribute/issue/147>`_: respect the sys.dont_write_bytecode flag

------
0.6.10
------

* Reverted change made for the DistributionNotFound exception because
  zc.buildout uses the exception message to get the name of the
  distribution.

-----
0.6.9
-----

* `Distribute #90 <https://bitbucket.org/tarek/distribute/issue/90>`_: unknown setuptools version can be added in the working set
* `Distribute #87 <https://bitbucket.org/tarek/distribute/issue/87>`_: setupt.py doesn't try to convert distribute_setup.py anymore
  Initial Patch by arfrever.
* `Distribute #89 <https://bitbucket.org/tarek/distribute/issue/89>`_: added a side bar with a download link to the doc.
* `Distribute #86 <https://bitbucket.org/tarek/distribute/issue/86>`_: fixed missing sentence in pkg_resources doc.
* Added a nicer error message when a DistributionNotFound is raised.
* `Distribute #80 <https://bitbucket.org/tarek/distribute/issue/80>`_: test_develop now works with Python 3.1
* `Distribute #93 <https://bitbucket.org/tarek/distribute/issue/93>`_: upload_docs now works if there is an empty sub-directory.
* `Distribute #70 <https://bitbucket.org/tarek/distribute/issue/70>`_: exec bit on non-exec files
* `Distribute #99 <https://bitbucket.org/tarek/distribute/issue/99>`_: now the standalone easy_install command doesn't uses a
  "setup.cfg" if any exists in the working directory. It will use it
  only if triggered by ``install_requires`` from a setup.py call
  (install, develop, etc).
* `Distribute #101 <https://bitbucket.org/tarek/distribute/issue/101>`_: Allowing ``os.devnull`` in Sandbox
* `Distribute #92 <https://bitbucket.org/tarek/distribute/issue/92>`_: Fixed the "no eggs" found error with MacPort
  (platform.mac_ver() fails)
* `Distribute #103 <https://bitbucket.org/tarek/distribute/issue/103>`_: test_get_script_header_jython_workaround not run
  anymore under py3 with C or POSIX local. Contributed by Arfrever.
* `Distribute #104 <https://bitbucket.org/tarek/distribute/issue/104>`_: remvoved the assertion when the installation fails,
  with a nicer message for the end user.
* `Distribute #100 <https://bitbucket.org/tarek/distribute/issue/100>`_: making sure there's no SandboxViolation when
  the setup script patches setuptools.

-----
0.6.8
-----

* Added "check_packages" in dist. (added in Setuptools 0.6c11)
* Fixed the DONT_PATCH_SETUPTOOLS state.

-----
0.6.7
-----

* `Distribute #58 <https://bitbucket.org/tarek/distribute/issue/58>`_: Added --user support to the develop command
* `Distribute #11 <https://bitbucket.org/tarek/distribute/issue/11>`_: Generated scripts now wrap their call to the script entry point
  in the standard "if name == 'main'"
* Added the 'DONT_PATCH_SETUPTOOLS' environment variable, so virtualenv
  can drive an installation that doesn't patch a global setuptools.
* Reviewed unladen-swallow specific change from
  http://code.google.com/p/unladen-swallow/source/detail?spec=svn875&r=719
  and determined that it no longer applies. Distribute should work fine with
  Unladen Swallow 2009Q3.
* `Distribute #21 <https://bitbucket.org/tarek/distribute/issue/21>`_: Allow PackageIndex.open_url to gracefully handle all cases of a
  httplib.HTTPException instead of just InvalidURL and BadStatusLine.
* Removed virtual-python.py from this distribution and updated documentation
  to point to the actively maintained virtualenv instead.
* `Distribute #64 <https://bitbucket.org/tarek/distribute/issue/64>`_: use_setuptools no longer rebuilds the distribute egg every
  time it is run
* use_setuptools now properly respects the requested version
* use_setuptools will no longer try to import a distribute egg for the
  wrong Python version
* `Distribute #74 <https://bitbucket.org/tarek/distribute/issue/74>`_: no_fake should be True by default.
* `Distribute #72 <https://bitbucket.org/tarek/distribute/issue/72>`_: avoid a bootstrapping issue with easy_install -U

-----
0.6.6
-----

* Unified the bootstrap file so it works on both py2.x and py3k without 2to3
  (patch by Holger Krekel)

-----
0.6.5
-----

* `Distribute #65 <https://bitbucket.org/tarek/distribute/issue/65>`_: cli.exe and gui.exe are now generated at build time,
  depending on the platform in use.

* `Distribute #67 <https://bitbucket.org/tarek/distribute/issue/67>`_: Fixed doc typo (PEP 381/382)

* Distribute no longer shadows setuptools if we require a 0.7-series
  setuptools.  And an error is raised when installing a 0.7 setuptools with
  distribute.

* When run from within buildout, no attempt is made to modify an existing
  setuptools egg, whether in a shared egg directory or a system setuptools.

* Fixed a hole in sandboxing allowing builtin file to write outside of
  the sandbox.

-----
0.6.4
-----

* Added the generation of `distribute_setup_3k.py` during the release.
  This closes `Distribute #52 <https://bitbucket.org/tarek/distribute/issue/52>`_.

* Added an upload_docs command to easily upload project documentation to
  PyPI's https://pythonhosted.org. This close issue `Distribute #56 <https://bitbucket.org/tarek/distribute/issue/56>`_.

* Fixed a bootstrap bug on the use_setuptools() API.

-----
0.6.3
-----

setuptools
==========

* Fixed a bunch of calls to file() that caused crashes on Python 3.

bootstrapping
=============

* Fixed a bug in sorting that caused bootstrap to fail on Python 3.

-----
0.6.2
-----

setuptools
==========

* Added Python 3 support; see docs/python3.txt.
  This closes `Old Setuptools #39 <http://bugs.python.org/setuptools/issue39>`_.

* Added option to run 2to3 automatically when installing on Python 3.
  This closes issue `Distribute #31 <https://bitbucket.org/tarek/distribute/issue/31>`_.

* Fixed invalid usage of requirement.parse, that broke develop -d.
  This closes `Old Setuptools #44 <http://bugs.python.org/setuptools/issue44>`_.

* Fixed script launcher for 64-bit Windows.
  This closes `Old Setuptools #2 <http://bugs.python.org/setuptools/issue2>`_.

* KeyError when compiling extensions.
  This closes `Old Setuptools #41 <http://bugs.python.org/setuptools/issue41>`_.

bootstrapping
=============

* Fixed bootstrap not working on Windows. This closes issue `Distribute #49 <https://bitbucket.org/tarek/distribute/issue/49>`_.

* Fixed 2.6 dependencies. This closes issue `Distribute #50 <https://bitbucket.org/tarek/distribute/issue/50>`_.

* Make sure setuptools is patched when running through easy_install
  This closes `Old Setuptools #40 <http://bugs.python.org/setuptools/issue40>`_.

-----
0.6.1
-----

setuptools
==========

* package_index.urlopen now catches BadStatusLine and malformed url errors.
  This closes `Distribute #16 <https://bitbucket.org/tarek/distribute/issue/16>`_ and `Distribute #18 <https://bitbucket.org/tarek/distribute/issue/18>`_.

* zip_ok is now False by default. This closes `Old Setuptools #33 <http://bugs.python.org/setuptools/issue33>`_.

* Fixed invalid URL error catching. `Old Setuptools #20 <http://bugs.python.org/setuptools/issue20>`_.

* Fixed invalid bootstraping with easy_install installation (`Distribute #40 <https://bitbucket.org/tarek/distribute/issue/40>`_).
  Thanks to Florian Schulze for the help.

* Removed buildout/bootstrap.py. A new repository will create a specific
  bootstrap.py script.


bootstrapping
=============

* The boostrap process leave setuptools alone if detected in the system
  and --root or --prefix is provided, but is not in the same location.
  This closes `Distribute #10 <https://bitbucket.org/tarek/distribute/issue/10>`_.

---
0.6
---

setuptools
==========

* Packages required at build time where not fully present at install time.
  This closes `Distribute #12 <https://bitbucket.org/tarek/distribute/issue/12>`_.

* Protected against failures in tarfile extraction. This closes `Distribute #10 <https://bitbucket.org/tarek/distribute/issue/10>`_.

* Made Jython api_tests.txt doctest compatible. This closes `Distribute #7 <https://bitbucket.org/tarek/distribute/issue/7>`_.

* sandbox.py replaced builtin type file with builtin function open. This
  closes `Distribute #6 <https://bitbucket.org/tarek/distribute/issue/6>`_.

* Immediately close all file handles. This closes `Distribute #3 <https://bitbucket.org/tarek/distribute/issue/3>`_.

* Added compatibility with Subversion 1.6. This references `Distribute #1 <https://bitbucket.org/tarek/distribute/issue/1>`_.

pkg_resources
=============

* Avoid a call to /usr/bin/sw_vers on OSX and use the official platform API
  instead. Based on a patch from ronaldoussoren. This closes issue `#5 <https://bitbucket.org/pypa/setuptools/issue/5>`_.

* Fixed a SandboxViolation for mkdir that could occur in certain cases.
  This closes `Distribute #13 <https://bitbucket.org/tarek/distribute/issue/13>`_.

* Allow to find_on_path on systems with tight permissions to fail gracefully.
  This closes `Distribute #9 <https://bitbucket.org/tarek/distribute/issue/9>`_.

* Corrected inconsistency between documentation and code of add_entry.
  This closes `Distribute #8 <https://bitbucket.org/tarek/distribute/issue/8>`_.

* Immediately close all file handles. This closes `Distribute #3 <https://bitbucket.org/tarek/distribute/issue/3>`_.

easy_install
============

* Immediately close all file handles. This closes `Distribute #3 <https://bitbucket.org/tarek/distribute/issue/3>`_.

-----
0.6c9
-----

 * Fixed a missing files problem when using Windows source distributions on
   non-Windows platforms, due to distutils not handling manifest file line
   endings correctly.

 * Updated Pyrex support to work with Pyrex 0.9.6 and higher.

 * Minor changes for Jython compatibility, including skipping tests that can't
   work on Jython.

 * Fixed not installing eggs in ``install_requires`` if they were also used for
   ``setup_requires`` or ``tests_require``.

 * Fixed not fetching eggs in ``install_requires`` when running tests.

 * Allow ``ez_setup.use_setuptools()`` to upgrade existing setuptools
   installations when called from a standalone ``setup.py``.

 * Added a warning if a namespace package is declared, but its parent package
   is not also declared as a namespace.

 * Support Subversion 1.5

 * Removed use of deprecated ``md5`` module if ``hashlib`` is available

 * Fixed ``bdist_wininst upload`` trying to upload the ``.exe`` twice

 * Fixed ``bdist_egg`` putting a ``native_libs.txt`` in the source package's
   ``.egg-info``, when it should only be in the built egg's ``EGG-INFO``.

 * Ensure that _full_name is set on all shared libs before extensions are
   checked for shared lib usage.  (Fixes a bug in the experimental shared
   library build support.)

 * Fix to allow unpacked eggs containing native libraries to fail more
   gracefully under Google App Engine (with an ``ImportError`` loading the
   C-based module, instead of getting a ``NameError``).

-----
0.6c7
-----

 * Fixed ``distutils.filelist.findall()`` crashing on broken symlinks, and
   ``egg_info`` command failing on new, uncommitted SVN directories.

 * Fix import problems with nested namespace packages installed via
   ``--root`` or ``--single-version-externally-managed``, due to the
   parent package not having the child package as an attribute.

-----
0.6c6
-----

 * Added ``--egg-path`` option to ``develop`` command, allowing you to force
   ``.egg-link`` files to use relative paths (allowing them to be shared across
   platforms on a networked drive).

 * Fix not building binary RPMs correctly.

 * Fix "eggsecutables" (such as setuptools' own egg) only being runnable with
   bash-compatible shells.

 * Fix ``#!`` parsing problems in Windows ``.exe`` script wrappers, when there
   was whitespace inside a quoted argument or at the end of the ``#!`` line
   (a regression introduced in 0.6c4).

 * Fix ``test`` command possibly failing if an older version of the project
   being tested was installed on ``sys.path`` ahead of the test source
   directory.

 * Fix ``find_packages()`` treating ``ez_setup`` and directories with ``.`` in
   their names as packages.

-----
0.6c5
-----

 * Fix uploaded ``bdist_rpm`` packages being described as ``bdist_egg``
   packages under Python versions less than 2.5.

 * Fix uploaded ``bdist_wininst`` packages being described as suitable for
   "any" version by Python 2.5, even if a ``--target-version`` was specified.

-----
0.6c4
-----

 * Overhauled Windows script wrapping to support ``bdist_wininst`` better.
   Scripts installed with ``bdist_wininst`` will always use ``#!python.exe`` or
   ``#!pythonw.exe`` as the executable name (even when built on non-Windows
   platforms!), and the wrappers will look for the executable in the script's
   parent directory (which should find the right version of Python).

 * Fix ``upload`` command not uploading files built by ``bdist_rpm`` or
   ``bdist_wininst`` under Python 2.3 and 2.4.

 * Add support for "eggsecutable" headers: a ``#!/bin/sh`` script that is
   prepended to an ``.egg`` file to allow it to be run as a script on Unix-ish
   platforms.  (This is mainly so that setuptools itself can have a single-file
   installer on Unix, without doing multiple downloads, dealing with firewalls,
   etc.)

 * Fix problem with empty revision numbers in Subversion 1.4 ``entries`` files

 * Use cross-platform relative paths in ``easy-install.pth`` when doing
   ``develop`` and the source directory is a subdirectory of the installation
   target directory.

 * Fix a problem installing eggs with a system packaging tool if the project
   contained an implicit namespace package; for example if the ``setup()``
   listed a namespace package ``foo.bar`` without explicitly listing ``foo``
   as a namespace package.

-----
0.6c3
-----

 * Fixed breakages caused by Subversion 1.4's new "working copy" format

-----
0.6c2
-----

 * The ``ez_setup`` module displays the conflicting version of setuptools (and
   its installation location) when a script requests a version that's not
   available.

 * Running ``setup.py develop`` on a setuptools-using project will now install
   setuptools if needed, instead of only downloading the egg.

-----
0.6c1
-----

 * Fixed ``AttributeError`` when trying to download a ``setup_requires``
   dependency when a distribution lacks a ``dependency_links`` setting.

 * Made ``zip-safe`` and ``not-zip-safe`` flag files contain a single byte, so
   as to play better with packaging tools that complain about zero-length
   files.

 * Made ``setup.py develop`` respect the ``--no-deps`` option, which it
   previously was ignoring.

 * Support ``extra_path`` option to ``setup()`` when ``install`` is run in
   backward-compatibility mode.

 * Source distributions now always include a ``setup.cfg`` file that explicitly
   sets ``egg_info`` options such that they produce an identical version number
   to the source distribution's version number.  (Previously, the default
   version number could be different due to the use of ``--tag-date``, or if
   the version was overridden on the command line that built the source
   distribution.)

-----
0.6b4
-----

 * Fix ``register`` not obeying name/version set by ``egg_info`` command, if
   ``egg_info`` wasn't explicitly run first on the same command line.

 * Added ``--no-date`` and ``--no-svn-revision`` options to ``egg_info``
   command, to allow suppressing tags configured in ``setup.cfg``.

 * Fixed redundant warnings about missing ``README`` file(s); it should now
   appear only if you are actually a source distribution.

-----
0.6b3
-----

 * Fix ``bdist_egg`` not including files in subdirectories of ``.egg-info``.

 * Allow ``.py`` files found by the ``include_package_data`` option to be
   automatically included.  Remove duplicate data file matches if both
   ``include_package_data`` and ``package_data`` are used to refer to the same
   files.

-----
0.6b1
-----

 * Strip ``module`` from the end of compiled extension modules when computing
   the name of a ``.py`` loader/wrapper.  (Python's import machinery ignores
   this suffix when searching for an extension module.)

------
0.6a11
------

 * Added ``test_loader`` keyword to support custom test loaders

 * Added ``setuptools.file_finders`` entry point group to allow implementing
   revision control plugins.

 * Added ``--identity`` option to ``upload`` command.

 * Added ``dependency_links`` to allow specifying URLs for ``--find-links``.

 * Enhanced test loader to scan packages as well as modules, and call
   ``additional_tests()`` if present to get non-unittest tests.

 * Support namespace packages in conjunction with system packagers, by omitting
   the installation of any ``__init__.py`` files for namespace packages, and
   adding a special ``.pth`` file to create a working package in
   ``sys.modules``.

 * Made ``--single-version-externally-managed`` automatic when ``--root`` is
   used, so that most system packagers won't require special support for
   setuptools.

 * Fixed ``setup_requires``, ``tests_require``, etc. not using ``setup.cfg`` or
   other configuration files for their option defaults when installing, and
   also made the install use ``--multi-version`` mode so that the project
   directory doesn't need to support .pth files.

 * ``MANIFEST.in`` is now forcibly closed when any errors occur while reading
   it.  Previously, the file could be left open and the actual error would be
   masked by problems trying to remove the open file on Windows systems.

------
0.6a10
------

 * Fixed the ``develop`` command ignoring ``--find-links``.

-----
0.6a9
-----

 * The ``sdist`` command no longer uses the traditional ``MANIFEST`` file to
   create source distributions.  ``MANIFEST.in`` is still read and processed,
   as are the standard defaults and pruning.  But the manifest is built inside
   the project's ``.egg-info`` directory as ``SOURCES.txt``, and it is rebuilt
   every time the ``egg_info`` command is run.

 * Added the ``include_package_data`` keyword to ``setup()``, allowing you to
   automatically include any package data listed in revision control or
   ``MANIFEST.in``

 * Added the ``exclude_package_data`` keyword to ``setup()``, allowing you to
   trim back files included via the ``package_data`` and
   ``include_package_data`` options.

 * Fixed ``--tag-svn-revision`` not working when run from a source
   distribution.

 * Added warning for namespace packages with missing ``declare_namespace()``

 * Added ``tests_require`` keyword to ``setup()``, so that e.g. packages
   requiring ``nose`` to run unit tests can make this dependency optional
   unless the ``test`` command is run.

 * Made all commands that use ``easy_install`` respect its configuration
   options, as this was causing some problems with ``setup.py install``.

 * Added an ``unpack_directory()`` driver to ``setuptools.archive_util``, so
   that you can process a directory tree through a processing filter as if it
   were a zipfile or tarfile.

 * Added an internal ``install_egg_info`` command to use as part of old-style
   ``install`` operations, that installs an ``.egg-info`` directory with the
   package.

 * Added a ``--single-version-externally-managed`` option to the ``install``
   command so that you can more easily wrap a "flat" egg in a system package.

 * Enhanced ``bdist_rpm`` so that it installs single-version eggs that
   don't rely on a ``.pth`` file.  The ``--no-egg`` option has been removed,
   since all RPMs are now built in a more backwards-compatible format.

 * Support full roundtrip translation of eggs to and from ``bdist_wininst``
   format.  Running ``bdist_wininst`` on a setuptools-based package wraps the
   egg in an .exe that will safely install it as an egg (i.e., with metadata
   and entry-point wrapper scripts), and ``easy_install`` can turn the .exe
   back into an ``.egg`` file or directory and install it as such.


-----
0.6a8
-----

 * Fixed some problems building extensions when Pyrex was installed, especially
   with Python 2.4 and/or packages using SWIG.

 * Made ``develop`` command accept all the same options as ``easy_install``,
   and use the ``easy_install`` command's configuration settings as defaults.

 * Made ``egg_info --tag-svn-revision`` fall back to extracting the revision
   number from ``PKG-INFO`` in case it is being run on a source distribution of
   a snapshot taken from a Subversion-based project.

 * Automatically detect ``.dll``, ``.so`` and ``.dylib`` files that are being
   installed as data, adding them to ``native_libs.txt`` automatically.

 * Fixed some problems with fresh checkouts of projects that don't include
   ``.egg-info/PKG-INFO`` under revision control and put the project's source
   code directly in the project directory.  If such a package had any
   requirements that get processed before the ``egg_info`` command can be run,
   the setup scripts would fail with a "Missing 'Version:' header and/or
   PKG-INFO file" error, because the egg runtime interpreted the unbuilt
   metadata in a directory on ``sys.path`` (i.e. the current directory) as
   being a corrupted egg.  Setuptools now monkeypatches the distribution
   metadata cache to pretend that the egg has valid version information, until
   it has a chance to make it actually be so (via the ``egg_info`` command).

-----
0.6a5
-----

 * Fixed missing gui/cli .exe files in distribution.  Fixed bugs in tests.

-----
0.6a3
-----

 * Added ``gui_scripts`` entry point group to allow installing GUI scripts
   on Windows and other platforms.  (The special handling is only for Windows;
   other platforms are treated the same as for ``console_scripts``.)

-----
0.6a2
-----

 * Added ``console_scripts`` entry point group to allow installing scripts
   without the need to create separate script files.  On Windows, console
   scripts get an ``.exe`` wrapper so you can just type their name.  On other
   platforms, the scripts are written without a file extension.

-----
0.6a1
-----

 * Added support for building "old-style" RPMs that don't install an egg for
   the target package, using a ``--no-egg`` option.

 * The ``build_ext`` command now works better when using the ``--inplace``
   option and multiple Python versions.  It now makes sure that all extensions
   match the current Python version, even if newer copies were built for a
   different Python version.

 * The ``upload`` command no longer attaches an extra ``.zip`` when uploading
   eggs, as PyPI now supports egg uploads without trickery.

 * The ``ez_setup`` script/module now displays a warning before downloading
   the setuptools egg, and attempts to check the downloaded egg against an
   internal MD5 checksum table.

 * Fixed the ``--tag-svn-revision`` option of ``egg_info`` not finding the
   latest revision number; it was using the revision number of the directory
   containing ``setup.py``, not the highest revision number in the project.

 * Added ``eager_resources`` setup argument

 * The ``sdist`` command now recognizes Subversion "deleted file" entries and
   does not include them in source distributions.

 * ``setuptools`` now embeds itself more thoroughly into the distutils, so that
   other distutils extensions (e.g. py2exe, py2app) will subclass setuptools'
   versions of things, rather than the native distutils ones.

 * Added ``entry_points`` and ``setup_requires`` arguments to ``setup()``;
   ``setup_requires`` allows you to automatically find and download packages
   that are needed in order to *build* your project (as opposed to running it).

 * ``setuptools`` now finds its commands, ``setup()`` argument validators, and
   metadata writers using entry points, so that they can be extended by
   third-party packages.  See `Creating distutils Extensions
   <http://pythonhosted.org/setuptools/setuptools.html#creating-distutils-extensions>`_
   for more details.

 * The vestigial ``depends`` command has been removed.  It was never finished
   or documented, and never would have worked without EasyInstall - which it
   pre-dated and was never compatible with.

------
0.5a12
------

 * The zip-safety scanner now checks for modules that might be used with
   ``python -m``, and marks them as unsafe for zipping, since Python 2.4 can't
   handle ``-m`` on zipped modules.

------
0.5a11
------

 * Fix breakage of the "develop" command that was caused by the addition of
   ``--always-unzip`` to the ``easy_install`` command.

-----
0.5a9
-----

 * Include ``svn:externals`` directories in source distributions as well as
   normal subversion-controlled files and directories.

 * Added ``exclude=patternlist`` option to ``setuptools.find_packages()``

 * Changed --tag-svn-revision to include an "r" in front of the revision number
   for better readability.

 * Added ability to build eggs without including source files (except for any
   scripts, of course), using the ``--exclude-source-files`` option to
   ``bdist_egg``.

 * ``setup.py install`` now automatically detects when an "unmanaged" package
   or module is going to be on ``sys.path`` ahead of a package being installed,
   thereby preventing the newer version from being imported.  If this occurs,
   a warning message is output to ``sys.stderr``, but installation proceeds
   anyway.  The warning message informs the user what files or directories
   need deleting, and advises them they can also use EasyInstall (with the
   ``--delete-conflicting`` option) to do it automatically.

 * The ``egg_info`` command now adds a ``top_level.txt`` file to the metadata
   directory that lists all top-level modules and packages in the distribution.
   This is used by the ``easy_install`` command to find possibly-conflicting
   "unmanaged" packages when installing the distribution.

 * Added ``zip_safe`` and ``namespace_packages`` arguments to ``setup()``.
   Added package analysis to determine zip-safety if the ``zip_safe`` flag
   is not given, and advise the author regarding what code might need changing.

 * Fixed the swapped ``-d`` and ``-b`` options of ``bdist_egg``.

-----
0.5a8
-----

 * The "egg_info" command now always sets the distribution metadata to "safe"
   forms of the distribution name and version, so that distribution files will
   be generated with parseable names (i.e., ones that don't include '-' in the
   name or version).  Also, this means that if you use the various ``--tag``
   options of "egg_info", any distributions generated will use the tags in the
   version, not just egg distributions.

 * Added support for defining command aliases in distutils configuration files,
   under the "[aliases]" section.  To prevent recursion and to allow aliases to
   call the command of the same name, a given alias can be expanded only once
   per command-line invocation.  You can define new aliases with the "alias"
   command, either for the local, global, or per-user configuration.

 * Added "rotate" command to delete old distribution files, given a set of
   patterns to match and the number of files to keep.  (Keeps the most
   recently-modified distribution files matching each pattern.)

 * Added "saveopts" command that saves all command-line options for the current
   invocation to the local, global, or per-user configuration file.  Useful for
   setting defaults without having to hand-edit a configuration file.

 * Added a "setopt" command that sets a single option in a specified distutils
   configuration file.

-----
0.5a7
-----

 * Added "upload" support for egg and source distributions, including a bug
   fix for "upload" and a temporary workaround for lack of .egg support in
   PyPI.

-----
0.5a6
-----

 * Beefed up the "sdist" command so that if you don't have a MANIFEST.in, it
   will include all files under revision control (CVS or Subversion) in the
   current directory, and it will regenerate the list every time you create a
   source distribution, not just when you tell it to.  This should make the
   default "do what you mean" more often than the distutils' default behavior
   did, while still retaining the old behavior in the presence of MANIFEST.in.

 * Fixed the "develop" command always updating .pth files, even if you
   specified ``-n`` or ``--dry-run``.

 * Slightly changed the format of the generated version when you use
   ``--tag-build`` on the "egg_info" command, so that you can make tagged
   revisions compare *lower* than the version specified in setup.py (e.g. by
   using ``--tag-build=dev``).

-----
0.5a5
-----

 * Added ``develop`` command to ``setuptools``-based packages.  This command
   installs an ``.egg-link`` pointing to the package's source directory, and
   script wrappers that ``execfile()`` the source versions of the package's
   scripts.  This lets you put your development checkout(s) on sys.path without
   having to actually install them.  (To uninstall the link, use
   use ``setup.py develop --uninstall``.)

 * Added ``egg_info`` command to ``setuptools``-based packages.  This command
   just creates or updates the "projectname.egg-info" directory, without
   building an egg.  (It's used by the ``bdist_egg``, ``test``, and ``develop``
   commands.)

 * Enhanced the ``test`` command so that it doesn't install the package, but
   instead builds any C extensions in-place, updates the ``.egg-info``
   metadata, adds the source directory to ``sys.path``, and runs the tests
   directly on the source.  This avoids an "unmanaged" installation of the
   package to ``site-packages`` or elsewhere.

 * Made ``easy_install`` a standard ``setuptools`` command, moving it from
   the ``easy_install`` module to ``setuptools.command.easy_install``.  Note
   that if you were importing or extending it, you must now change your imports
   accordingly.  ``easy_install.py`` is still installed as a script, but not as
   a module.

-----
0.5a4
-----

 * Setup scripts using setuptools can now list their dependencies directly in
   the setup.py file, without having to manually create a ``depends.txt`` file.
   The ``install_requires`` and ``extras_require`` arguments to ``setup()``
   are used to create a dependencies file automatically.  If you are manually
   creating ``depends.txt`` right now, please switch to using these setup
   arguments as soon as practical, because ``depends.txt`` support will be
   removed in the 0.6 release cycle.  For documentation on the new arguments,
   see the ``setuptools.dist.Distribution`` class.

 * Setup scripts using setuptools now always install using ``easy_install``
   internally, for ease of uninstallation and upgrading.

-----
0.5a1
-----

 * Added support for "self-installation" bootstrapping.  Packages can now
   include ``ez_setup.py`` in their source distribution, and add the following
   to their ``setup.py``, in order to automatically bootstrap installation of
   setuptools as part of their setup process::

    from ez_setup import use_setuptools
    use_setuptools()

    from setuptools import setup
    # etc...

-----
0.4a2
-----

 * Added ``ez_setup.py`` installer/bootstrap script to make initial setuptools
   installation easier, and to allow distributions using setuptools to avoid
   having to include setuptools in their source distribution.

 * All downloads are now managed by the ``PackageIndex`` class (which is now
   subclassable and replaceable), so that embedders can more easily override
   download logic, give download progress reports, etc.  The class has also
   been moved to the new ``setuptools.package_index`` module.

 * The ``Installer`` class no longer handles downloading, manages a temporary
   directory, or tracks the ``zip_ok`` option.  Downloading is now handled
   by ``PackageIndex``, and ``Installer`` has become an ``easy_install``
   command class based on ``setuptools.Command``.

 * There is a new ``setuptools.sandbox.run_setup()`` API to invoke a setup
   script in a directory sandbox, and a new ``setuptools.archive_util`` module
   with an ``unpack_archive()`` API.  These were split out of EasyInstall to
   allow reuse by other tools and applications.

 * ``setuptools.Command`` now supports reinitializing commands using keyword
   arguments to set/reset options.  Also, ``Command`` subclasses can now set
   their ``command_consumes_arguments`` attribute to ``True`` in order to
   receive an ``args`` option containing the rest of the command line.

-----
0.3a2
-----

 * Added new options to ``bdist_egg`` to allow tagging the egg's version number
   with a subversion revision number, the current date, or an explicit tag
   value.  Run ``setup.py bdist_egg --help`` to get more information.

 * Misc. bug fixes

-----
0.3a1
-----

 * Initial release.


