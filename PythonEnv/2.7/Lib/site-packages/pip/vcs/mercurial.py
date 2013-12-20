import os
import tempfile
import re
import sys
from pip.util import call_subprocess
from pip.util import display_path, rmtree
from pip.log import logger
from pip.vcs import vcs, VersionControl
from pip.download import path_to_url2
from pip.backwardcompat import ConfigParser


class Mercurial(VersionControl):
    name = 'hg'
    dirname = '.hg'
    repo_name = 'clone'
    schemes = ('hg', 'hg+http', 'hg+https', 'hg+ssh', 'hg+static-http')
    bundle_file = 'hg-clone.txt'
    guide = ('# This was a Mercurial repo; to make it a repo again run:\n'
            'hg init\nhg pull %(url)s\nhg update -r %(rev)s\n')

    def parse_vcs_bundle_file(self, content):
        url = rev = None
        for line in content.splitlines():
            if not line.strip() or line.strip().startswith('#'):
                continue
            url_match = re.search(r'hg\s*pull\s*(.*)\s*', line)
            if url_match:
                url = url_match.group(1).strip()
            rev_match = re.search(r'^hg\s*update\s*-r\s*(.*)\s*', line)
            if rev_match:
                rev = rev_match.group(1).strip()
            if url and rev:
                return url, rev
        return None, None

    def export(self, location):
        """Export the Hg repository at the url to the destination location"""
        temp_dir = tempfile.mkdtemp('-export', 'pip-')
        self.unpack(temp_dir)
        try:
            call_subprocess(
                [self.cmd, 'archive', location],
                filter_stdout=self._filter, show_stdout=False, cwd=temp_dir)
        finally:
            rmtree(temp_dir)

    def switch(self, dest, url, rev_options):
        repo_config = os.path.join(dest, self.dirname, 'hgrc')
        config = ConfigParser.SafeConfigParser()
        try:
            config.read(repo_config)
            config.set('paths', 'default', url)
            config_file = open(repo_config, 'w')
            config.write(config_file)
            config_file.close()
        except (OSError, ConfigParser.NoSectionError):
            e = sys.exc_info()[1]
            logger.warn(
                'Could not switch Mercurial repository to %s: %s'
                % (url, e))
        else:
            call_subprocess([self.cmd, 'update', '-q'] + rev_options, cwd=dest)

    def update(self, dest, rev_options):
        call_subprocess([self.cmd, 'pull', '-q'], cwd=dest)
        call_subprocess(
            [self.cmd, 'update', '-q'] + rev_options, cwd=dest)

    def obtain(self, dest):
        url, rev = self.get_url_rev()
        if rev:
            rev_options = [rev]
            rev_display = ' (to revision %s)' % rev
        else:
            rev_options = []
            rev_display = ''
        if self.check_destination(dest, url, rev_options, rev_display):
            logger.notify('Cloning hg %s%s to %s'
                          % (url, rev_display, display_path(dest)))
            call_subprocess([self.cmd, 'clone', '--noupdate', '-q', url, dest])
            call_subprocess([self.cmd, 'update', '-q'] + rev_options, cwd=dest)

    def get_url(self, location):
        url = call_subprocess(
            [self.cmd, 'showconfig', 'paths.default'],
            show_stdout=False, cwd=location).strip()
        if self._is_local_repository(url):
            url = path_to_url2(url)
        return url.strip()

    def get_tag_revs(self, location):
        tags = call_subprocess(
            [self.cmd, 'tags'], show_stdout=False, cwd=location)
        tag_revs = []
        for line in tags.splitlines():
            tags_match = re.search(r'([\w\d\.-]+)\s*([\d]+):.*$', line)
            if tags_match:
                tag = tags_match.group(1)
                rev = tags_match.group(2)
                if "tip" != tag:
                    tag_revs.append((rev.strip(), tag.strip()))
        return dict(tag_revs)

    def get_branch_revs(self, location):
        branches = call_subprocess(
            [self.cmd, 'branches'], show_stdout=False, cwd=location)
        branch_revs = []
        for line in branches.splitlines():
            branches_match = re.search(r'([\w\d\.-]+)\s*([\d]+):.*$', line)
            if branches_match:
                branch = branches_match.group(1)
                rev = branches_match.group(2)
                if "default" != branch:
                    branch_revs.append((rev.strip(), branch.strip()))
        return dict(branch_revs)

    def get_revision(self, location):
        current_revision = call_subprocess(
            [self.cmd, 'parents', '--template={rev}'],
            show_stdout=False, cwd=location).strip()
        return current_revision

    def get_revision_hash(self, location):
        current_rev_hash = call_subprocess(
            [self.cmd, 'parents', '--template={node}'],
            show_stdout=False, cwd=location).strip()
        return current_rev_hash

    def get_src_requirement(self, dist, location, find_tags):
        repo = self.get_url(location)
        if not repo.lower().startswith('hg:'):
            repo = 'hg+' + repo
        egg_project_name = dist.egg_name().split('-', 1)[0]
        if not repo:
            return None
        current_rev = self.get_revision(location)
        current_rev_hash = self.get_revision_hash(location)
        tag_revs = self.get_tag_revs(location)
        branch_revs = self.get_branch_revs(location)
        if current_rev in tag_revs:
            # It's a tag
            full_egg_name = '%s-%s' % (egg_project_name, tag_revs[current_rev])
        elif current_rev in branch_revs:
            # It's the tip of a branch
            full_egg_name = '%s-%s' % (egg_project_name, branch_revs[current_rev])
        else:
            full_egg_name = '%s-dev' % egg_project_name
        return '%s@%s#egg=%s' % (repo, current_rev_hash, full_egg_name)

vcs.register(Mercurial)
