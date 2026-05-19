// Happy Toy 전용 2D top-down 맵 에디터입니다.
// 게임의 MAP_CONFIG 구조를 그대로 편집하고, 저장 시 mapConfigOverride.js로 기록합니다.

import { DEFAULT_MAP_CONFIG, MAP_CONFIG } from "../config/gameConfig.js";

const FLOOR_Y = {
  1: 0,
  2: 3.4,
};

const COLLECTIONS = [
  { key: "floorAreas", label: "Walkable Area", color: "#4ca56b", kind: "bounds" },
  { key: "roomAreas", label: "Room Area", color: "#53a8d7", kind: "bounds" },
  { key: "blockedAreas", label: "Blocked Area", color: "#d6604d", kind: "bounds" },
  { key: "voidAreas", label: "Void Area", color: "#3c3f46", kind: "bounds" },
  { key: "dropZones", label: "Drop Zone", color: "#c45d9a", kind: "bounds" },
  { key: "ramps", label: "Stair/Transition Zone", color: "#caa44a", kind: "bounds" },
  { key: "stairways", label: "Visual Stairs", color: "#9d8556", kind: "bounds" },
  { key: "floorPanels", label: "Floor Slab", color: "#55655a", kind: "panel2d" },
  { key: "ceilingPanels", label: "Ceiling Slab", color: "#394842", kind: "box3d" },
  { key: "walls", label: "Wall", color: "#8d7b5c", kind: "box3d" },
  { key: "doors", label: "Sliding Door", color: "#d08645", kind: "door" },
  { key: "keys", label: "Key", color: "#e8cf55", kind: "point" },
  { key: "cabinets", label: "Cabinet", color: "#5cae9a", kind: "point" },
  { key: "landingAreas", label: "Landing Point", color: "#8bd17a", kind: "circle" },
  { key: "transitionWaypoints", label: "AI Stair Waypoint", color: "#e6e6a8", kind: "circle" },
  { key: "props", label: "Horror Prop", color: "#b16bd1", kind: "box3d" },
];

const COLLECTION_BY_KEY = Object.fromEntries(COLLECTIONS.map((entry) => [entry.key, entry]));
const DRAW_ORDER = [
  "voidAreas",
  "floorPanels",
  "floorAreas",
  "roomAreas",
  "blockedAreas",
  "dropZones",
  "ramps",
  "stairways",
  "ceilingPanels",
  "walls",
  "doors",
  "props",
  "cabinets",
  "keys",
  "landingAreas",
  "transitionWaypoints",
];

const state = {
  mapConfig: clone(MAP_CONFIG),
  selectedCollection: "walls",
  selectedId: null,
  floorFilter: "all",
  zoom: 18,
  panX: 0,
  panY: 110,
  isDragging: false,
  isPanning: false,
  dragStartWorld: null,
  dragStartShape: null,
};

const elements = {
  collectionSelect: document.querySelector("#collection-select"),
  floorFilter: document.querySelector("#floor-filter"),
  addButton: document.querySelector("#add-button"),
  duplicateButton: document.querySelector("#duplicate-button"),
  deleteButton: document.querySelector("#delete-button"),
  resetViewButton: document.querySelector("#reset-view-button"),
  saveOverrideButton: document.querySelector("#save-override-button"),
  clearOverrideButton: document.querySelector("#clear-override-button"),
  validateButton: document.querySelector("#validate-button"),
  validationOutput: document.querySelector("#validation-output"),
  canvas: document.querySelector("#map-canvas"),
  canvasStatus: document.querySelector("#canvas-status"),
  selectionLabel: document.querySelector("#selection-label"),
  inspectorFields: document.querySelector("#inspector-fields"),
  rawJson: document.querySelector("#raw-json"),
  applyJsonButton: document.querySelector("#apply-json-button"),
  exportJson: document.querySelector("#export-json"),
};

const ctx = elements.canvas.getContext("2d");

init();

function init() {
  for (const collection of COLLECTIONS) {
    const option = document.createElement("option");
    option.value = collection.key;
    option.textContent = collection.label;
    elements.collectionSelect.append(option);
  }
  elements.collectionSelect.value = state.selectedCollection;

  wireEvents();
  resizeCanvas();
  validateMap(false);
  render();
}

