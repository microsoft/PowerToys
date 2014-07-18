import tempfile
import re
import os.path
from pip.util import call_subprocess
from pip.util import display_path, rmtree
from pip.vcs import vcs, VersionControl
from pip.log import logger
from pip.backwardcompat import url2pathname, urlparse
urlsplit = urlparse.urlsplit
urlunsplit = urlparse.urlunsplit


class Git(VersionControl):
    name = 'git'
    dirname = '.git'
    repo_name = 'clone'
    schemes = ('git', 'git+http', 'git+https', 'git+ssh', 'git+git', 'git+file')
    bundle_file = 'git-clone.txt'
    guide = ('# This was a Git repo; to make it a repo again run:\n'
        'git init\ngit remote add origin %(url)s -f\ngit checkout %(rev)s\n')

    def __init__(self, url=None, *args, **kwargs):

        # Works around an apparent Git bug
        # (see http://article.gmane.org/gmane.comp.version-control.git/146500)
        if url:
            scheme, netloc, path, query, fragment = urlsplit(url)
            if scheme.endswith('file'):
                initial_slashes = path[:-len(path.lstrip('/'))]
                newpath = initial_slashes + url2pathname(path).replace('\\', '/').lstrip('/')
                url = urlunsplit((scheme, netloc, newpath, query, fragment))
                after_plus = scheme.find('+') + 1
                url = scheme[:after_plus] + urlunsplit((scheme[after_plus:], netloc, newpath, query, fragment))

        super(Git, self).__init__(url, *args, **kwargs)

    def parse_vcs_bundle_file(self, content):
        url = rev = None
        for line in content.splitlines():
            if not line.strip() or line.strip().startswith('#'):
                continue
            url_match = re.search(r'git\s*remote\s*add\s*origin(.*)\s*-f', line)
            if url_match:
                url = url_match.group(1).strip()
            rev_match = re.search(r'^git\s*checkout\s*-q\s*(.*)\s*', line)
            if rev_match:
                rev = rev_match.group(1).strip()
            if url and rev:
                return url, rev
        return None, None

    def export(self, location):
        """Export the Git repository at the url to the destination location"""
        temp_dir = tempfile.mkdtemp('-export', 'pip-')
        self.unpack(temp_dir)
        try:
            if not location.endswith('/'):
                location = location + '/'
            call_subprocess(
                [self.cmd, 'checkout-index', '-a', '-f', '--prefix', location],
                filter_stdout=self._filter, show_stdout=False, cwd=temp_dir)
        finally:
            rmtree(temp_dir)

    def check_rev_options(self, rev, dest, rev_options):
        """Check the revision options before checkout to compensate that tags
        and branches may need origin/ as a prefix.
        Returns the SHA1 of the branch or tag if found.
        """
        revisions = self.get_refs(dest)

        origin_rev = 'origin/%s' % rev
        if origin_rev in revisions:
            # remote branch
            return [revisions[origin_rev]]
        elif rev in revisions:
            # a local tag or branch name
            return [revisions[rev]]
        else:
            logger.warn("Could not find a tag or branch '%s', assuming commit." % rev)
            return rev_options

    def switch(self, dest, url, rev_options):
        call_subprocess(
            [self.cmd, 'config', 'remote.origin.url', url], cwd=dest)
        call_subprocess(
            [self.cmd, 'checkout', '-q'] + rev_options, cwd=dest)

        self.update_submodules(dest)

    def update(self, dest, rev_options):
        # First fetch changes from the default remote
        call_subprocess([self.cmd, 'fetch', '-q'], cwd=dest)
        # Then reset to wanted revision (maby even origin/master)
        if rev_options:
            rev_options = self.check_rev_options(rev_options[0], dest, rev_options)
        call_subprocess([self.cmd, 'reset', '--hard', '-q'] + rev_options, cwd=dest)
        #: update submodules
        self.update_submodules(dest)

    def obtain(self, dest):
        url, rev = self.get_url_rev()
        if rev:
            rev_options = [rev]
            rev_display = ' (to %s)' % rev
        else:
            rev_options = ['origin/master']
            rev_display = ''
        if self.check_destination(dest, url, rev_options, rev_display):
            logger.notify('Cloning %s%s to %s' % (url, rev_display, display_path(dest)))
            call_subprocess([self.cmd, 'clone', '-q', url, dest])
            #: repo may contain submodules
            self.update_submodules(dest)
            if rev:
                rev_options = self.check_rev_options(rev, dest, rev_options)
                # Only do a checkout if rev_options differs from HEAD
                if not self.get_revision(dest).startswith(rev_options[0]):
                    call_subprocess([self.cmd, 'checkout', '-q'] + rev_options, cwd=dest)

    def get_url(self, location):
        url = call_subprocess(
            [self.cmd, 'config', 'remote.origin.url'],
            show_stdout=False, cwd=location)
        return url.strip()

    def get_revision(self, location):
        current_rev = call_subprocess(
            [self.cmd, 'rev-parse', 'HEAD'], show_stdout=False, cwd=location)
        return current_rev.strip()

    def get_refs(self, location):
        """Return map of named refs (branches or tags) to commit hashes."""
        output = call_subprocess([self.cmd, 'show-ref'],
                                 show_stdout=False, cwd=location)
        rv = {}
        for line in output.strip().splitlines():
            commit, ref = line.split(' ', 1)
            ref = ref.strip()
            ref_name = None
            if ref.startswith('refs/remotes/'):
                ref_name = ref[len('refs/remotes/'):]
            elif ref.startswith('refs/heads/'):
                ref_name = ref[len('refs/heads/'):]
            elif ref.startswith('refs/tags/'):
                ref_name = ref[len('refs/tags/'):]
            if ref_name is not None:
                rv[ref_name] = commit.strip()
        return rv

    def get_src_requirement(self, dist, location, find_tags):
        repo = self.get_url(location)
        if not repo.lower().startswith('git:'):
            repo = 'git+' + repo
        egg_project_name = dist.egg_name().split('-', 1)[0]
        if not repo:
            return None
        current_rev = self.get_revision(location)
        refs = self.get_refs(location)
        # refs maps names to commit hashes; we need the inverse
        # if multiple names map to a single commit, this arbitrarily picks one
        names_by_commit = dict((commit, ref) for ref, commit in refs.items())

        if current_rev in names_by_commit:
            # It's a tag
            full_egg_name = '%s-%s' % (egg_project_name, names_by_commit[current_rev])
        else:
            full_egg_name = '%s-dev' % egg_project_name

        return '%s@%s#egg=%s' % (repo, current_rev, full_egg_name)

    def get_url_rev(self):
        """
        Prefixes stub URLs like 'user@hostname:user/repo.git' with 'ssh://'.
        That's required because although they use SSH they sometimes doesn't
        work with a ssh:// scheme (e.g. Github). But we need a scheme for
        parsing. Hence we remove it again afterwards and return it as a stub.
        """
        if not '://' in self.url:
            assert not 'file:' in self.url
            self.url = self.url.replace('git+', 'git+ssh://')
            url, rev = super(Git, self).get_url_rev()
            url = url.replace('ssh://', '')
        else:
            url, rev = super(Git, self).get_url_rev()

        return url, rev

    def update_submodules(self, location):
        if not os.path.exists(os.path.join(location, '.gitmodules')):
            return
        call_subprocess([self.cmd, 'submodule', 'update', '--init', '--recursive', '-q'],
                        cwd=location)

vcs.register(Git)
