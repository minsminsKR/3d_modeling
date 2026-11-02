"""Run a background command on a private, never-switched Windows desktop.

The child and its normally inheriting descendants use this desktop; existing
external services are outside its control. Pass the project's usual batch,
input-isolation, logging and timeout flags to the command itself.
"""
import argparse
import ctypes
from ctypes import wintypes
import msvcrt
import os
from pathlib import Path
import subprocess
import sys
import uuid

if os.name!='nt':raise SystemExit('Windows desktop isolation required')
p=argparse.ArgumentParser();p.add_argument('--log',required=True);p.add_argument('--cwd',required=True);p.add_argument('command',nargs=argparse.REMAINDER);args=p.parse_args()
if args.command and args.command[0]=='--':args.command=args.command[1:]
if not args.command:raise SystemExit('A command is required')
user32=ctypes.WinDLL('user32',use_last_error=True);kernel32=ctypes.WinDLL('kernel32',use_last_error=True)
HANDLE=wintypes.HANDLE;DWORD=wintypes.DWORD;LPWSTR=wintypes.LPWSTR
class STARTUPINFO(ctypes.Structure):
    _fields_=[('cb',DWORD),('lpReserved',LPWSTR),('lpDesktop',LPWSTR),('lpTitle',LPWSTR),('dwX',DWORD),('dwY',DWORD),('dwXSize',DWORD),('dwYSize',DWORD),('dwXCountChars',DWORD),('dwYCountChars',DWORD),('dwFillAttribute',DWORD),('dwFlags',DWORD),('wShowWindow',wintypes.WORD),('cbReserved2',wintypes.WORD),('lpReserved2',ctypes.POINTER(ctypes.c_ubyte)),('hStdInput',HANDLE),('hStdOutput',HANDLE),('hStdError',HANDLE)]
class PROCESS_INFORMATION(ctypes.Structure):
    _fields_=[('hProcess',HANDLE),('hThread',HANDLE),('dwProcessId',DWORD),('dwThreadId',DWORD)]
user32.CreateDesktopW.argtypes=[wintypes.LPCWSTR,wintypes.LPCWSTR,ctypes.c_void_p,DWORD,DWORD,ctypes.c_void_p];user32.CreateDesktopW.restype=HANDLE
user32.CloseDesktop.argtypes=[HANDLE];user32.CloseDesktop.restype=wintypes.BOOL
kernel32.CreateProcessW.argtypes=[wintypes.LPCWSTR,LPWSTR,ctypes.c_void_p,ctypes.c_void_p,wintypes.BOOL,DWORD,ctypes.c_void_p,wintypes.LPCWSTR,ctypes.POINTER(STARTUPINFO),ctypes.POINTER(PROCESS_INFORMATION)];kernel32.CreateProcessW.restype=wintypes.BOOL
kernel32.WaitForSingleObject.argtypes=[HANDLE,DWORD];kernel32.WaitForSingleObject.restype=DWORD
kernel32.GetExitCodeProcess.argtypes=[HANDLE,ctypes.POINTER(DWORD)];kernel32.GetExitCodeProcess.restype=wintypes.BOOL
kernel32.CloseHandle.argtypes=[HANDLE];kernel32.CloseHandle.restype=wintypes.BOOL
name='CodexBackground_'+uuid.uuid4().hex
desktop=user32.CreateDesktopW(name,None,None,0,0x000F01FF,None)
if not desktop:raise ctypes.WinError(ctypes.get_last_error())
log=Path(args.log);log.parent.mkdir(parents=True,exist_ok=True)
try:
    with open(os.devnull,'rb',buffering=0) as stdin,log.open('ab',buffering=0) as output:
        handles=[msvcrt.get_osfhandle(f.fileno()) for f in (stdin,output)]
        for handle in handles:os.set_handle_inheritable(handle,True)
        startup=STARTUPINFO();startup.cb=ctypes.sizeof(STARTUPINFO);startup.lpDesktop='winsta0\\'+name
        startup.dwFlags=0x00000001|0x00000100;startup.wShowWindow=0
        startup.hStdInput=handles[0];startup.hStdOutput=handles[1];startup.hStdError=handles[1]
        info=PROCESS_INFORMATION();command=ctypes.create_unicode_buffer(subprocess.list2cmdline(args.command))
        if not kernel32.CreateProcessW(None,command,None,None,True,0x08000000|0x00004000,None,str(Path(args.cwd).resolve()),ctypes.byref(startup),ctypes.byref(info)):
            raise ctypes.WinError(ctypes.get_last_error())
        print('Isolated desktop started:',name,'PID:',info.dwProcessId,flush=True)
        try:
            kernel32.WaitForSingleObject(info.hProcess,0xffffffff);code=DWORD()
            if not kernel32.GetExitCodeProcess(info.hProcess,ctypes.byref(code)):raise ctypes.WinError(ctypes.get_last_error())
        finally:kernel32.CloseHandle(info.hThread);kernel32.CloseHandle(info.hProcess)
finally:user32.CloseDesktop(desktop)
print('Isolated command exit:',code.value,flush=True)
sys.exit(code.value)
