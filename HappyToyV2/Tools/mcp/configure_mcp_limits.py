"""Set bounded timeouts for the two locally registered editor MCP servers only."""
from pathlib import Path
import os
import re
import tomllib

config=Path(os.environ.get('CODEX_HOME',str(Path.home()/'.codex')))/'config.toml'
text=config.read_text(encoding='utf-8-sig')
before=tomllib.loads(text)
for server in ('blender','unity'):
    if server not in before.get('mcp_servers',{}):raise ValueError('Register '+server+' first')
    pattern=re.compile(r'(?m)^\[mcp_servers\.'+server+r'\]\r?\n((?:(?!^\[)[\s\S])*?)(?=^\[|\Z)')
    found=pattern.search(text)
    if not found:raise ValueError('Expected explicit MCP section for '+server)
    body=found.group(1)
    for key,value in (('startup_timeout_sec',30),('tool_timeout_sec',180)):
        setting=re.compile(r'(?m)^'+key+r'\s*=.*$')
        body=setting.sub(key+' = '+str(value),body) if setting.search(body) else body.rstrip()+'\n'+key+' = '+str(value)+'\n\n'
    text=text[:found.start(1)]+body+text[found.end(1):]
after=tomllib.loads(text)
expected=tomllib.loads(config.read_text(encoding='utf-8-sig'))
for server in ('blender','unity'):
    expected['mcp_servers'][server].update(startup_timeout_sec=30,tool_timeout_sec=180)
if after!=expected:raise ValueError('Unexpected configuration change')
config.write_text(text,encoding='utf-8',newline='\n')
print('Configured only blender/unity startup=30s and tools=180s; other settings retained')
