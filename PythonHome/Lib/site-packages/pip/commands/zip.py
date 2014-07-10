import sys
import re
import fnmatch
import os
import shutil
import zipfile
from pip.util import display_path, backup_dir, rmtree
from pip.log import logger
from pip.exceptions import InstallationError
from pip.basecommand import Command


class ZipCommand(Command):
    """Zip individual packages."""
    name = 'zip'
    usage = """
     %prog [options] <package> ..."""
    summary = 'DEPRECATED. Zip individual packages.'

    def __init__(self, *args, **kw):
        super(ZipCommand, self).__init__(*args, **kw)
        if self.name == 'zip':
            self.cmd_opts.add_option(
                '--unzip',
                action='store_true',
                dest='unzip',
                help='Unzip (rather than zip) a package.')
        else:
            self.cmd_opts.add_option(
                '--zip',
                action='store_false',
                dest='unzip',
                default=True,
                help='Zip (rather than unzip) a package.')
        self.cmd_opts.add_option(
            '--no-pyc',
            action='store_true',
            dest='no_pyc',
            help='Do not include .pyc files in zip files (useful on Google App Engine).')
        self.cmd_opts.add_option(
            '-l', '--list',
            action='store_true',
            dest='list',
            help='List the packages available, and their zip status.')
        self.cmd_opts.add_option(
            '--sort-files',
            action='store_true',
            dest='sort_files',
            help='With --list, sort packages according to how many files they contain.')
        self.cmd_opts.add_option(
            '--path',
            action='append',
            dest='paths',
            help='Restrict operations to the given paths (may include wildcards).')
        self.cmd_opts.add_option(
            '-n', '--simulate',
            action='store_true',
            help='Do not actually perform the zip/unzip operation.')

        self.parser.insert_option_group(0, self.cmd_opts)

    def paths(self):
        """All the entries of sys.path, possibly restricted by --path"""
        if not self.select_paths:
            return sys.path
        result = []
        match_any = set()
        for path in sys.path:
            path = os.path.normcase(os.path.abspath(path))
            for match in self.select_paths:
                match = os.path.normcase(os.path.abspath(match))
                if '*' in match:
                    if re.search(fnmatch.translate(match + '*'), path):
                        result.append(path)
                        match_any.add(match)
                        break
                else:
                    if path.startswith(match):
                        result.append(path)
                        match_any.add(match)
                        break
            else:
                logger.debug("Skipping path %s because it doesn't match %s"
                             % (path, ', '.join(self.select_paths)))
        for match in self.select_paths:
            if match not in match_any and '*' not in match:
                result.append(match)
                logger.debug("Adding path %s because it doesn't match "
                             "anything already on sys.path" % match)
        return result

    def run(self, options, args):

        logger.deprecated('1.7', "DEPRECATION: 'pip zip' and 'pip unzip` are deprecated, and will be removed in a future release.")

        self.select_paths = options.paths
        self.simulate = options.simulate
        if options.list:
            return self.list(options, args)
        if not args:
            raise InstallationError(
                'You must give at least one package to zip or unzip')
        packages = []
        for arg in args:
            module_name, filename = self.find_package(arg)
            if options.unzip and os.path.isdir(filename):
                raise InstallationError(
                    'The module %s (in %s) is not a zip file; cannot be unzipped'
                    % (module_name, filename))
            elif not options.unzip and not os.path.isdir(filename):
                raise InstallationError(
                    'The module %s (in %s) is not a directory; cannot be zipped'
                    % (module_name, filename))
            packages.append((module_name, filename))
        last_status = None
        for module_name, filename in packages:
            if options.unzip:
                last_status = self.unzip_package(module_name, filename)
            else:
                last_status = self.zip_package(module_name, filename, options.no_pyc)
        return last_status

    def unzip_package(self, module_name, filename):
        zip_filename = os.path.dirname(filename)
        if not os.path.isfile(zip_filename) and zipfile.is_zipfile(zip_filename):
            raise InstallationError(
                'Module %s (in %s) isn\'t located in a zip file in %s'
                % (module_name, filename, zip_filename))
        package_path = os.path.dirname(zip_filename)
        if not package_path in self.paths():
            logger.warn(
                'Unpacking %s into %s, but %s is not on sys.path'
                % (display_path(zip_filename), display_path(package_path),
                   display_path(package_path)))
        logger.notify('Unzipping %s (in %s)' % (module_name, display_path(zip_filename)))
        if self.simulate:
            logger.notify('Skipping remaining operations because of --simulate')
            return
        logger.indent += 2
        try:
            ## FIXME: this should be undoable:
            zip = zipfile.ZipFile(zip_filename)
            to_save = []
            for info in zip.infolist():
                name = info.filename
                if name.startswith(module_name + os.path.sep):
                    content = zip.read(name)
                    dest = os.path.join(package_path, name)
                    if not os.path.exists(os.path.dirname(dest)):
                        os.makedirs(os.path.dirname(dest))
                    if not content and dest.endswith(os.path.sep):
                        if not os.path.exists(dest):
                            os.makedirs(dest)
                    else:
                        f = open(dest, 'wb')
                        f.write(content)
                        f.close()
                else:
                    to_save.append((name, zip.read(name)))
            zip.close()
            if not to_save:
                logger.info('Removing now-empty zip file %s' % display_path(zip_filename))
                os.unlink(zip_filename)
                self.remove_filename_from_pth(zip_filename)
            else:
                logger.info('Removing entries in %s/ from zip file %s' % (module_name, display_path(zip_filename)))
                zip = zipfile.ZipFile(zip_filename, 'w')
                for name, content in to_save:
                    zip.writestr(name, content)
                zip.close()
        finally:
            logger.indent -= 2

    def zip_package(self, module_name, filename, no_pyc):
        orig_filename = filename
        logger.notify('Zip %s (in %s)' % (module_name, display_path(filename)))
        logger.indent += 2
        if filename.endswith('.egg'):
            dest_filename = filename
        else:
            dest_filename = filename + '.zip'
        try:
            ## FIXME: I think this needs to be undoable:
            if filename == dest_filename:
                filename = backup_dir(orig_filename)
                logger.notify('Moving %s aside to %s' % (orig_filename, filename))
                if not self.simulate:
                    shutil.move(orig_filename, filename)
            try:
                logger.info('Creating zip file in %s' % display_path(dest_filename))
                if not self.simulate:
                    zip = zipfile.ZipFile(dest_filename, 'w')
                    zip.writestr(module_name + '/', '')
                    for dirpath, dirnames, filenames in os.walk(filename):
                        if no_pyc:
                            filenames = [f for f in filenames
                                         if not f.lower().endswith('.pyc')]
                        for fns, is_dir in [(dirnames, True), (filenames, False)]:
                            for fn in fns:
                                full = os.path.join(dirpath, fn)
                                dest = os.path.join(module_name, dirpath[len(filename):].lstrip(os.path.sep), fn)
                                if is_dir:
                                    zip.writestr(dest + '/', '')
                                else:
                                    zip.write(full, dest)
                    zip.close()
                logger.info('Removing old directory %s' % display_path(filename))
                if not self.simulate:
                    rmtree(filename)
            except:
                ## FIXME: need to do an undo here
                raise
            ## FIXME: should also be undone:
            self.add_filename_to_pth(dest_filename)
        finally:
            logger.indent -= 2

    def remove_filename_from_pth(self, filename):
        for pth in self.pth_files():
            f = open(pth, 'r')
            lines = f.readlines()
            f.close()
            new_lines = [
                l for l in lines if l.strip() != filename]
            if lines != new_lines:
                logger.info('Removing reference to %s from .pth file %s'
                            % (display_path(filename), display_path(pth)))
                if not [line for line in new_lines if line]:
                    logger.info('%s file would be empty: deleting' % display_path(pth))
                    if not self.simulate:
                        os.unlink(pth)
                else:
                    if not self.simulate:
                        f = open(pth, 'wb')
                        f.writelines(new_lines)
                        f.close()
                return
        logger.warn('Cannot find a reference to %s in any .pth file' % display_path(filename))

    def add_filename_to_pth(self, filename):
        path = os.path.dirname(filename)
        dest = filename + '.pth'
        if path not in self.paths():
            logger.warn('Adding .pth file %s, but it is not on sys.path' % display_path(dest))
        if not self.simulate:
            if os.path.exists(dest):
                f = open(dest)
                lines = f.readlines()
                f.close()
                if lines and not lines[-1].endswith('\n'):
                    lines[-1] += '\n'
                lines.append(filename + '\n')
            else:
                lines = [filename + '\n']
            f = open(dest, 'wb')
            f.writelines(lines)
            f.close()

    def pth_files(self):
        for path in self.paths():
            if not os.path.exists(path) or not os.path.isdir(path):
                continue
            for filename in os.listdir(path):
                if filename.endswith('.pth'):
                    yield os.path.join(path, filename)

    def find_package(self, package):
        for path in self.paths():
            full = os.path.join(path, package)
            if os.path.exists(full):
                return package, full
            if not os.path.isdir(path) and zipfile.is_zipfile(path):
                zip = zipfile.ZipFile(path, 'r')
                try:
                    zip.read(os.path.join(package, '__init__.py'))
                except KeyError:
                    pass
                else:
                    zip.close()
                    return package, full
                zip.close()
        ## FIXME: need special error for package.py case:
        raise InstallationError(
            'No package with the name %s found' % package)

    def list(self, options, args):
        if args:
            raise InstallationError(
                'You cannot give an argument with --list')
        for path in sorted(self.paths()):
            if not os.path.exists(path):
                continue
            basename = os.path.basename(path.rstrip(os.path.sep))
            if os.path.isfile(path) and zipfile.is_zipfile(path):
                if os.path.dirname(path) not in self.paths():
                    logger.notify('Zipped egg: %s' % display_path(path))
                continue
            if (basename != 'site-packages' and basename != 'dist-packages'
                and not path.replace('\\', '/').endswith('lib/python')):
                continue
            logger.notify('In %s:' % display_path(path))
            logger.indent += 2
            zipped = []
            unzipped = []
            try:
                for filename in sorted(os.listdir(path)):
                    ext = os.path.splitext(filename)[1].lower()
                    if ext in ('.pth', '.egg-info', '.egg-link'):
                        continue
                    if ext == '.py':
                        logger.info('Not displaying %s: not a package' % display_path(filename))
                        continue
                    full = os.path.join(path, filename)
                    if os.path.isdir(full):
                        unzipped.append((filename, self.count_package(full)))
                    elif zipfile.is_zipfile(full):
                        zipped.append(filename)
                    else:
                        logger.info('Unknown file: %s' % display_path(filename))
                if zipped:
                    logger.notify('Zipped packages:')
                    logger.indent += 2
                    try:
                        for filename in zipped:
                            logger.notify(filename)
                    finally:
                        logger.indent -= 2
                else:
                    logger.notify('No zipped packages.')
                if unzipped:
                    if options.sort_files:
                        unzipped.sort(key=lambda x: -x[1])
                    logger.notify('Unzipped packages:')
                    logger.indent += 2
                    try:
                        for filename, count in unzipped:
                            logger.notify('%s  (%i files)' % (filename, count))
                    finally:
                        logger.indent -= 2
                else:
                    logger.notify('No unzipped packages.')
            finally:
                logger.indent -= 2

    def count_package(self, path):
        total = 0
        for dirpath, dirnames, filenames in os.walk(path):
            filenames = [f for f in filenames
                         if not f.lower().endswith('.pyc')]
            total += len(filenames)
        return total
