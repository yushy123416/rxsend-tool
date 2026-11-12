"""Verify PE icon resource payloads against the authored ICO, bypassing shell cache."""
import ctypes as C
from ctypes import wintypes as W
from pathlib import Path
import struct
import sys
import io
from PIL import Image

binary = Path(sys.argv[1]).resolve()
ico = Path("assets/app.ico").read_bytes()
api = C.WinDLL("kernel32", use_last_error=True)
api.LoadLibraryExW.argtypes = [W.LPCWSTR, W.HANDLE, W.DWORD]
api.LoadLibraryExW.restype = W.HMODULE
api.FindResourceW.argtypes = [W.HMODULE, C.c_void_p, C.c_void_p]
api.FindResourceW.restype = W.HRSRC
api.SizeofResource.argtypes = [W.HMODULE, W.HRSRC]
api.SizeofResource.restype = W.DWORD
api.LoadResource.argtypes = [W.HMODULE, W.HRSRC]
api.LoadResource.restype = W.HGLOBAL
api.LockResource.argtypes = [W.HGLOBAL]
api.LockResource.restype = C.c_void_p
api.FreeLibrary.argtypes = [W.HMODULE]
module = api.LoadLibraryExW(str(binary), None, 0x22)
if not module:
    raise C.WinError(C.get_last_error())

def resource(kind, name):
    key = C.cast(C.c_wchar_p(name), C.c_void_p) if isinstance(name, str) else C.c_void_p(name)
    handle = api.FindResourceW(module, key, C.c_void_p(kind))
    if not handle:
        raise C.WinError(C.get_last_error())
    size = api.SizeofResource(module, handle)
    return C.string_at(api.LockResource(api.LoadResource(module, handle)), size)

try:
    names = []
    callback_type = C.WINFUNCTYPE(W.BOOL, W.HMODULE, C.c_void_p, C.c_void_p, W.LPARAM)
    @callback_type
    def collect(handle, kind, name, param):
        names.append(name if name <= 0xFFFF else C.wstring_at(name))
        return True
    api.EnumResourceNamesW.argtypes = [W.HMODULE, C.c_void_p, callback_type, W.LPARAM]
    api.EnumResourceNamesW(module, C.c_void_p(14), collect, 0)
    assert len(names) == 1, "Expected one primary icon group"
    group = resource(14, names[0])
    count = struct.unpack_from("<H", group, 4)[0]
    expected_count = struct.unpack_from("<H", ico, 4)[0]
    expected = []
    for i in range(expected_count):
        size, offset = struct.unpack_from("<II", ico, 6 + 16*i + 8)
        expected.append(ico[offset:offset+size])
    payloads = []
    for i in range(count):
        icon_id = struct.unpack_from("<H", group, 6 + 14*i + 12)[0]
        payloads.append(resource(3, icon_id))
    assert count == expected_count and all(p in payloads for p in expected), "Embedded icon differs from assets/app.ico"
    largest = max(payloads, key=len)
    output = Path("build/ui-review/exe-icon.png")
    output.parent.mkdir(parents=True, exist_ok=True)
    Image.open(io.BytesIO(largest)).save(output)
    print(f"PASS: {binary.name} embeds all {count} expected ICO sizes (16 to 256 px)")
finally:
    api.FreeLibrary(module)
