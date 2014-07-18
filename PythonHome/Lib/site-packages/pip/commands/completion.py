import sys
from pip.basecommand import Command

BASE_COMPLETION = """
# pip %(shell)s completion start%(script)s# pip %(shell)s completion end
"""

COMPLETION_SCRIPTS = {
    'bash': """
_pip_completion()
{
    COMPREPLY=( $( COMP_WORDS="${COMP_WORDS[*]}" \\
                   COMP_CWORD=$COMP_CWORD \\
                   PIP_AUTO_COMPLETE=1 $1 ) )
}
complete -o default -F _pip_completion pip
""", 'zsh': """
function _pip_completion {
  local words cword
  read -Ac words
  read -cn cword
  reply=( $( COMP_WORDS="$words[*]" \\
             COMP_CWORD=$(( cword-1 )) \\
             PIP_AUTO_COMPLETE=1 $words[1] ) )
}
compctl -K _pip_completion pip
"""}


class CompletionCommand(Command):
    """A helper command to be used for command completion."""
    name = 'completion'
    summary = 'A helper command to be used for command completion'
    hidden = True

    def __init__(self, *args, **kw):
        super(CompletionCommand, self).__init__(*args, **kw)
        self.parser.add_option(
            '--bash', '-b',
            action='store_const',
            const='bash',
            dest='shell',
            help='Emit completion code for bash')
        self.parser.add_option(
            '--zsh', '-z',
            action='store_const',
            const='zsh',
            dest='shell',
            help='Emit completion code for zsh')

    def run(self, options, args):
        """Prints the completion code of the given shell"""
        shells = COMPLETION_SCRIPTS.keys()
        shell_options = ['--' + shell for shell in sorted(shells)]
        if options.shell in shells:
            script = COMPLETION_SCRIPTS.get(options.shell, '')
            print(BASE_COMPLETION % {'script': script, 'shell': options.shell})
        else:
            sys.stderr.write('ERROR: You must pass %s\n' % ' or '.join(shell_options))
