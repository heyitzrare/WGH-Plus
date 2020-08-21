import bps.asm, sys

with open(sys.argv[1], "rb") as flashlight:
    bps.asm.disassemble(flashlight, sys.stdout)

sys.stdout.flush()
