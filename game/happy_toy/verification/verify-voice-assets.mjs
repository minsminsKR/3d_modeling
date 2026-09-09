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
  "library", "washroom", "boarded", "throughClass", "gym",
  "windowHall", "skybridge", "courtyard", "memorial", "auditorium", "foyer", "atrium",
  "trophy", "arcade", "art", "practice", "studio", "broadcast",
  "darkroom", "greenroom", "homeec", "club",
  "specimen", "stagewing", "laundry", "lablink",
  "av", "supply", "counsel", "staticset", "stairhall", "nurseryhall", "dollhall",
  "archivehall", "storagehall", "teahall",
  "dorm", "dollclass", "prepstore", "closedlib", "etiquette", "roofhall", "lostfound",
  "classwing", "uncathall", "northhall", "b1boiler", "f2bloodhall", "annexgate",
  "b1floodclass", "f2gallery",
];

assert.equal(fs.existsSync(root), true, `voice directory missing: ${root}`);
for (const key of required) {
  const file = path.join(root, `${key}.ogg`);
  const stat = fs.statSync(file);
  assert.ok(stat.size > 4000, `${key}.ogg too small (${stat.size})`);
}

const arg = process.argv[2] || "http://127.0.0.1:8010/";
const servedUrl = String(arg).includes(".ogg")
  ? arg
  : new URL("assets/voice/skybridge.ogg", arg.endsWith("/") ? arg : `${arg}/`).href;
const served = await fetch(servedUrl);
assert.equal(served.ok, true, `voice clip not served: ${served.status} ${servedUrl}`);
const type = String(served.headers.get("content-type") || "");
assert.ok(type.includes("ogg") || type.includes("audio"), `unexpected voice mime: ${type}`);
const bytes = Buffer.from(await served.arrayBuffer());
assert.ok(bytes.length > 4000, `served voice clip too small: ${bytes.length}`);

console.log(`PASS: ${required.length} Korean PA clips on disk and ${servedUrl} served as ${type}`);