function wireEvents() {
  window.addEventListener("resize", () => {
    resizeCanvas();
    render();
  });

  elements.collectionSelect.addEventListener("change", () => {
    state.selectedCollection = elements.collectionSelect.value;
    state.selectedId = null;
    render();
  });

  elements.floorFilter.addEventListener("change", () => {
    state.floorFilter = elements.floorFilter.value;
    state.selectedId = null;
    render();
  });

  elements.addButton.addEventListener("click", addItem);
  elements.duplicateButton.addEventListener("click", duplicateSelected);
  elements.deleteButton.addEventListener("click", deleteSelected);
  elements.resetViewButton.addEventListener("click", resetView);
  elements.validateButton.addEventListener("click", () => validateMap(true));
  elements.saveOverrideButton.addEventListener("click", saveOverride);
  elements.clearOverrideButton.addEventListener("click", clearOverride);
  elements.applyJsonButton.addEventListener("click", applyRawJson);

  elements.canvas.addEventListener("contextmenu", (event) => event.preventDefault());
  elements.canvas.addEventListener("mousedown", handlePointerDown);
  window.addEventListener("mousemove", handlePointerMove);
  window.addEventListener("mouseup", handlePointerUp);
  elements.canvas.addEventListener("wheel", handleWheel, { passive: false });

  window.addEventListener("keydown", (event) => {
    if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
      return;
    }
    if (event.key === "Delete" || event.key === "Backspace") {
      deleteSelected();
    }
    if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)) {
      nudgeSelected(event.key, event.shiftKey ? 1 : 0.25);
      event.preventDefault();
    }
  });
}

function resizeCanvas() {
  const rect = elements.canvas.getBoundingClientRect();
  const ratio = Math.max(1, window.devicePixelRatio || 1);
  elements.canvas.width = Math.floor(rect.width * ratio);
  elements.canvas.height = Math.floor(rect.height * ratio);
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
}

function render() {
  const width = elements.canvas.clientWidth;
  const height = elements.canvas.clientHeight;
  ctx.clearRect(0, 0, width, height);
  drawGrid(width, height);
  drawAxes(width, height);

  for (const collectionKey of DRAW_ORDER) {
    drawCollection(collectionKey);
  }

  renderInspector();
  updateExport();
  updateStatus();
}

function drawGrid(width, height) {
  const topLeft = screenToWorld(0, 0);
  const bottomRight = screenToWorld(width, height);
  const step = state.zoom >= 22 ? 1 : 2;
  const majorStep = 5;

  ctx.save();
  ctx.lineWidth = 1;
  for (let x = Math.floor(topLeft.x / step) * step; x <= bottomRight.x; x += step) {
    const a = worldToScreen(x, topLeft.z);
    const b = worldToScreen(x, bottomRight.z);
    ctx.strokeStyle = x % majorStep === 0 ? "rgba(155, 169, 143, 0.18)" : "rgba(155, 169, 143, 0.08)";
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }
  for (let z = Math.floor(topLeft.z / step) * step; z <= bottomRight.z; z += step) {
    const a = worldToScreen(topLeft.x, z);
    const b = worldToScreen(bottomRight.x, z);
    ctx.strokeStyle = z % majorStep === 0 ? "rgba(155, 169, 143, 0.18)" : "rgba(155, 169, 143, 0.08)";
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }
  ctx.restore();
}

function drawAxes(width, height) {
  const origin = worldToScreen(0, 0);
  ctx.save();
  ctx.strokeStyle = "rgba(212, 178, 74, 0.38)";
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(0, origin.y);
  ctx.lineTo(width, origin.y);
  ctx.moveTo(origin.x, 0);
  ctx.lineTo(origin.x, height);
  ctx.stroke();
  ctx.fillStyle = "rgba(238, 231, 210, 0.64)";
  ctx.fillText("X", width - 18, origin.y - 8);
  ctx.fillText("Z", origin.x + 8, height - 12);
  ctx.restore();
}

function drawCollection(collectionKey) {
  const collection = COLLECTION_BY_KEY[collectionKey];
  const items = state.mapConfig[collectionKey] || [];
  for (const item of items) {
    if (!shouldShowItem(item, collectionKey)) {
      continue;
    }
    drawItem(collection, item, collectionKey);
  }
}

