# -*- coding: utf-8 -*-
"""upload_docs

Implements a Distutils 'upload_docs' subcommand (upload documentation to
PyPI's pythonhosted.org).
"""

from base64 import standard_b64encode
from distutils import log
from distutils.errors import DistutilsOptionError
from distutils.command.upload import upload
import os
import socket
import zipfile
import tempfile
import sys
import shutil

from setuptools.compat import httplib, urlparse, unicode, iteritems, PY3
from pkg_resources import iter_entry_points


errors = 'surrogateescape' if PY3 else 'strict'


# This is not just a replacement for byte literals
# but works as a general purpose encoder
def b(s, encoding='utf-8'):
    if isinstance(s, unicode):
        return s.encode(encoding, errors)
    return s


class upload_docs(upload):
    description = 'Upload documentation to PyPI'

    user_options = [
        ('repository=', 'r',
         "url of repository [default: %s]" % upload.DEFAULT_REPOSITORY),
        ('show-response', None,
         'display full response text from server'),
        ('upload-dir=', None, 'directory to upload'),
    ]
    boolean_options = upload.boolean_options

    def has_sphinx(self):
        if self.upload_dir is None:
            for ep in iter_entry_points('distutils.commands', 'build_sphinx'):
                return True

    sub_commands = [('build_sphinx', has_sphinx)]

    def initialize_options(self):
        upload.initialize_options(self)
        self.upload_dir = None
        self.target_dir = None

    def finalize_options(self):
        upload.finalize_options(self)
        if self.upload_dir is None:
            if self.has_sphinx():
                build_sphinx = self.get_finalized_command('build_sphinx')
                self.target_dir = build_sphinx.builder_target_dir
            else:
                build = self.get_finalized_command('build')
                self.target_dir = os.path.join(build.build_base, 'docs')
        else:
            self.ensure_dirname('upload_dir')
            self.target_dir = self.upload_dir
        self.announce('Using upload directory %s' % self.target_dir)

    def create_zipfile(self, filename):
        zip_file = zipfile.ZipFile(filename, "w")
        try:
            self.mkpath(self.target_dir)  # just in case
            for root, dirs, files in os.walk(self.target_dir):
                if root == self.target_dir and not files:
                    raise DistutilsOptionError(
                        "no files found in upload directory '%s'"
                        % self.target_dir)
                for name in files:
                    full = os.path.join(root, name)
                    relative = root[len(self.target_dir):].lstrip(os.path.sep)
                    dest = os.path.join(relative, name)
                    zip_file.write(full, dest)
        finally:
            zip_file.close()

    def run(self):
        # Run sub commands
        for cmd_name in self.get_sub_commands():
            self.run_command(cmd_name)

        tmp_dir = tempfile.mkdtemp()
        name = self.distribution.metadata.get_name()
        zip_file = os.path.join(tmp_dir, "%s.zip" % name)
        try:
            self.create_zipfile(zip_file)
            self.upload_file(zip_file)
        finally:
            shutil.rmtree(tmp_dir)

    def upload_file(self, filename):
        f = open(filename, 'rb')
        content = f.read()
        f.close()
        meta = self.distribution.metadata
        data = {
            ':action': 'doc_upload',
            'name': meta.get_name(),
            'content': (os.path.basename(filename), content),
        }
        # set up the authentication
        credentials = b(self.username + ':' + self.password)
        credentials = standard_b64encode(credentials)
        if PY3:
            credentials = credentials.decode('ascii')
        auth = "Basic " + credentials

        # Build up the MIME payload for the POST data
        boundary = '--------------GHSKFJDLGDS7543FJKLFHRE75642756743254'
        sep_boundary = b('\n--') + b(boundary)
        end_boundary = sep_boundary + b('--')
        body = []
        for key, values in iteritems(data):
            title = '\nContent-Disposition: form-data; name="%s"' % key
            # handle multiple entries for the same name
            if not isinstance(values, list):
                values = [values]
            for value in values:
                if type(value) is tuple:
                    title += '; filename="%s"' % value[0]
                    value = value[1]
                else:
                    value = b(value)
                body.append(sep_boundary)
                body.append(b(title))
                body.append(b("\n\n"))
                body.append(value)
                if value and value[-1:] == b('\r'):
                    body.append(b('\n'))  # write an extra newline (lurve Macs)
        body.append(end_boundary)
        body.append(b("\n"))
        body = b('').join(body)

        self.announce("Submitting documentation to %s" % (self.repository),
                      log.INFO)

        # build the Request
        # We can't use urllib2 since we need to send the Basic
        # auth right with the first request
        schema, netloc, url, params, query, fragments = \
            urlparse(self.repository)
        assert not params and not query and not fragments
        if schema == 'http':
            conn = httplib.HTTPConnection(netloc)
        elif schema == 'https':
            conn = httplib.HTTPSConnection(netloc)
        else:
            raise AssertionError("unsupported schema " + schema)

        data = ''
        try:
            conn.connect()
            conn.putrequest("POST", url)
            content_type = 'multipart/form-data; boundary=%s' % boundary
            conn.putheader('Content-type', content_type)
            conn.putheader('Content-length', str(len(body)))
            conn.putheader('Authorization', auth)
            conn.endheaders()
            conn.send(body)
        except socket.error:
            e = sys.exc_info()[1]
            self.announce(str(e), log.ERROR)
            return

        r = conn.getresponse()
        if r.status == 200:
            self.announce('Server response (%s): %s' % (r.status, r.reason),
                          log.INFO)
        elif r.status == 301:
            location = r.getheader('Location')
            if location is None:
                location = 'https://pythonhosted.org/%s/' % meta.get_name()
            self.announce('Upload successful. Visit %s' % location,
                          log.INFO)
        else:
            self.announce('Upload failed (%s): %s' % (r.status, r.reason),
                          log.ERROR)
        if self.show_response:
            print('-' * 75, r.read(), '-' * 75)
