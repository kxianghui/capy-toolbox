import os
import sys


def fail(message, code):
    sys.stderr.write(message + "\n")
    return code


def main():
    if len(sys.argv) != 3:
        return fail("usage: pdf2docx_engine.exe <input.pdf> <output.docx>", 2)

    src = os.path.abspath(sys.argv[1])
    dst = os.path.abspath(sys.argv[2])

    if not os.path.isfile(src):
        return fail("input not found: " + src, 3)

    try:
        from pdf2docx import Converter

        converter = Converter(src)
        try:
            converter.convert(dst)
        finally:
            converter.close()
    except Exception as exc:
        return fail("conversion failed: {0}".format(exc), 1)

    if not os.path.isfile(dst) or os.path.getsize(dst) == 0:
        return fail("output missing or empty", 1)

    return 0


if __name__ == "__main__":
    sys.exit(main())