function drawItem(collection, item, collectionKey) {
  const shape = getShape(collectionKey, item);
  if (!shape) {
    return;
  }
  const selected = state.selectedCollection === collectionKey && state.selectedId === item.id;
  const inActiveLayer = state.selectedCollection === collectionKey;
  const alpha = selected ? 0.88 : inActiveLayer ? 0.58 : 0.28;
  const strokeAlpha = selected ? 1 : inActiveLayer ? 0.82 : 0.42;

  ctx.save();
  ctx.fillStyle = withAlpha(collection.color, alpha);
  ctx.strokeStyle = withAlpha(selected ? "#fff1b8" : collection.color, strokeAlpha);
  ctx.lineWidth = selected ? 3 : inActiveLayer ? 1.7 : 1;

  if (shape.kind === "circle" || shape.kind === "point") {
    const p = worldToScreen(shape.x, shape.z);
    const radius = Math.max(5, shape.radius * state.zoom);
    ctx.beginPath();
    ctx.arc(p.x, p.y, radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  } else {
    const p = worldToScreen(shape.x, shape.z);
    const w = Math.max(2, shape.width * state.zoom);
    const d = Math.max(2, shape.depth * state.zoom);
    ctx.beginPath();
    ctx.rect(p.x - w / 2, p.y - d / 2, w, d);
    ctx.fill();
    ctx.stroke();
  }

  if (selected || inActiveLayer) {
    const p = worldToScreen(shape.x, shape.z);
    ctx.fillStyle = "rgba(255, 249, 232, 0.86)";
    ctx.font = "12px Segoe UI, sans-serif";
    ctx.fillText(item.id || "(no id)", p.x + 6, p.y - 6);
  }
  ctx.restore();
}

function handlePointerDown(event) {
  if (event.button === 1 || event.button === 2) {
    state.isPanning = true;
    state.dragStartWorld = { x: event.clientX, z: event.clientY };
    return;
  }

  const point = canvasPoint(event);
  const world = screenToWorld(point.x, point.y);
  const hit = findHit(world);
  if (hit) {
    state.selectedCollection = hit.collectionKey;
    elements.collectionSelect.value = hit.collectionKey;
    state.selectedId = hit.item.id;
    state.isDragging = true;
    state.dragStartWorld = world;
    state.dragStartShape = getShape(hit.collectionKey, hit.item);
  } else {
    state.selectedId = null;
  }
  render();
}

function handlePointerMove(event) {
  if (state.isPanning) {
    state.panX += event.movementX;
    state.panY += event.movementY;
    render();
    return;
  }

  const point = canvasPoint(event);
  const world = screenToWorld(point.x, point.y);
  if (state.isDragging) {
    const item = getSelectedItem();
    if (!item || !state.dragStartShape) {
      return;
    }
    const dx = world.x - state.dragStartWorld.x;
    const dz = world.z - state.dragStartWorld.z;
    applyShape(state.selectedCollection, item, {
      ...state.dragStartShape,
      x: snap(state.dragStartShape.x + dx),
      z: snap(state.dragStartShape.z + dz),
    });
    render();
  } else {
    const hit = findHit(world);
    elements.canvas.style.cursor = hit ? "move" : "crosshair";
  }
}

function handlePointerUp() {
  state.isDragging = false;
  state.isPanning = false;
  state.dragStartWorld = null;
  state.dragStartShape = null;
}

function handleWheel(event) {
  event.preventDefault();
  const point = canvasPoint(event);
  const before = screenToWorld(point.x, point.y);
  const factor = event.deltaY > 0 ? 0.9 : 1.1;
  state.zoom = clamp(state.zoom * factor, 5, 80);
  const after = screenToWorld(point.x, point.y);
  state.panX += (after.x - before.x) * state.zoom;
  state.panY += (after.z - before.z) * state.zoom;
  render();
}

function findHit(world) {
  const ordered = [...DRAW_ORDER].reverse();
  ordered.sort((a, b) => (a === state.selectedCollection ? -1 : b === state.selectedCollection ? 1 : 0));
  for (const collectionKey of ordered) {
    const items = state.mapConfig[collectionKey] || [];
    for (let index = items.length - 1; index >= 0; index -= 1) {
      const item = items[index];
      if (!shouldShowItem(item, collectionKey)) {
        continue;
      }
      const shape = getShape(collectionKey, item);
      if (shape && pointInShape(world, shape)) {
        return { collectionKey, item };
      }
    }
  }
  return null;
}

function pointInShape(point, shape) {
  if (shape.kind === "circle" || shape.kind === "point") {
    const radius = Math.max(shape.radius, 0.45);
    return Math.hypot(point.x - shape.x, point.z - shape.z) <= radius;
  }
  const halfWidth = Math.max(shape.width / 2, 0.28);
  const halfDepth = Math.max(shape.depth / 2, 0.28);
  return Math.abs(point.x - shape.x) <= halfWidth && Math.abs(point.z - shape.z) <= halfDepth;
}

function addItem() {
  ensureCollection(state.selectedCollection);
  const point = screenToWorld(elements.canvas.clientWidth / 2, elements.canvas.clientHeight / 2);
  const item = makeDefaultItem(state.selectedCollection, point);
  state.mapConfig[state.selectedCollection].push(item);
  state.selectedId = item.id;
  render();
}

function duplicateSelected() {
  const item = getSelectedItem();
  if (!item) {
    return;
  }
  const copy = clone(item);
  copy.id = nextId(`${item.id || state.selectedCollection}-copy`);
  const shape = getShape(state.selectedCollection, copy);
  if (shape) {
    applyShape(state.selectedCollection, copy, { ...shape, x: shape.x + 1, z: shape.z + 1 });
  }
  state.mapConfig[state.selectedCollection].push(copy);
  state.selectedId = copy.id;
  render();
}

function deleteSelected() {
  if (!state.selectedId) {
    return;
  }
  const items = state.mapConfig[state.selectedCollection] || [];
  const index = items.findIndex((item) => item.id === state.selectedId);
  if (index >= 0) {
    items.splice(index, 1);
    state.selectedId = null;
    render();
  }
}

function nudgeSelected(key, amount) {
  const item = getSelectedItem();
  if (!item) {
    return;
  }
  const shape = getShape(state.selectedCollection, item);
  if (!shape) {
    return;
  }
  const dx = key === "ArrowRight" ? amount : key === "ArrowLeft" ? -amount : 0;
  const dz = key === "ArrowDown" ? amount : key === "ArrowUp" ? -amount : 0;
  applyShape(state.selectedCollection, item, { ...shape, x: snap(shape.x + dx), z: snap(shape.z + dz) });
  render();
}

function renderInspector() {
  const item = getSelectedItem();
  elements.inspectorFields.textContent = "";
  if (!item) {
    elements.selectionLabel.textContent = "선택 없음";
    elements.rawJson.value = "";
    return;
  }

  elements.selectionLabel.textContent = `${COLLECTION_BY_KEY[state.selectedCollection].label} / ${item.id}`;
  const shape = getShape(state.selectedCollection, item);
  const fields = buildFields(item, shape);
  for (const field of fields) {
    elements.inspectorFields.append(createField(field));
  }
  elements.rawJson.value = JSON.stringify(item, null, 2);
}

function buildFields(item, shape) {
  const fields = [
    { key: "id", label: "id", value: item.id || "", type: "text", full: true },
  ];
  if ("label" in item || state.selectedCollection === "doors" || state.selectedCollection === "keys") {
    fields.push({ key: "label", label: "label", value: item.label || "", type: "text", full: true });
  }
  if ("type" in item || ["props", "floorAreas", "roomAreas", "blockedAreas", "voidAreas"].includes(state.selectedCollection)) {
    fields.push({ key: "type", label: "type", value: item.type || "", type: "text", full: true });
  }
  fields.push({ key: "floor", label: "floor", value: getFloor(state.selectedCollection, item) || currentFloor(), type: "number" });
  if (shape) {
    fields.push({ key: "x", label: "x", value: shape.x, type: "number" });
    fields.push({ key: "z", label: "z", value: shape.z, type: "number" });
    fields.push({ key: "width", label: "width", value: shape.width, type: "number" });
    fields.push({ key: "depth", label: "depth", value: shape.depth, type: "number" });
    fields.push({ key: "y", label: "y", value: getY(state.selectedCollection, item), type: "number" });
    fields.push({ key: "height", label: "height", value: shape.height ?? getHeight(state.selectedCollection, item), type: "number" });
  }
  if ("connectedRoomId" in item || state.selectedCollection === "doors") {
    fields.push({ key: "connectedRoomId", label: "connectedRoomId", value: item.connectedRoomId || "", type: "text", full: true });
  }
  if ("locked" in item || state.selectedCollection === "doors") {
    fields.push({ key: "locked", label: "locked", value: Boolean(item.locked), type: "checkbox" });
    fields.push({ key: "blocked", label: "blocked", value: Boolean(item.blocked), type: "checkbox" });
  }
  if ("collision" in item || state.selectedCollection === "props") {
    fields.push({ key: "collision", label: "collision", value: Boolean(item.collision), type: "checkbox" });
  }
  if ("yaw" in item || "rotation" in item) {
    fields.push({ key: "yaw", label: "yaw", value: getYaw(item), type: "number" });
  }
  if ("colorIndex" in item || state.selectedCollection === "props") {
    fields.push({ key: "colorIndex", label: "colorIndex", value: item.colorIndex ?? 0, type: "number" });
  }
  return fields;
}

function createField(field) {
  const label = document.createElement("label");
  if (field.full) {
    label.classList.add("full");
  }
  label.textContent = field.label;
  const input = document.createElement("input");
  input.type = field.type === "checkbox" ? "checkbox" : field.type;
  if (field.type === "checkbox") {
    input.checked = Boolean(field.value);
  } else {
    input.value = formatNumber(field.value);
    if (field.type === "number") {
      input.step = "0.05";
    }
  }
  input.addEventListener("change", () => {
    updateSelectedField(field.key, field.type === "checkbox" ? input.checked : input.value);
  });
  label.append(input);
  return label;
}

function updateSelectedField(key, rawValue) {
  const item = getSelectedItem();
  if (!item) {
    return;
  }
  const numericKeys = new Set(["floor", "x", "z", "width", "depth", "y", "height", "yaw", "colorIndex"]);
  const value = numericKeys.has(key) ? Number(rawValue) : rawValue;
  const shape = getShape(state.selectedCollection, item);

  if (["id", "label", "type", "connectedRoomId", "locked", "blocked", "collision", "colorIndex"].includes(key)) {
    item[key] = value;
  } else if (key === "floor") {
    setFloor(state.selectedCollection, item, value);
  } else if (key === "yaw") {
    setYaw(item, value);
  } else if (shape && ["x", "z", "width", "depth", "height"].includes(key)) {
    applyShape(state.selectedCollection, item, { ...shape, [key]: value });
  } else if (key === "y") {
    setY(state.selectedCollection, item, value);
  }
  render();
}

function applyRawJson() {
  const item = getSelectedItem();
  if (!item) {
    return;
  }
  try {
    const parsed = JSON.parse(elements.rawJson.value);
    for (const key of Object.keys(item)) {
      delete item[key];
    }
    Object.assign(item, parsed);
    state.selectedId = item.id;
    render();
    setStatus("Raw JSON을 적용했습니다.");
  } catch (error) {
    setStatus(`JSON 오류: ${error.message}`);
  }
}

async function saveOverride() {
  try {
    const response = await fetch("/api/editor/map-override", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ mapConfig: state.mapConfig }),
    });
    const result = await response.json();
    if (!response.ok) {
      throw new Error(result.error || "저장 실패");
    }
    setStatus(`저장 완료: ${result.path}`);
  } catch (error) {
    setStatus(`저장 실패: ${error.message}`);
  }
}

