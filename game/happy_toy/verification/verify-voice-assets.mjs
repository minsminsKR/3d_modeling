import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../assets/voice");
const required = [
  "start", "hunt", "pa", "f1b", "b1", "f2", "nurse", "music", "faculty", "science",
  "key", "key2", "key3", "keysDone", "ritual", "ritualFail", "death", "clear",
  "hide", "stairWait", "leaveStart", "nursery", "shrine", "stairB1", "stairF2",
  "b1deep", "f2deep", "b1east", "f2south", "f1maze", "f1ring",
  "library", "washroom", "boarded",
];

assert.equal(fs.existsSync(root), true, `voice directory missing: ${root}`);
for (const key of required) {
  const file = path.join(root, `${key}.ogg`);
  const stat = fs.statSync(file);
  assert.ok(stat.size > 4000, `${key}.ogg too small (${stat.size})`);
}

const served = await fetch(process.argv[2] || "http://127.0.0.1:8010/assets/voice/f2south.ogg");
assert.equal(served.ok, true, `voice clip not served: ${served.status}`);
const type = String(served.headers.get("content-type") || "");
assert.ok(type.includes("ogg") || type.includes("audio"), `unexpected voice mime: ${type}`);
const bytes = Buffer.from(await served.arrayBuffer());
assert.ok(bytes.length > 4000, `served voice clip too small: ${bytes.length}`);

console.log(`PASS: ${required.length} Korean PA clips on disk and /assets/voice/f2south.ogg served as ${type}`);
