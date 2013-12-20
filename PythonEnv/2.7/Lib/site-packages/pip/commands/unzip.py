from pip.commands.zip import ZipCommand


class UnzipCommand(ZipCommand):
    """Unzip individual packages."""
    name = 'unzip'
    summary = 'Unzip individual packages.'