async function clearOverride() {
  try {
    const response = await fetch("/api/editor/map-override", { method: "DELETE" });
    const result = await response.json();
    if (!response.ok) {
      throw new Error(result.error || "해제 실패");
    }
    state.mapConfig = clone(DEFAULT_MAP_CONFIG);
    state.selectedId = null;
    render();
    setStatus("저장 맵을 해제하고 기본 맵을 불러왔습니다.");
  } catch (error) {
    setStatus(`해제 실패: ${error.message}`);
  }
}

function validateMap(showStatus) {
  const messages = [];
  for (const collection of COLLECTIONS) {
    const ids = new Set();
    for (const item of state.mapConfig[collection.key] || []) {
      if (!item.id) {
        messages.push(`[${collection.key}] id가 없는 항목이 있습니다.`);
        continue;
      }
      if (ids.has(item.id)) {
        messages.push(`[${collection.key}] 중복 id: ${item.id}`);
      }
      ids.add(item.id);
    }
  }

  const roomIds = new Set((state.mapConfig.roomAreas || []).map((room) => room.id));
  for (const door of state.mapConfig.doors || []) {
    if (!door.locked && !door.blocked && !door.connectedRoomId) {
      messages.push(`문 ${door.id}: 열리는 문인데 connectedRoomId가 없습니다.`);
    }
    if (door.connectedRoomId && !roomIds.has(door.connectedRoomId)) {
      messages.push(`문 ${door.id}: 연결된 방 ${door.connectedRoomId}를 찾을 수 없습니다.`);
    }
  }

  const waypointIds = new Set((state.mapConfig.transitionWaypoints || []).map((waypoint) => waypoint.id));
  for (const waypoint of state.mapConfig.transitionWaypoints || []) {
    for (const link of waypoint.links || []) {
      if (!waypointIds.has(link)) {
        messages.push(`waypoint ${waypoint.id}: link ${link}가 존재하지 않습니다.`);
      }
    }
  }

  const panelOverlaps = findFloorPanelOverlaps();
  for (const overlap of panelOverlaps) {
    messages.push(`2층/바닥 패널 겹침: ${overlap.a} <-> ${overlap.b}`);
  }

  elements.validationOutput.classList.toggle("ok", messages.length === 0);
  elements.validationOutput.classList.toggle("warn", messages.length > 0);
  elements.validationOutput.textContent = messages.length === 0
    ? "검증 통과: 중복 id, 열린 문 연결 누락, waypoint link 누락, 바닥 패널 겹침이 없습니다."
    : messages.join("\n");

  if (showStatus) {
    setStatus(messages.length === 0 ? "맵 검증 통과" : `맵 검증 경고 ${messages.length}개`);
  }
}

