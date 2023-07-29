"""
command line interface
"""
from argparse import ONE_OR_MORE, ArgumentParser


from . import __version__
from .engine import User


def run():
    """
    entry point
    """
    parser = ArgumentParser(
        prog="langue", description="run language analysis and manipulation engine"
    )
    parser.add_argument(
        "--version", action="version", version=f"%(prog)s {__version__}"
    )
    args = parser.parse_args()
    exit(0)
