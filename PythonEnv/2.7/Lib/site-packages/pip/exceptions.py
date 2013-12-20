"""Exceptions used throughout package"""


class PipError(Exception):
    """Base pip exception"""


class InstallationError(PipError):
    """General exception during installation"""


class UninstallationError(PipError):
    """General exception during uninstallation"""


class DistributionNotFound(InstallationError):
    """Raised when a distribution cannot be found to satisfy a requirement"""


class BestVersionAlreadyInstalled(PipError):
    """Raised when the most up-to-date version of a package is already
    installed.  """


class BadCommand(PipError):
    """Raised when virtualenv or a command is not found"""


class CommandError(PipError):
    """Raised when there is an error in command-line arguments"""


class PreviousBuildDirError(PipError):
    """Raised when there's a previous conflicting build directory"""


class HashMismatch(InstallationError):
    """Distribution file hash values don't match."""
