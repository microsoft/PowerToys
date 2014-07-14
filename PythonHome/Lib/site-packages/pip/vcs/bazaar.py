import os
import tempfile
import re
from pip.backwardcompat import urlparse
from pip.log import logger
from pip.util import rmtree, display_path, call_subprocess
from pip.vcs import vcs, VersionControl
from pip.download import path_to_url


class Bazaar(VersionControl):
    name = 'bzr'
    dirname = '.bzr'
    repo_name = 'branch'
    bundle_file = 'bzr-branch.txt'
    schemes = ('bzr', 'bzr+http', 'bzr+https', 'bzr+ssh', 'bzr+sftp', 'bzr+ftp', 'bzr+lp')
    guide = ('# This was a Bazaar branch; to make it a branch again run:\n'
             'bzr branch -r %(rev)s %(url)s .\n')

    def __init__(self, url=None, *args, **kwargs):
        super(Bazaar, self).__init__(url, *args, **kwargs)
        # Python >= 2.7.4, 3.3 doesn't have uses_fragment or non_hierarchical
        # Register lp but do not expose as a scheme to support bzr+lp.
        if getattr(urlparse, 'uses_fragment', None):
            urlparse.uses_fragment.extend(['lp'])
            urlparse.non_hierarchical.extend(['lp'])

    def parse_vcs_bundle_file(self, content):
        url = rev = None
        for line in content.splitlines():
            if not line.strip() or line.strip().startswith('#'):
                continue
            match = re.search(r'^bzr\s*branch\s*-r\s*(\d*)', line)
            if match:
                rev = match.group(1).strip()
            url = line[match.end():].strip().split(None, 1)[0]
            if url and rev:
                return url, rev
        return None, None

    def export(self, location):
        """Export the Bazaar repository at the url to the destination location"""
        temp_dir = tempfile.mkdtemp('-export', 'pip-')
        self.unpack(temp_dir)
        if os.path.exists(location):
            # Remove the location to make sure Bazaar can export it correctly
            rmtree(location)
        try:
            call_subprocess([self.cmd, 'export', location], cwd=temp_dir,
                            filter_stdout=self._filter, show_stdout=False)
        finally:
            rmtree(temp_dir)

    def switch(self, dest, url, rev_options):
        call_subprocess([self.cmd, 'switch', url], cwd=dest)

    def update(self, dest, rev_options):
        call_subprocess(
            [self.cmd, 'pull', '-q'] + rev_options, cwd=dest)

    def obtain(self, dest):
        url, rev = self.get_url_rev()
        if rev:
            rev_options = ['-r', rev]
            rev_display = ' (to revision %s)' % rev
        else:
            rev_options = []
            rev_display = ''
        if self.check_destination(dest, url, rev_options, rev_display):
            logger.notify('Checking out %s%s to %s'
                          % (url, rev_display, display_path(dest)))
            call_subprocess(
                [self.cmd, 'branch', '-q'] + rev_options + [url, dest])

    def get_url_rev(self):
        # hotfix the URL scheme after removing bzr+ from bzr+ssh:// readd it
        url, rev = super(Bazaar, self).get_url_rev()
        if url.startswith('ssh://'):
            url = 'bzr+' + url
        return url, rev

    def get_url(self, location):
        urls = call_subprocess(
            [self.cmd, 'info'], show_stdout=False, cwd=location)
        for line in urls.splitlines():
            line = line.strip()
            for x in ('checkout of branch: ',
                      'parent branch: '):
                if line.startswith(x):
                    repo = line.split(x)[1]
                    if self._is_local_repository(repo):
                        return path_to_url(repo)
                    return repo
        return None

    def get_revision(self, location):
        revision = call_subprocess(
            [self.cmd, 'revno'], show_stdout=False, cwd=location)
        return revision.splitlines()[-1]

    def get_tag_revs(self, location):
        tags = call_subprocess(
            [self.cmd, 'tags'], show_stdout=False, cwd=location)
        tag_revs = []
        for line in tags.splitlines():
            tags_match = re.search(r'([.\w-]+)\s*(.*)$', line)
            if tags_match:
                tag = tags_match.group(1)
                rev = tags_match.group(2)
                tag_revs.append((rev.strip(), tag.strip()))
        return dict(tag_revs)

    def get_src_requirement(self, dist, location, find_tags):
        repo = self.get_url(location)
        if not repo.lower().startswith('bzr:'):
            repo = 'bzr+' + repo
        egg_project_name = dist.egg_name().split('-', 1)[0]
        if not repo:
            return None
        current_rev = self.get_revision(location)
        tag_revs = self.get_tag_revs(location)

        if current_rev in tag_revs:
            # It's a tag
            full_egg_name = '%s-%s' % (egg_project_name, tag_revs[current_rev])
        else:
            full_egg_name = '%s-dev_r%s' % (dist.egg_name(), current_rev)
        return '%s@%s#egg=%s' % (repo, current_rev, full_egg_name)


vcs.register(Bazaar)
