import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { ENEMY_CONFIGS, MAP_CONFIG } from "../src/config/gameConfig.js";

const OUT_DIR = path.dirname(fileURLToPath(import.meta.url));
const SCALE = 12;
const PADDING = 72;

const COLORS = {
  walkable: "#32483a",
  room: "#426445",
  blocked: "#843634",
  void: "#26394f",
  drop: "#b89938",
  ramp: "#6f5b37",
  wall: "#111713",
  door: "#8a5b36",
  locked: "#a22f33",
  cabinet: "#5c4a3f",
  key: "#d7ba45",
  waypoint: "#ffe17b",
  enemy: "#d15c4a",
  final: "#e36a75",
};

fs.mkdirSync(OUT_DIR, { recursive: true });

const outputs = [1, 2].map((floor) => renderFloorSvg(floor));
const overviewPath = path.join(OUT_DIR, "happy-toy-map-overview.html");
fs.writeFileSync(overviewPath, renderOverview(outputs), "utf8");

console.log(outputs.concat(overviewPath).join("\n"));

function renderFloorSvg(floor) {
  const floorAreas = (MAP_CONFIG.floorAreas || []).filter((area) => area.floor === floor);
  const rooms = (MAP_CONFIG.roomAreas || []).filter((area) => area.floor === floor);
  const blocked = (MAP_CONFIG.blockedAreas || []).filter((area) => area.floor === floor);
  const voids = (MAP_CONFIG.voidAreas || []).filter((area) => area.floor === floor);
  const drops = (MAP_CONFIG.dropZones || []).filter((area) => area.floor === floor);
  const ramps = (MAP_CONFIG.ramps || []).filter((ramp) => ramp.startFloor === floor || ramp.endFloor === floor);
  const walls = (MAP_CONFIG.walls || []).filter((wall) => objectFloorFromY(wall.position[1]) === floor);
  const doors = (MAP_CONFIG.doors || []).filter((door) => objectFloorFromY(door.position[1]) === floor);
  const cabinets = (MAP_CONFIG.cabinets || []).filter((cabinet) => objectFloorFromY(cabinet.position[1]) === floor);
  const keys = (MAP_CONFIG.keys || []).filter((key) => objectFloorFromY(key.position[1]) === floor);
  const waypoints = (MAP_CONFIG.transitionWaypoints || []).filter((waypoint) => waypoint.floor === floor);
  const enemies = ENEMY_CONFIGS.filter((enemy) => objectFloorFromY(enemy.spawn[1]) === floor);

  const floorRects = floorAreas.map((area) => areaToBox(area, getFloorY(floor)));
  const roomRects = rooms.map((area) => areaToBox(area, getFloorY(floor)));
  const blockedRects = blocked.map((area) => areaToBox(area, getFloorY(floor)));
  const voidRects = voids.map((area) => areaToBox(area, getFloorY(floor)));
  const dropRects = drops.map((area) => areaToBox(area, getFloorY(floor)));
  const rampRects = ramps.map((ramp) => ({
    id: ramp.id,
    position: [(ramp.minX + ramp.maxX) / 2, getFloorY(floor), (ramp.minZ + ramp.maxZ) / 2],
    size: [ramp.maxX - ramp.minX, 0.1, ramp.maxZ - ramp.minZ],
  }));

  const bounds = getBounds([...floorRects, ...roomRects, ...blockedRects, ...voidRects, ...dropRects, ...rampRects, ...walls, ...doors]);
  bounds.minX -= 2;
  bounds.maxX += 2;
  bounds.minZ -= 2;
  bounds.maxZ += 2;

  const width = Math.ceil((bounds.maxX - bounds.minX) * SCALE + PADDING * 2);
  const height = Math.ceil((bounds.maxZ - bounds.minZ) * SCALE + PADDING * 2);
  const xMap = (x) => (x - bounds.minX) * SCALE + PADDING;
  const zMap = (z) => (z - bounds.minZ) * SCALE + PADDING;

  const drawRect = (box, color, opacity = 1, stroke = "#000", strokeWidth = 1, dashed = false) => {
    const x = xMap(box.position[0] - box.size[0] / 2);
    const z = zMap(box.position[2] - box.size[2] / 2);
    return `<rect x="${x.toFixed(1)}" y="${z.toFixed(1)}" width="${(box.size[0] * SCALE).toFixed(1)}" height="${(box.size[2] * SCALE).toFixed(1)}" fill="${color}" opacity="${opacity}" stroke="${stroke}" stroke-width="${strokeWidth}"${dashed ? ' stroke-dasharray="5 4"' : ""}/>`;
  };
  const drawText = (text, x, z, size = 10, color = "#d8e3cf") => `<text x="${xMap(x).toFixed(1)}" y="${zMap(z).toFixed(1)}" fill="${color}" font-family="Consolas, monospace" font-size="${size}" text-anchor="middle" dominant-baseline="middle">${escapeXml(text)}</text>`;
  const drawCircle = (x, z, r, color, stroke = "#111") => `<circle cx="${xMap(x).toFixed(1)}" cy="${zMap(z).toFixed(1)}" r="${r}" fill="${color}" stroke="${stroke}" stroke-width="1"/>`;

  const svg = [];
  svg.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">`);
  svg.push('<rect width="100%" height="100%" fill="#070907"/>');
  svg.push(`<text x="24" y="32" fill="#f0e7c0" font-family="Consolas, monospace" font-size="22" font-weight="700">Happy Toy ${floor}F map</text>`);
  svg.push('<text x="24" y="55" fill="#a8b39e" font-family="Consolas, monospace" font-size="12">top-down X/Z plan from current MAP_CONFIG</text>');

  for (const area of floorRects) svg.push(drawRect(area, COLORS.walkable, 0.82, "#6d8d6d"));
  for (const area of roomRects) svg.push(drawRect(area, COLORS.room, 0.35, "#9fc99a", 1, true));
  for (const area of voidRects) svg.push(drawRect(area, COLORS.void, 0.45, "#6283a6", 1, true));
  for (const area of blockedRects) svg.push(drawRect(area, COLORS.blocked, 0.62, "#df7772", 1, true));
  for (const area of rampRects) svg.push(drawRect(area, COLORS.ramp, 0.72, "#c9a95e"));
  for (const area of dropRects) svg.push(drawRect(area, COLORS.drop, 0.72, "#ffe17b"));
  for (const wall of walls) svg.push(drawRect(wall, COLORS.wall, 1, "#293029"));
  for (const door of doors) svg.push(drawRect(door, door.locked || door.blocked ? COLORS.locked : COLORS.door, 1, "#f0c78a", 1.2));

  for (const cabinet of cabinets) {
    svg.push(drawRect({ position: cabinet.position, size: [1.18, 0.1, 0.72] }, COLORS.cabinet, 0.95, "#c0aa95"));
    svg.push(drawText("cab", cabinet.position[0], cabinet.position[2], 7, "#f2d6bd"));
  }

  for (const key of keys) {
    svg.push(drawCircle(key.position[0], key.position[2], 5, COLORS.key));
    svg.push(drawText("key", key.position[0], key.position[2] - 0.65, 8, "#ffe680"));
  }

  if (floor === 1 && MAP_CONFIG.finalExit) {
    svg.push(drawCircle(MAP_CONFIG.finalExit.position[0], MAP_CONFIG.finalExit.position[2], 6, COLORS.final));
    svg.push(drawText("final", MAP_CONFIG.finalExit.position[0], MAP_CONFIG.finalExit.position[2] - 0.85, 8, "#ffadb6"));
  }

  for (const waypoint of waypoints) {
    svg.push(drawCircle(waypoint.position[0], waypoint.position[2], 5, COLORS.waypoint, "#fff2a0"));
    svg.push(drawText(waypoint.id.replace("stair-", ""), waypoint.position[0], waypoint.position[2] + 0.85, 7, "#ffe17b"));
  }

  for (const enemy of enemies) {
    svg.push(drawCircle(enemy.spawn[0], enemy.spawn[2], 6, COLORS.enemy, "#ffd2c9"));
    svg.push(drawText(enemy.label, enemy.spawn[0], enemy.spawn[2] - 0.85, 8, "#ffc0b6"));
  }

  for (const area of floorAreas) {
    const [x, z] = areaCenter(area);
    svg.push(drawText(area.id, x, z, 7, "#bfd8bf"));
  }

  for (const door of doors) {
    const shortId = door.id.replace("door-", "").replace("upper-", "2f-");
    svg.push(drawText(shortId, door.position[0], door.position[2] + 1.0, 7, door.locked || door.blocked ? "#ff9a9a" : "#efc28e"));
  }

  svg.push(renderLegend(width));
  svg.push("</svg>");

  const filename = path.join(OUT_DIR, `happy-toy-map-${floor}f.svg`);
  fs.writeFileSync(filename, svg.join("\n"), "utf8");
  return filename;
}

