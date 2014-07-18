from pip.commands.zip import ZipCommand


class UnzipCommand(ZipCommand):
    """Unzip individual packages."""
    name = 'unzip'
    summary = 'DEPRECATED. Unzip individual packages.'
