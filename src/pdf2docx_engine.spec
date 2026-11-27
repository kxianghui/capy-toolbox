# -*- mode: python ; coding: utf-8 -*-

a = Analysis(
    ['pdf2docx_engine.py'],
    pathex=[],
    binaries=[],
    datas=[],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter', 'matplotlib', 'pandas', 'scipy', 'PyQt5', 'PySide2', 'IPython', 'pytest', 'pydoc_data'],
    noarchive=False,
    optimize=0,
)

# PDF conversion does not need video codecs: drop OpenCV's FFmpeg DLL to cut size.
a.binaries = [b for b in a.binaries if 'opencv_videoio_ffmpeg' not in b[0].lower()]
a.datas = [d for d in a.datas if '\\cv2\\data\\' not in d[0].lower() and '/cv2/data/' not in d[0].lower()]

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='pdf2docx_engine',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
