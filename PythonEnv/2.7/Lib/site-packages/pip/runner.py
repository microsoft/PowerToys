import sys
import os


def run():
    base = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ## FIXME: this is kind of crude; if we could create a fake pip
    ## module, then exec into it and update pip.__path__ properly, we
    ## wouldn't have to update sys.path:
    sys.path.insert(0, base)
    import pip
    return pip.main()


if __name__ == '__main__':
    exit = run()
    if exit:
        sys.exit(exit)
