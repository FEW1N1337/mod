"""Hermes plugin shim for Beyin V3; the implementation lives in the vault."""
import importlib.util, os
from pathlib import Path

VAULT = Path(os.environ.get('BEYIN_VAULT') or '/home/user/mod')


def register(ctx):
    module_path = VAULT / '.claude/scripts/beyin_v3_hermes.py'
    if not module_path.is_file():
        return
    spec = importlib.util.spec_from_file_location('beyin_v3_hermes', module_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.register(ctx, vault=VAULT)
