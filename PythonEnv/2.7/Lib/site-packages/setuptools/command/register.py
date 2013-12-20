from distutils.command.register import register as _register

class register(_register):
    __doc__ = _register.__doc__

    def run(self):
        # Make sure that we are using valid current name/version info
        self.run_command('egg_info')
        _register.run(self)

