"""Stop this isolated job's process group if its web-worker parent dies."""
import ctypes
import os
import signal

def install_process_guard():
    leader = os.getpid()
    parent = os.getppid()
    def stop(signum, frame):
        signal.signal(signal.SIGTERM,signal.SIG_DFL)
        # A forked dataloader worker must only terminate itself.
        if os.getpid()==leader and os.getpgrp()==leader:
            os.killpg(leader,signal.SIGTERM)
        else:
            os.kill(os.getpid(),signal.SIGTERM)
    signal.signal(signal.SIGTERM,stop)
    libc=ctypes.CDLL(None,use_errno=True)
    if libc.prctl(1,signal.SIGTERM,0,0,0)!=0:
        raise OSError(ctypes.get_errno(),'Cannot register job parent-death guard')
    if parent==1 or os.getppid()!=parent: stop(signal.SIGTERM,None)
