const element = {
  style: {},
  textContent: "",
  classList: { toggle() {}, add() {}, remove() {} },
  addEventListener() {},
  checked: false,
  setAttribute() {},
};
globalThis.document = {
  querySelector: () => element,
  querySelectorAll: () => [],
  documentElement: { style: { setProperty() {} } },
  body: { classList: { toggle() {} }, dataset: {} },
};
globalThis.window = {
  matchMedia: () => ({ matches: false }),
  setTimeout,
  clearTimeout,
};

const { Hud } = await import("../src/ui/Hud.js");
const hud = new Hud();
if (typeof hud.setFloorDebug !== "function") {
  throw new Error("Hud.setFloorDebug missing");
}
hud.setDebugEnabled(true);
hud.setFloorDebug({
  floor: 1,
  x: 1.25,
  z: -4.5,
  tileType: "walkable",
  tileId: "start",
  belowValidLanding: false,
  belowFloor: null,
  belowTileType: "none",
  lastDropAttempt: null,
  areaCounts: { walkable: 1, room: 0, blocked: 0, void: 0, stair: 0, door: 0 },
  transitionWaypoints: [],
  nearestDoor: null,
  monsters: [],
  testSafeMode: false,
});
if (!String(element.textContent).includes("floor: 1")) {
  throw new Error(`debug text not applied: ${element.textContent}`);
}
console.log("hud setFloorDebug ok");
