try:
    import idlelib.PyShell
except ImportError:
    # IDLE is not installed, but maybe PyShell is on sys.path:
    try:
        import PyShell
    except ImportError:
        raise
    else:
        import os
        idledir = os.path.dirname(os.path.abspath(PyShell.__file__))
        if idledir != os.getcwd():
            # We're not in the IDLE directory, help the subprocess find run.py
            pypath = os.environ.get('PYTHONPATH', '')
            if pypath:
                os.environ['PYTHONPATH'] = pypath + ':' + idledir
            else:
                os.environ['PYTHONPATH'] = idledir
        PyShell.main()
else:
    idlelib.PyShell.main()