function findFloorPanelOverlaps() {
  const panels = state.mapConfig.floorPanels || [];
  const overlaps = [];
  for (let i = 0; i < panels.length; i += 1) {
    for (let j = i + 1; j < panels.length; j += 1) {
      const a = getShape("floorPanels", panels[i]);
      const b = getShape("floorPanels", panels[j]);
      if (!a || !b || Math.abs((panels[i].y ?? 0) - (panels[j].y ?? 0)) > 0.001) {
        continue;
      }
      const ix = Math.min(a.x + a.width / 2, b.x + b.width / 2) - Math.max(a.x - a.width / 2, b.x - b.width / 2);
      const iz = Math.min(a.z + a.depth / 2, b.z + b.depth / 2) - Math.max(a.z - a.depth / 2, b.z - b.depth / 2);
      if (ix > 0.001 && iz > 0.001) {
        overlaps.push({ a: panels[i].id, b: panels[j].id });
      }
    }
  }
  return overlaps;
}

function getShape(collectionKey, item) {
  const collection = COLLECTION_BY_KEY[collectionKey];
  if (!item || !collection) {
    return null;
  }
  if (["bounds"].includes(collection.kind)) {
    const minX = item.minX ?? -1;
    const maxX = item.maxX ?? 1;
    const minZ = item.minZ ?? -1;
    const maxZ = item.maxZ ?? 1;
    return { kind: "rect", x: (minX + maxX) / 2, z: (minZ + maxZ) / 2, width: maxX - minX, depth: maxZ - minZ, height: 0 };
  }
  if (collectionKey === "floorPanels") {
    return { kind: "rect", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, width: item.size?.[0] ?? 2, depth: item.size?.[1] ?? 2, height: item.slabThickness ?? 0 };
  }
  if (["walls", "ceilingPanels", "props"].includes(collectionKey)) {
    return { kind: "rect", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, width: item.size?.[0] ?? 1, depth: item.size?.[2] ?? 1, height: item.size?.[1] ?? 1 };
  }
  if (collectionKey === "doors") {
    return { kind: "rect", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, width: Math.max(item.size?.[0] ?? 0.22, 0.45), depth: Math.max(item.size?.[2] ?? 0.22, 0.45), height: item.size?.[1] ?? 2.35 };
  }
  if (collectionKey === "landingAreas") {
    return { kind: "circle", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, radius: item.radius ?? 0.8, width: (item.radius ?? 0.8) * 2, depth: (item.radius ?? 0.8) * 2, height: 0 };
  }
  if (collectionKey === "transitionWaypoints") {
    return { kind: "circle", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, radius: 0.45, width: 0.9, depth: 0.9, height: 0 };
  }
  return { kind: "point", x: item.position?.[0] ?? 0, z: item.position?.[2] ?? 0, radius: 0.45, width: 0.9, depth: 0.9, height: 0 };
}

