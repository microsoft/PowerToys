from pip.basecommand import Command, SUCCESS
from pip.exceptions import CommandError


class HelpCommand(Command):
    """Show help for commands"""
    name = 'help'
    usage = """
      %prog <command>"""
    summary = 'Show help for commands.'

    def run(self, options, args):
        from pip.commands import commands, get_similar_commands

        try:
            # 'pip help' with no args is handled by pip.__init__.parseopt()
            cmd_name = args[0]  # the command we need help for
        except IndexError:
            return SUCCESS

        if cmd_name not in commands:
            guess = get_similar_commands(cmd_name)

            msg = ['unknown command "%s"' % cmd_name]
            if guess:
                msg.append('maybe you meant "%s"' % guess)

            raise CommandError(' - '.join(msg))

        command = commands[cmd_name](self.main_parser)  # instantiate
        command.parser.print_help()

        return SUCCESS
