# EASY-INSTALL-SCRIPT: %(spec)r,%(script_name)r
__requires__ = """%(spec)r"""
import pkg_resources
pkg_resources.run_script("""%(spec)r""", """%(script_name)r""")
