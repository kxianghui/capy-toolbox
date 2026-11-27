#!/usr/bin/env python
# Embed a Win32 RT_VERSION (VERSIONINFO) resource into an already-compiled exe
# using the Win32 UpdateResource API. Works without rc.exe/.res, and keeps the
# icon supplied via csc -win32icon intact (csc rejects -win32res with -win32icon).
import argparse
import ctypes
import ctypes.wintypes as wt
import struct
import sys
from argparse import Namespace

RT_VERSION = 16
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
version = ctypes.WinDLL("version", use_last_error=True)


# --- VS_VERSIONINFO builder -------------------------------------------------

def align4(data):
    return data + b"\x00" * ((-len(data)) % 4)


def align_key(key):
    # A block is a 6-byte header (3 WORDs) followed by the UTF-16 key. The key
    # must be padded so the following Value starts on a DWORD boundary relative
    # to the block start, i.e. (6 + len(key)) % 4 == 0.
    return key + b"\x00" * ((-(6 + len(key))) % 4)


def u16(s):
    return s.encode("utf-16-le")


def text_block(key, value, wtype=1):
    key_b = align_key(u16(key) + b"\x00\x00")
    val_b = u16(value) + b"\x00\x00"
    total = (6 + len(key_b) + len(val_b) + 3) & ~3
    pad = total - (6 + len(key_b) + len(val_b))
    header = struct.pack("<HHH", total, len(value) + 1, wtype)
    return header + key_b + val_b + b"\x00" * pad


def container(key, children, wtype=1, wvalue=0):
    key_b = align_key(u16(key) + b"\x00\x00")
    body = key_b + children
    total = (6 + len(body) + 3) & ~3
    pad = total - (6 + len(body))
    header = struct.pack("<HHH", total, wvalue, wtype)
    return header + body + b"\x00" * pad


def translation_block(lang=0x0409, codepage=0x04B0):
    key_b = align_key(u16("Translation") + b"\x00\x00")
    value = struct.pack("<I", (codepage << 16) | lang)
    body = key_b + value
    total = (6 + len(body) + 3) & ~3
    pad = total - (6 + len(body))
    header = struct.pack("<HHH", total, 4, 0)  # binary value, 4 bytes
    return header + body + b"\x00" * pad


def parse_ver(s):
    parts = []
    for p in s.split("."):
        try:
            parts.append(int(p))
        except ValueError:
            parts.append(0)
    while len(parts) < 4:
        parts.append(0)
    return parts[:4]


def fixed_file_info(fileversion):
    fv = parse_ver(fileversion)
    return struct.pack(
        "<13I",
        0xFEEF04BD,               # dwSignature
        0x00010000,               # dwStrucVersion
        (fv[0] << 16) | fv[1],    # dwFileVersionMS
        (fv[2] << 16) | fv[3],    # dwFileVersionLS
        (fv[0] << 16) | fv[1],    # dwProductVersionMS
        (fv[2] << 16) | fv[3],    # dwProductVersionLS
        0x0000003F,               # dwFileFlagsMask
        0x00000000,               # dwFileFlags
        0x00040004,               # dwFileOS (VOS_NT_WINDOWS32)
        0x00000001,               # dwFileType (VFT_APP)
        0x00000000,               # dwFileSubtype
        0x00000000,               # dwFileDateMS
        0x00000000,               # dwFileDateLS
    )


def build_versioninfo(args, lang=0x0804, codepage=0x04B0):
    table_key = "%04X%04X" % (lang, codepage)
    strings = [
        ("CompanyName", args.company),
        ("FileDescription", args.description),
        ("FileVersion", args.fileversion + ".0"
         if args.fileversion.count(".") == 2 else args.fileversion),
        ("InternalName", "Capy"),
        ("LegalCopyright", args.copyright),
        ("OriginalFilename", args.original),
        ("ProductName", "Capy"),
        ("ProductVersion", args.productversion),
    ]
    string_children = b"".join(text_block(k, v) for k, v in strings)
    string_table = container(table_key, string_children)
    string_file_info = container("StringFileInfo", string_table)
    var_info = container("VarFileInfo", translation_block(lang, codepage))

    value = fixed_file_info(args.fileversion)
    key_b = align_key(u16("VS_VERSION_INFO") + b"\x00\x00")
    children = align4(string_file_info) + align4(var_info)
    body = key_b + align4(value) + children
    total = (6 + len(body) + 3) & ~3
    pad = total - (6 + len(body))
    header = struct.pack("<HHH", total, len(value), 0)  # binary value
    return header + body + b"\x00" * pad


# --- Win32 resource injection ----------------------------------------------