function renderOverview(files) {
  return `<!doctype html>
<meta charset="utf-8">
<title>Happy Toy Map Overview</title>
<style>
body{margin:0;background:#070907;color:#e8e2c8;font-family:Consolas,monospace}
main{display:grid;gap:18px;padding:18px}
img{max-width:100%;background:#070907;border:1px solid #394036}
h1{font-size:20px;margin:0 0 4px}
</style>
<main>
<h1>Happy Toy current map overview</h1>
${files.map((file) => `<section><h2>${path.basename(file)}</h2><img src="${path.basename(file)}"></section>`).join("\n")}
</main>`;
}

function renderLegend(width) {
  const legendX = width - 245;
  const legend = [
    ["walkable", COLORS.walkable],
    ["room/event outline", COLORS.room],
    ["wall", COLORS.wall],
    ["door", COLORS.door],
    ["locked/blocked door", COLORS.locked],
    ["blocked area", COLORS.blocked],
    ["void/out-of-bounds", COLORS.void],
    ["stair/ramp/drop", COLORS.ramp],
    ["cabinet/key/enemy", COLORS.cabinet],
  ];
  const rows = legend.map(([name, color], index) => {
    const y = 34 + index * 19;
    return `<rect x="12" y="${y - 9}" width="14" height="10" fill="${color}" stroke="#222"/><text x="34" y="${y}" fill="#d8e3cf" font-family="Consolas, monospace" font-size="11">${escapeXml(name)}</text>`;
  }).join("");
  return `<g transform="translate(${legendX}, 24)"><rect x="0" y="0" width="220" height="${legend.length * 19 + 26}" fill="#0b0f0b" opacity="0.86" stroke="#657057"/><text x="12" y="18" fill="#f0e7c0" font-family="Consolas, monospace" font-size="12" font-weight="700">Legend</text>${rows}</g>`;
}

function areaCenter(area) {
  return [(area.minX + area.maxX) / 2, (area.minZ + area.maxZ) / 2];
}

function areaToBox(area, y) {
  const [x, z] = areaCenter(area);
  return { id: area.id, position: [x, y, z], size: [area.maxX - area.minX, 0.1, area.maxZ - area.minZ] };
}

function getBounds(rects) {
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  for (const rect of rects) {
    const [x, , z] = rect.position;
    const [width, , depth] = rect.size;
    minX = Math.min(minX, x - width / 2);
    maxX = Math.max(maxX, x + width / 2);
    minZ = Math.min(minZ, z - depth / 2);
    maxZ = Math.max(maxZ, z + depth / 2);
  }
  return { minX, maxX, minZ, maxZ };
}

function getFloorY(floor) {
  return floor === 2 ? 3.4 : 0;
}

function objectFloorFromY(y) {
  return y > 1.7 ? 2 : 1;
}

function escapeXml(value) {
  return String(value).replace(/[&<>"']/g, (character) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&apos;",
  })[character]);
}
