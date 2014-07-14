"""
Package containing all pip commands
"""


from pip.commands.bundle import BundleCommand
from pip.commands.completion import CompletionCommand
from pip.commands.freeze import FreezeCommand
from pip.commands.help import HelpCommand
from pip.commands.list import ListCommand
from pip.commands.search import SearchCommand
from pip.commands.show import ShowCommand
from pip.commands.install import InstallCommand
from pip.commands.uninstall import UninstallCommand
from pip.commands.unzip import UnzipCommand
from pip.commands.zip import ZipCommand
from pip.commands.wheel import WheelCommand


commands = {
    BundleCommand.name: BundleCommand,
    CompletionCommand.name: CompletionCommand,
    FreezeCommand.name: FreezeCommand,
    HelpCommand.name: HelpCommand,
    SearchCommand.name: SearchCommand,
    ShowCommand.name: ShowCommand,
    InstallCommand.name: InstallCommand,
    UninstallCommand.name: UninstallCommand,
    UnzipCommand.name: UnzipCommand,
    ZipCommand.name: ZipCommand,
    ListCommand.name: ListCommand,
    WheelCommand.name: WheelCommand,
}


commands_order = [
    InstallCommand,
    UninstallCommand,
    FreezeCommand,
    ListCommand,
    ShowCommand,
    SearchCommand,
    WheelCommand,
    ZipCommand,
    UnzipCommand,
    BundleCommand,
    HelpCommand,
]


def get_summaries(ignore_hidden=True, ordered=True):
    """Yields sorted (command name, command summary) tuples."""

    if ordered:
        cmditems = _sort_commands(commands, commands_order)
    else:
        cmditems = commands.items()

    for name, command_class in cmditems:
        if ignore_hidden and command_class.hidden:
            continue

        yield (name, command_class.summary)


def get_similar_commands(name):
    """Command name auto-correct."""
    from difflib import get_close_matches

    close_commands = get_close_matches(name, commands.keys())

    if close_commands:
        guess = close_commands[0]
    else:
        guess = False

    return guess


def _sort_commands(cmddict, order):
    def keyfn(key):
        try:
            return order.index(key[1])
        except ValueError:
            # unordered items should come last
            return 0xff

    return sorted(cmddict.items(), key=keyfn)
