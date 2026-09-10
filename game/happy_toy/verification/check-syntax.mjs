import {readdir} from 'node:fs/promises';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const files=[];
async function walk(dir) {for(const entry of await readdir(dir,{withFileTypes:true})){const url=new URL(entry.name+(entry.isDirectory()?'/':''),dir);if(entry.isDirectory())await walk(url);else if(entry.name.endsWith('.js'))files.push(fileURLToPath(url));}}
await walk(new URL('../src/',import.meta.url));
for(const file of files){const result=spawnSync(process.execPath,['--check',file],{encoding:'utf8'});if(result.status!==0){console.error(result.stderr);process.exit(1);}}
console.log(`Syntax passed: ${files.length} game modules`);