function applyShape(collectionKey, item, shape) {
  const collection = COLLECTION_BY_KEY[collectionKey];
  if (collection.kind === "bounds") {
    item.minX = round(shape.x - Math.max(0.05, shape.width) / 2);
    item.maxX = round(shape.x + Math.max(0.05, shape.width) / 2);
    item.minZ = round(shape.z - Math.max(0.05, shape.depth) / 2);
    item.maxZ = round(shape.z + Math.max(0.05, shape.depth) / 2);
    return;
  }
  if (collectionKey === "floorPanels") {
    item.position = [round(shape.x), item.y ?? 0, round(shape.z)];
    item.size = [round(Math.max(0.05, shape.width)), round(Math.max(0.05, shape.depth))];
    if (Number.isFinite(shape.height) && shape.height > 0) {
      item.slabThickness = round(shape.height);
    }
    return;
  }
  if (["walls", "ceilingPanels", "props"].includes(collectionKey)) {
    item.position = [round(shape.x), item.position?.[1] ?? getY(collectionKey, item), round(shape.z)];
    item.size = [round(Math.max(0.05, shape.width)), round(Math.max(0.05, shape.height ?? 1)), round(Math.max(0.05, shape.depth))];
    return;
  }
  if (collectionKey === "doors") {
    item.position = [round(shape.x), item.position?.[1] ?? getY(collectionKey, item), round(shape.z)];
    const current = item.size || [0.22, 2.35, 1.72];
    const xThin = current[0] <= current[2];
    item.size = xThin
      ? [round(Math.max(0.08, shape.width)), round(Math.max(0.5, shape.height ?? current[1])), round(Math.max(0.08, shape.depth))]
      : [round(Math.max(0.08, shape.width)), round(Math.max(0.5, shape.height ?? current[1])), round(Math.max(0.08, shape.depth))];
    return;
  }
  if (collectionKey === "landingAreas") {
    item.position = [round(shape.x), item.position?.[1] ?? getY(collectionKey, item), round(shape.z)];
    item.radius = round(Math.max(0.1, shape.width / 2));
    return;
  }
  if (collectionKey === "transitionWaypoints" || ["keys", "cabinets"].includes(collectionKey)) {
    item.position = [round(shape.x), item.position?.[1] ?? getY(collectionKey, item), round(shape.z)];
  }
}

