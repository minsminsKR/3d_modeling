"""Serve an exported Unity lighting review locally; Python 3.11 standard library."""
import argparse
import functools
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
import json
import os
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--directory',required=True,type=Path)
parser.add_argument('--port',type=int,default=0)
args=parser.parse_args()
root=args.directory.resolve()
if not (root/'manifest.json').is_file():parser.error('Export manifest.json first')
viewer=Path(__file__).with_name('index.html')

class Handler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path.split('?')[0] in ('/','/index.html'):
            body=viewer.read_bytes()
            self.send_response(200);self.send_header('Content-Type','text/html; charset=utf-8')
            self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        else:super().do_GET()
    def end_headers(self):
        self.send_header('Cache-Control','no-cache');super().end_headers()
    def log_message(self,*_args):pass

server=ThreadingHTTPServer(('127.0.0.1',args.port),functools.partial(Handler,directory=str(root)))
state=dict(pid=os.getpid(),url='http://127.0.0.1:'+str(server.server_port)+'/',directory=str(root))
(root/'viewer-server.json').write_text(json.dumps(state,indent=2),encoding='utf-8')
print(json.dumps(state),flush=True)
try:server.serve_forever()
except KeyboardInterrupt:pass
finally:server.server_close()
