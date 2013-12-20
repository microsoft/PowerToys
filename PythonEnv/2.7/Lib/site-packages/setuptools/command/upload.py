"""distutils.command.upload

Implements the Distutils 'upload' subcommand (upload package to PyPI)."""

from distutils.errors import *
from distutils.core import Command
from distutils.spawn import spawn
from distutils import log
try:
    from hashlib import md5
except ImportError:
    from md5 import md5
import os
import sys
import socket
import platform
import base64

from setuptools.compat import urlparse, StringIO, httplib, ConfigParser

class upload(Command):

    description = "upload binary package to PyPI"

    DEFAULT_REPOSITORY = 'https://pypi.python.org/pypi'

    user_options = [
        ('repository=', 'r',
         "url of repository [default: %s]" % DEFAULT_REPOSITORY),
        ('show-response', None,
         'display full response text from server'),
        ('sign', 's',
         'sign files to upload using gpg'),
        ('identity=', 'i', 'GPG identity used to sign files'),
        ]
    boolean_options = ['show-response', 'sign']

    def initialize_options(self):
        self.username = ''
        self.password = ''
        self.repository = ''
        self.show_response = 0
        self.sign = False
        self.identity = None

    def finalize_options(self):
        if self.identity and not self.sign:
            raise DistutilsOptionError(
                "Must use --sign for --identity to have meaning"
            )
        if 'HOME' in os.environ:
            rc = os.path.join(os.environ['HOME'], '.pypirc')
            if os.path.exists(rc):
                self.announce('Using PyPI login from %s' % rc)
                config = ConfigParser.ConfigParser({
                        'username':'',
                        'password':'',
                        'repository':''})
                config.read(rc)
                if not self.repository:
                    self.repository = config.get('server-login', 'repository')
                if not self.username:
                    self.username = config.get('server-login', 'username')
                if not self.password:
                    self.password = config.get('server-login', 'password')
        if not self.repository:
            self.repository = self.DEFAULT_REPOSITORY

    def run(self):
        if not self.distribution.dist_files:
            raise DistutilsOptionError("No dist file created in earlier command")
        for command, pyversion, filename in self.distribution.dist_files:
            self.upload_file(command, pyversion, filename)

    def upload_file(self, command, pyversion, filename):
        # Sign if requested
        if self.sign:
            gpg_args = ["gpg", "--detach-sign", "-a", filename]
            if self.identity:
                gpg_args[2:2] = ["--local-user", self.identity]
            spawn(gpg_args,
                  dry_run=self.dry_run)

        # Fill in the data
        f = open(filename,'rb')
        content = f.read()
        f.close()
        basename = os.path.basename(filename)
        comment = ''
        if command=='bdist_egg' and self.distribution.has_ext_modules():
            comment = "built on %s" % platform.platform(terse=1)
        data = {
            ':action':'file_upload',
            'protocol_version':'1',
            'name':self.distribution.get_name(),
            'version':self.distribution.get_version(),
            'content':(basename,content),
            'filetype':command,
            'pyversion':pyversion,
            'md5_digest':md5(content).hexdigest(),
            }
        if command == 'bdist_rpm':
            dist, version, id = platform.dist()
            if dist:
                comment = 'built for %s %s' % (dist, version)
        elif command == 'bdist_dumb':
            comment = 'built for %s' % platform.platform(terse=1)
        data['comment'] = comment

        if self.sign:
            asc_file = open(filename + ".asc")
            data['gpg_signature'] = (os.path.basename(filename) + ".asc", asc_file.read())
            asc_file.close()

        # set up the authentication
        auth = "Basic " + base64.encodestring(self.username + ":" + self.password).strip()

        # Build up the MIME payload for the POST data
        boundary = '--------------GHSKFJDLGDS7543FJKLFHRE75642756743254'
        sep_boundary = '\n--' + boundary
        end_boundary = sep_boundary + '--'
        body = StringIO.StringIO()
        for key, value in data.items():
            # handle multiple entries for the same name
            if type(value) != type([]):
                value = [value]
            for value in value:
                if type(value) is tuple:
                    fn = ';filename="%s"' % value[0]
                    value = value[1]
                else:
                    fn = ""
                value = str(value)
                body.write(sep_boundary)
                body.write('\nContent-Disposition: form-data; name="%s"'%key)
                body.write(fn)
                body.write("\n\n")
                body.write(value)
                if value and value[-1] == '\r':
                    body.write('\n')  # write an extra newline (lurve Macs)
        body.write(end_boundary)
        body.write("\n")
        body = body.getvalue()

        self.announce("Submitting %s to %s" % (filename, self.repository), log.INFO)

        # build the Request
        # We can't use urllib2 since we need to send the Basic
        # auth right with the first request
        schema, netloc, url, params, query, fragments = \
            urlparse(self.repository)
        assert not params and not query and not fragments
        if schema == 'http':
            http = httplib.HTTPConnection(netloc)
        elif schema == 'https':
            http = httplib.HTTPSConnection(netloc)
        else:
            raise AssertionError("unsupported schema " + schema)

        data = ''
        loglevel = log.INFO
        try:
            http.connect()
            http.putrequest("POST", url)
            http.putheader('Content-type',
                           'multipart/form-data; boundary=%s'%boundary)
            http.putheader('Content-length', str(len(body)))
            http.putheader('Authorization', auth)
            http.endheaders()
            http.send(body)
        except socket.error:
            e = sys.exc_info()[1]
            self.announce(str(e), log.ERROR)
            return

        r = http.getresponse()
        if r.status == 200:
            self.announce('Server response (%s): %s' % (r.status, r.reason),
                          log.INFO)
        else:
            self.announce('Upload failed (%s): %s' % (r.status, r.reason),
                          log.ERROR)
        if self.show_response:
            print('-'*75, r.read(), '-'*75)