function makeDefaultItem(collectionKey, point) {
  const floor = currentFloor();
  const y = FLOOR_Y[floor] ?? 0;
  const id = nextId(collectionKey.replace(/Areas$/, "").replace(/s$/, ""));
  if (["floorAreas", "roomAreas", "blockedAreas", "voidAreas"].includes(collectionKey)) {
    const type = collectionKey === "floorAreas" ? "walkable" : collectionKey === "roomAreas" ? "room" : collectionKey === "blockedAreas" ? "blocked" : "void/out-of-bounds";
    return { id, floor, type, y, minX: round(point.x - 1.5), maxX: round(point.x + 1.5), minZ: round(point.z - 1.5), maxZ: round(point.z + 1.5) };
  }
  if (collectionKey === "dropZones") {
    return { id, type: "dropZone", floor, minX: round(point.x - 0.7), maxX: round(point.x + 0.7), minZ: round(point.z - 0.7), maxZ: round(point.z + 0.7), targetFloor: floor === 1 ? 2 : 1, targetLandingId: "" };
  }
  if (collectionKey === "ramps") {
    return { id, type: "transitionZone", startFloor: 1, endFloor: 2, axis: "z", minX: round(point.x - 1), maxX: round(point.x + 1), minZ: round(point.z - 3), maxZ: round(point.z + 3), startZ: round(point.z + 3), endZ: round(point.z - 3), startY: 0, endY: 3.4 };
  }
  if (collectionKey === "stairways") {
    return { id, minX: round(point.x - 1.2), maxX: round(point.x + 1.2), minZ: round(point.z - 4), maxZ: round(point.z + 4), startY: 0, endY: 3.4, steps: 16, railMode: "wall-handrail" };
  }
  if (collectionKey === "floorPanels") {
    return { id, y, position: [round(point.x), y, round(point.z)], size: [4, 4], color: 0x171d19, slabThickness: floor === 2 ? 0.42 : undefined };
  }
  if (collectionKey === "ceilingPanels") {
    return { id, y: y + 3.06, position: [round(point.x), y + 3.06, round(point.z)], size: [4, 0.24, 4], color: 0x0d110d };
  }
  if (collectionKey === "walls") {
    return { id, position: [round(point.x), y + 1.5, round(point.z)], size: [0.4, 3, 4] };
  }
  if (collectionKey === "doors") {
    return { id, label: "새 문", position: [round(point.x), y, round(point.z)], size: [0.22, 2.35, 1.72], openDirection: 1, connectedRoomId: "" };
  }
  if (collectionKey === "keys") {
    return { id, label: "새 열쇠", position: [round(point.x), y, round(point.z)] };
  }
  if (collectionKey === "cabinets") {
    return { id, label: "새 캐비넷", position: [round(point.x), y, round(point.z)], yaw: 0 };
  }
  if (collectionKey === "landingAreas") {
    return { id, floor, position: [round(point.x), y, round(point.z)], radius: 0.8 };
  }
  if (collectionKey === "transitionWaypoints") {
    return { id, floor, type: "stairWaypoint", position: [round(point.x), y, round(point.z)], links: [] };
  }
  return { id, type: "barricade", position: [round(point.x), y, round(point.z)], size: [1.2, 1, 0.8], colorIndex: 3, collision: false };
}

function shouldShowItem(item, collectionKey) {
  if (state.floorFilter === "all") {
    return true;
  }
  const floor = getFloor(collectionKey, item);
  return !floor || String(floor) === state.floorFilter;
}

