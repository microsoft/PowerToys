# -*- coding: utf-8 -*-
#
# Copyright (C) 2013 Vinay Sajip.
# Licensed to the Python Software Foundation under a contributor agreement.
# See LICENSE.txt and CONTRIBUTORS.txt.
#
import hashlib
import itertools
import logging
import os
import shutil
import socket
from string import ascii_lowercase
import subprocess
import tempfile
from threading import Thread

from distlib import DistlibException
from distlib.compat import (HTTPBasicAuthHandler, Request, HTTPPasswordMgr,
                            urlparse, build_opener)
from distlib.util import cached_property, zip_dir

logger = logging.getLogger(__name__)

DEFAULT_MIRROR_HOST = 'last.pypi.python.org'
DEFAULT_INDEX = 'http://pypi.python.org/pypi'
DEFAULT_REALM = 'pypi'

class PackageIndex(object):
    """
    This class represents a package index compatible with PyPI, the Python
    Package Index.
    """

    boundary = b'----------ThIs_Is_tHe_distlib_index_bouNdaRY_$'

    def __init__(self, url=None, mirror_host=None):
        """
        Initialise an instance.

        :param url: The URL of the index. If not specified, the URL for PyPI is
                    used.
        :param mirror_host: If not specified, ``last.pypi.python.org`` is used.
                            This is expected to have a canonial name which
                            allows all mirror hostnames to be divined (e.g. if
                            the canonical hostname for ``last.pypi.python.org``
                            is ``g.pypi.python.org``, then the mirrors that are
                            available would be assumed to be
                            ``a.pypi.python.org``, ``b.pypi.python.org``, ...
                            up to and including ``g.pypi.python.org``.
        """
        self.url = url or DEFAULT_INDEX
        self.mirror_host = mirror_host or DEFAULT_MIRROR_HOST
        self.read_configuration()
        scheme, netloc, path, params, query, frag = urlparse(self.url)
        if params or query or frag or scheme not in ('http', 'https'):
            raise DistlibException('invalid repository: %s' % self.url)
        self.password_handler = None
        self.ssl_verifier = None
        self.gpg = None
        self.gpg_home = None
        with open(os.devnull, 'w') as sink:
            for s in ('gpg2', 'gpg'):
                try:
                    rc = subprocess.check_call([s, '--version'], stdout=sink,
                                               stderr=sink)
                    if rc == 0:
                        self.gpg = s
                        break
                except OSError:
                    pass

    def _get_pypirc_command(self):
        """
        Get the distutils command for interacting with PyPI configurations.
        :return: the command.
        """
        from distutils.core import Distribution
        from distutils.config import PyPIRCCommand
        d = Distribution()
        return PyPIRCCommand(d)

    def read_configuration(self):
        """
        Read the PyPI access configuration as supported by distutils, getting
        PyPI to do the acutal work. This populates ``username``, ``password``,
        ``realm`` and ``url`` attributes from the configuration.
        """
        # get distutils to do the work
        c = self._get_pypirc_command()
        c.repository = self.url
        cfg = c._read_pypirc()
        self.username = cfg.get('username')
        self.password = cfg.get('password')
        self.realm = cfg.get('realm', 'pypi')
        self.url = cfg.get('repository', self.url)

    def save_configuration(self):
        """
        Save the PyPI access configuration. You must have set ``username`` and
        ``password`` attributes before calling this method.

        Again, distutils is used to do the actual work.
        """
        self.check_credentials()
        # get distutils to do the work
        c = self._get_pypirc_command()
        c._store_pypirc(self.username, self.password)

    def check_credentials(self):
        """
        Check that ``username`` and ``password`` have been set, and raise an
        exception if not.
        """
        if self.username is None or self.password is None:
            raise DistlibException('username and password must be set')
        pm = HTTPPasswordMgr()
        _, netloc, _, _, _, _ = urlparse(self.url)
        pm.add_password(self.realm, netloc, self.username, self.password)
        self.password_handler = HTTPBasicAuthHandler(pm)

    def register(self, metadata):
        """
        Register a distribution on PyPI, using the provided metadata.

        :param metadata: A :class:`Metadata` instance defining at least a name
                         and version number for the distribution to be
                         registered.
        :return: The HTTP response received from PyPI upon submission of the
                request.
        """
        self.check_credentials()
        missing, warnings = metadata.check(True)    # strict check
        logger.debug('result of check: missing: %s, warnings: %s',
                     missing, warnings)
        d = metadata.todict(True)
        d[':action'] = 'verify'
        request = self.encode_request(d.items(), [])
        response = self.send_request(request)
        d[':action'] = 'submit'
        request = self.encode_request(d.items(), [])
        return self.send_request(request)

    def _reader(self, name, stream, outbuf):
        """
        Thread runner for reading lines of from a subprocess into a buffer.

        :param name: The logical name of the stream (used for logging only).
        :param stream: The stream to read from. This will typically a pipe
                       connected to the output stream of a subprocess.
        :param outbuf: The list to append the read lines to.
        """
        while True:
            s = stream.readline()
            if not s:
                break
            s = s.decode('utf-8').rstrip()
            outbuf.append(s)
            logger.debug('%s: %s' % (name, s))
        stream.close()

    def get_sign_command(self, filename, signer, sign_password):
        """
        Return a suitable command for signing a file.

        :param filename: The pathname to the file to be signed.
        :param signer: The identifier of the signer of the file.
        :param sign_password: The passphrase for the signer's
                              private key used for signing.
        :return: The signing command as a list suitable to be
                 passed to :class:`subprocess.Popen`.
        """
        cmd = [self.gpg, '--status-fd', '2', '--no-tty']
        if self.gpg_home:
            cmd.extend(['--homedir', self.gpg_home])
        if sign_password is not None:
            cmd.extend(['--batch', '--passphrase-fd', '0'])
        td = tempfile.mkdtemp()
        sf = os.path.join(td, os.path.basename(filename) + '.asc')
        cmd.extend(['--detach-sign', '--armor', '--local-user',
                    signer, '--output', sf, filename])
        logger.debug('invoking: %s', ' '.join(cmd))
        return cmd, sf

    def run_command(self, cmd, input_data=None):
        """
        Run a command in a child process , passing it any input data specified.

        :param cmd: The command to run.
        :param input_data: If specified, this must be a byte string containing
                           data to be sent to the child process.
        :return: A tuple consisting of the subprocess' exit code, a list of
                 lines read from the subprocess' ``stdout``, and a list of
                 lines read from the subprocess' ``stderr``.
        """
        kwargs = {
            'stdout': subprocess.PIPE,
            'stderr': subprocess.PIPE,
        }
        if input_data is not None:
            kwargs['stdin'] = subprocess.PIPE
        stdout = []
        stderr = []
        p = subprocess.Popen(cmd, **kwargs)
        # We don't use communicate() here because we may need to
        # get clever with interacting with the command
        t1 = Thread(target=self._reader, args=('stdout', p.stdout, stdout))
        t1.start()
        t2 = Thread(target=self._reader, args=('stderr', p.stderr, stderr))
        t2.start()
        if input_data is not None:
            p.stdin.write(input_data)
            p.stdin.close()

        p.wait()
        t1.join()
        t2.join()
        return p.returncode, stdout, stderr

    def sign_file(self, filename, signer, sign_password):
        """
        Sign a file.

        :param filename: The pathname to the file to be signed.
        :param signer: The identifier of the signer of the file.
        :param sign_password: The passphrase for the signer's
                              private key used for signing.
        :return: The absolute pathname of the file where the signature is
                 stored.
        """
        cmd, sig_file = self.get_sign_command(filename, signer, sign_password)
        rc, stdout, stderr = self.run_command(cmd,
                                              sign_password.encode('utf-8'))
        if rc != 0:
            raise DistlibException('sign command failed with error '
                                   'code %s' % rc)
        return sig_file

    def upload_file(self, metadata, filename, signer=None, sign_password=None,
                    filetype='sdist', pyversion='source'):
        """
        Upload a release file to the index.

        :param metadata: A :class:`Metadata` instance defining at least a name
                         and version number for the file to be uploaded.
        :param filename: The pathname of the file to be uploaded.
        :param signer: The identifier of the signer of the file.
        :param sign_password: The passphrase for the signer's
                              private key used for signing.
        :param filetype: The type of the file being uploaded. This is the
                        distutils command which produced that file, e.g.
                        ``sdist`` or ``bdist_wheel``.
        :param pyversion: The version of Python which the release relates
                          to. For code compatible with any Python, this would
                          be ``source``, otherwise it would be e.g. ``3.2``.
        :return: The HTTP response received from PyPI upon submission of the
                request.
        """
        self.check_credentials()
        if not os.path.exists(filename):
            raise DistlibException('not found: %s' % filename)
        missing, warnings = metadata.check(True)    # strict check
        logger.debug('result of check: missing: %s, warnings: %s',
                     missing, warnings)
        d = metadata.todict(True)
        sig_file = None
        if signer:
            if not self.gpg:
                logger.warning('no signing program available - not signed')
            else:
                sig_file = self.sign_file(filename, signer, sign_password)
        with open(filename, 'rb') as f:
            file_data = f.read()
        digest = hashlib.md5(file_data).hexdigest()
        d.update({
            ':action': 'file_upload',
            'protcol_version': '1',
            'filetype': filetype,
            'pyversion': pyversion,
            'md5_digest': digest,
        })
        files = [('content', os.path.basename(filename), file_data)]
        if sig_file:
            with open(sig_file, 'rb') as f:
                sig_data = f.read()
            files.append(('gpg_signature', os.path.basename(sig_file),
                         sig_data))
            shutil.rmtree(os.path.dirname(sig_file))
        logger.debug('files: %s', files)
        request = self.encode_request(d.items(), files)
        return self.send_request(request)

    def upload_documentation(self, metadata, doc_dir):
        """
        Upload documentation to the index.

        :param metadata: A :class:`Metadata` instance defining at least a name
                         and version number for the documentation to be
                         uploaded.
        :param doc_dir: The pathname of the directory which contains the
                        documentation. This should be the directory that
                        contains the ``index.html`` for the documentation.
        :return: The HTTP response received from PyPI upon submission of the
                request.
        """
        self.check_credentials()
        if not os.path.isdir(doc_dir):
            raise DistlibException('not a directory: %r' % doc_dir)
        fn = os.path.join(doc_dir, 'index.html')
        if not os.path.exists(fn):
            raise DistlibException('not found: %r' % fn)
        missing, warnings = metadata.check(True)    # strict check
        logger.debug('result of check: missing: %s, warnings: %s',
                     missing, warnings)
        name, version = metadata.name, metadata.version
        zip_data = zip_dir(doc_dir).getvalue()
        fields = [(':action', 'doc_upload'),
                  ('name', name), ('version', version)]
        files = [('content', name, zip_data)]
        request = self.encode_request(fields, files)
        return self.send_request(request)

    def get_verify_command(self, signature_filename, data_filename):
        """
        Return a suitable command for verifying a file.

        :param signature_filename: The pathname to the file containing the
                                   signature.
        :param data_filename: The pathname to the file containing the
                              signed data.
        :return: The verifying command as a list suitable to be
                 passed to :class:`subprocess.Popen`.
        """
        cmd = [self.gpg, '--status-fd', '2', '--no-tty']
        if self.gpg_home:
            cmd.extend(['--homedir', self.gpg_home])
        cmd.extend(['--verify', signature_filename, data_filename])
        logger.debug('invoking: %s', ' '.join(cmd))
        return cmd

    def verify_signature(self, signature_filename, data_filename):
        """
        Verify a signature for a file.

        :param signature_filename: The pathname to the file containing the
                                   signature.
        :param data_filename: The pathname to the file containing the
                              signed data.
        :return: True if the signature was verified, else False.
        """
        if not self.gpg:
            raise DistlibException('verification unavailable because gpg '
                                   'unavailable')
        cmd = self.get_verify_command(signature_filename, data_filename)
        rc, stdout, stderr = self.run_command(cmd)
        if rc not in (0, 1):
            raise DistlibException('verify command failed with error '
                             'code %s' % rc)
        return rc == 0

    def download_file(self, url, destfile, digest=None, reporthook=None):
        """
        This is a convenience method for downloading a file from an URL.
        Normally, this will be a file from the index, though currently
        no check is made for this (i.e. a file can be downloaded from
        anywhere).

        The method is just like the :func:`urlretrieve` function in the
        standard library, except that it allows digest computation to be
        done during download and checking that the downloaded data
        matched any expected value.

        :param url: The URL of the file to be downloaded (assumed to be
                    available via an HTTP GET request).
        :param destfile: The pathname where the downloaded file is to be
                         saved.
        :param digest: If specified, this must be a (hasher, value)
                       tuple, where hasher is the algorithm used (e.g.
                       ``'md5'``) and ``value`` is the expected value.
        :param reporthook: The same as for :func:`urlretrieve` in the
                           standard library.
        """
        if digest is None:
            digester = None
        else:
            if isinstance(digest, (list, tuple)):
                hasher, digest = digest
            else:
                hasher = 'md5'
            digester = getattr(hashlib, hasher)()
        # The following code is equivalent to urlretrieve.
        # We need to do it this way so that we can compute the
        # digest of the file as we go.
        with open(destfile, 'wb') as dfp:
            # addinfourl is not a context manager on 2.x
            # so we have to use try/finally
            sfp = self.send_request(Request(url))
            try:
                headers = sfp.info()
                blocksize = 8192
                size = -1
                read = 0
                blocknum = 0
                if "content-length" in headers:
                    size = int(headers["Content-Length"])
                if reporthook:
                    reporthook(blocknum, blocksize, size)
                while True:
                    block = sfp.read(blocksize)
                    if not block:
                        break
                    read += len(block)
                    dfp.write(block)
                    if digester:
                        digester.update(block)
                    blocknum += 1
                    if reporthook:
                        reporthook(blocknum, blocksize, size)
            finally:
                sfp.close()

        # check that we got the whole file, if we can
        if size >= 0 and read < size:
            raise DistlibException(
                'retrieval incomplete: got only %d out of %d bytes'
                % (read, size))
        # if we have a digest, it must match.
        if digester:
            actual = digester.hexdigest()
            if digest != actual:
                raise DistlibException('MD5 digest mismatch for %s: expected '
                                       '%s, got %s' % (destfile, digest,
                                                       actual))

    def send_request(self, req):
        """
        Send a standard library :class:`Request` to PyPI and return its
        response.

        :param req: The request to send.
        :return: The HTTP response from PyPI (a standard library HTTPResponse).
        """
        handlers = []
        if self.password_handler:
            handlers.append(self.password_handler)
        if self.ssl_verifier:
            handlers.append(self.ssl_verifier)
        opener = build_opener(*handlers)
        return opener.open(req)

    def encode_request(self, fields, files):
        """
        Encode fields and files for posting to an HTTP server.

        :param fields: The fields to send as a list of (fieldname, value)
                       tuples.
        :param files: The files to send as a list of (fieldname, filename,
                      file_bytes) tuple.
        """
        # Adapted from packaging, which in turn was adapted from
        # http://code.activestate.com/recipes/146306

        parts = []
        boundary = self.boundary
        for k, values in fields:
            if not isinstance(values, (list, tuple)):
                values = [values]

            for v in values:
                parts.extend((
                    b'--' + boundary,
                    ('Content-Disposition: form-data; name="%s"' %
                     k).encode('utf-8'),
                    b'',
                    v.encode('utf-8')))
        for key, filename, value in files:
            parts.extend((
                b'--' + boundary,
                ('Content-Disposition: form-data; name="%s"; filename="%s"' %
                 (key, filename)).encode('utf-8'),
                b'',
                value))

        parts.extend((b'--' + boundary + b'--', b''))

        body = b'\r\n'.join(parts)
        ct = b'multipart/form-data; boundary=' + boundary
        headers = {
            'Content-type': ct,
            'Content-length': str(len(body))
        }
        return Request(self.url, body, headers)

    @cached_property
    def mirrors(self):
        """
        Return the list of hostnames which are mirrors for this index.
        :return: A (possibly empty) list of hostnames of mirrors.
        """
        result = []
        try:
            host = socket.gethostbyname_ex(self.mirror_host)[0]
        except socket.gaierror: # pragma: no cover
            host = None
        if host:
            last, rest = host.split('.', 1)
            n = len(last)
            host_list = (''.join(w) for w in itertools.chain.from_iterable(
                        itertools.product(ascii_lowercase, repeat=i)
                        for i in range(1, n + 1)))
            for s in host_list:
                result.append('.'.join((s, rest)))
                if s == last:
                    break
        return result