def inject(exe, blob, res_id, lang):
    begin = kernel32.BeginUpdateResourceW
    begin.restype = wt.HANDLE
    begin.argtypes = [wt.LPCWSTR, wt.BOOL]
    update = kernel32.UpdateResourceW
    update.restype = wt.BOOL
    update.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p,
                       wt.WORD, ctypes.c_void_p, wt.DWORD]
    end = kernel32.EndUpdateResourceW
    end.restype = wt.BOOL
    end.argtypes = [wt.HANDLE, wt.BOOL]

    h = begin(exe, False)
    if not h:
        raise OSError("BeginUpdateResource failed: %d" % ctypes.get_last_error())

    # csc auto-embeds an RT_VERSION at language 0 (neutral, version 0.0.0.0).
    # UpdateResource replaces a same type/name/lang entry in place, so targeting
    # lang 0 overwrites it. If another language is requested, drop the neutral
    # default first so GetFileVersionInfo does not read it instead.
    if lang != 0x0000:
        update(h, ctypes.c_void_p(RT_VERSION), ctypes.c_void_p(res_id),
               0x0000, None, 0)

    buf = ctypes.create_string_buffer(blob, len(blob))
    ok = update(h, ctypes.c_void_p(RT_VERSION), ctypes.c_void_p(res_id),
                lang, buf, len(blob))
    if not ok:
        err = ctypes.get_last_error()
        end(h, True)  # discard
        raise OSError("UpdateResource failed: %d" % err)

    if not end(h, False):
        raise OSError("EndUpdateResource failed: %d" % ctypes.get_last_error())


def verify(exe, lang=0x0804, codepage=0x04B0):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    table_key = "%04X%04X" % (lang, codepage)
    size = version.GetFileVersionInfoSizeW(exe, None)
    if not size:
        print("verify: NO version resource found")
        return False
    buf = ctypes.create_string_buffer(size)
    if not version.GetFileVersionInfoW(exe, 0, size, buf):
        print("verify: GetFileVersionInfo failed")
        return False

    def query_string(key):
        ptr = ctypes.c_void_p()
        ln = ctypes.c_uint()
        if not version.VerQueryValueW(buf, "\\StringFileInfo\\" + table_key + "\\" + key,
                                      ctypes.byref(ptr), ctypes.byref(ln)):
            return None
        return ctypes.wstring_at(ptr.value, ln.value).rstrip("\x00")

    def query_fixed():
        ptr = ctypes.c_void_p()
        ln = ctypes.c_uint()
        if not version.VerQueryValueW(buf, "\\", ctypes.byref(ptr), ctypes.byref(ln)):
            return None
        vals = ctypes.cast(ptr, ctypes.POINTER(ctypes.c_uint32))
        ms, ls = vals[2], vals[3]
        return "%d.%d.%d.%d" % (ms >> 16, ms & 0xFFFF, ls >> 16, ls & 0xFFFF)

    ptr = ctypes.c_void_p()
    ln = ctypes.c_uint()
    if version.VerQueryValueW(buf, "\\VarFileInfo\\Translation",
                              ctypes.byref(ptr), ctypes.byref(ln)) and ln.value >= 4:
        langid, cp = struct.unpack("<HH", ctypes.string_at(ptr.value, 4))
        print("verify: Translation lang=0x%04X codepage=0x%04X" % (langid, cp))

    fixed = query_fixed()
    print("verify: FixedFileVersion =", fixed)
    for k in ("FileVersion", "ProductVersion", "FileDescription",
              "CompanyName", "OriginalFilename", "LegalCopyright", "ProductName"):
        print("verify: %-18s = %r" % (k, query_string(k)))
    return fixed is not None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--fileversion", required=True)
    ap.add_argument("--productversion", required=True)
    ap.add_argument("--original", required=True)
    ap.add_argument("--description", default="Capy")
    ap.add_argument("--company", default="kxianghui")
    ap.add_argument("--copyright", default="\u00a9 2026 kxianghui \u4fdd\u7559\u6240\u6709\u6743\u5229")
    ap.add_argument("--id", type=int, default=1)
    ap.add_argument("--lang", type=lambda s: int(s, 0), default=0x0804)
    ap.add_argument("--verify-only", action="store_true")
    args = ap.parse_args()

    if not args.verify_only:
        ns = Namespace(fileversion=args.fileversion,
                       productversion=args.productversion,
                       original=args.original, description=args.description,
                       company=args.company, copyright=args.copyright)
        blob = build_versioninfo(ns, args.lang)
        inject(args.exe, blob, args.id, args.lang)
        print("injected RT_VERSION (%d bytes) into %s" % (len(blob), args.exe))

    sys.exit(0 if verify(args.exe, args.lang) else 1)


if __name__ == "__main__":
    main()