function getSelectedItem() {
  return (state.mapConfig[state.selectedCollection] || []).find((item) => item.id === state.selectedId) || null;
}

function getFloor(collectionKey, item) {
  if (Number.isFinite(item.floor)) return item.floor;
  if (Number.isFinite(item.startFloor)) return item.startFloor;
  const y = getY(collectionKey, item);
  return y >= 2 ? 2 : 1;
}

function setFloor(collectionKey, item, floor) {
  if (Number.isFinite(item.floor)) {
    item.floor = floor;
  }
  const y = FLOOR_Y[floor] ?? 0;
  if (collectionKey === "floorPanels") {
    item.y = y;
    item.position[1] = y;
  } else if (item.position) {
    if (collectionKey === "walls") {
      item.position[1] = y + (item.size?.[1] ?? 3) / 2;
    } else {
      item.position[1] = y;
    }
  }
  if (Number.isFinite(item.y)) {
    item.y = y;
  }
}

function getY(collectionKey, item) {
  if (collectionKey === "floorPanels") return item.y ?? item.position?.[1] ?? 0;
  if (Number.isFinite(item.y)) return item.y;
  if (item.position) return item.position[1] ?? 0;
  if (Number.isFinite(item.startY)) return item.startY;
  return FLOOR_Y[getFloor(collectionKey, item)] ?? 0;
}

function setY(collectionKey, item, y) {
  if (collectionKey === "floorPanels") {
    item.y = y;
    item.position[1] = y;
    return;
  }
  if (item.position) {
    item.position[1] = y;
  }
  if (Number.isFinite(item.y)) {
    item.y = y;
  }
}

function getHeight(collectionKey, item) {
  if (collectionKey === "floorPanels") return item.slabThickness ?? 0;
  if (item.size?.length >= 2) return item.size[1];
  return 0;
}

function getYaw(item) {
  if (Number.isFinite(item.yaw)) return item.yaw;
  return item.rotation?.[1] ?? 0;
}

function setYaw(item, value) {
  if (Number.isFinite(item.yaw)) {
    item.yaw = value;
  } else {
    item.rotation = item.rotation || [0, 0, 0];
    item.rotation[1] = value;
  }
}

function currentFloor() {
  return state.floorFilter === "2" ? 2 : 1;
}

function ensureCollection(key) {
  if (!Array.isArray(state.mapConfig[key])) {
    state.mapConfig[key] = [];
  }
}

function nextId(prefix) {
  const allIds = new Set();
  for (const collection of COLLECTIONS) {
    for (const item of state.mapConfig[collection.key] || []) {
      allIds.add(item.id);
    }
  }
  let index = 1;
  let id = `${prefix}-${index}`;
  while (allIds.has(id)) {
    index += 1;
    id = `${prefix}-${index}`;
  }
  return id;
}

function resetView() {
  state.zoom = 18;
  state.panX = 0;
  state.panY = 110;
  render();
}

function updateExport() {
  elements.exportJson.value = `export const MAP_CONFIG_OVERRIDE = ${JSON.stringify(state.mapConfig, null, 2)};\n`;
}

function updateStatus() {
  const selected = getSelectedItem();
  const itemText = selected ? `${state.selectedCollection}:${selected.id}` : "선택 없음";
  elements.canvasStatus.textContent = `layer=${COLLECTION_BY_KEY[state.selectedCollection].label} floor=${state.floorFilter} zoom=${state.zoom.toFixed(1)} ${itemText}`;
}

function setStatus(text) {
  elements.canvasStatus.textContent = text;
}

function worldToScreen(x, z) {
  return {
    x: elements.canvas.clientWidth / 2 + state.panX + x * state.zoom,
    y: elements.canvas.clientHeight / 2 + state.panY + z * state.zoom,
  };
}

function screenToWorld(x, y) {
  return {
    x: (x - elements.canvas.clientWidth / 2 - state.panX) / state.zoom,
    z: (y - elements.canvas.clientHeight / 2 - state.panY) / state.zoom,
  };
}

function canvasPoint(event) {
  const rect = elements.canvas.getBoundingClientRect();
  return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function withAlpha(hex, alpha) {
  const value = hex.replace("#", "");
  const r = parseInt(value.slice(0, 2), 16);
  const g = parseInt(value.slice(2, 4), 16);
  const b = parseInt(value.slice(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function formatNumber(value) {
  if (typeof value === "number") {
    return Number.isInteger(value) ? String(value) : String(round(value));
  }
  return value ?? "";
}

function snap(value) {
  return round(Math.round(value * 10) / 10);
}

function round(value) {
  return Number(Number(value).toFixed(3));
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}
