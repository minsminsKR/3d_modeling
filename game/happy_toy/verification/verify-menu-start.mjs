const listeners = [];
const element = (id) => {
  const el = {
    id,
    style: { display: "", pointerEvents: "auto", setProperty() {} },
    innerHTML: "",
    textContent: "",
    disabled: false,
    innerText: "",
    classList: { add() {}, remove() {}, toggle() {} },
    addEventListener(type, fn) { listeners.push({ id, type, fn }); },
    querySelector: (sel) => (String(sel).includes("menu-overlay") ? overlay : null),
    closest: (sel) => (String(sel).includes("button") ? { id } : null),
  };
  return el;
};
const overlay = element("overlay");
const nodes = {
  "btn-start-game": element("btn-start-game"),
  "menu-status": element("menu-status"),
  "diff-label": element("diff-label"),
  "btn-difficulty": element("btn-difficulty"),
  "btn-settings": element("btn-settings"),
  "btn-records": element("btn-records"),
};
globalThis.window = {
  addEventListener() {},
  AudioContext: class { constructor() { throw new Error("no audio"); } },
};
globalThis.document = {
  body: { appendChild() {} },
  createElement: () => {
    const el = element("menu-system-root");
    el.querySelector = (sel) => (String(sel).includes("menu-overlay") ? overlay : nodes["btn-start-game"]);
    return el;
  },
  getElementById: (id) => nodes[id] || null,
};

const { MenuSystem } = await import("../src/ui/MenuSystem.js");

const game = {
  assetsReady: false,
  isStarted: false,
  isPaused: false,
  start() { this.isStarted = true; },
  input: { requestPointerLock() { this.locked = true; } },
};

const menu = new MenuSystem(game);
if (nodes["btn-start-game"].textContent !== "불러오는 중...") {
  throw new Error("start button should show loading label");
}
if (!nodes["menu-status"].textContent.includes("불러오는")) {
  throw new Error(`expected loading status, got ${nodes["menu-status"].textContent}`);
}

const queued = menu.tryStart();
if (queued !== false || game.isStarted) {
  throw new Error("tryStart should queue while assets are loading");
}
if (!menu.pendingStart) {
  throw new Error("pendingStart should be set");
}

game.assetsReady = true;
menu.setAssetsReady(true);
if (!game.isStarted) {
  throw new Error("queued start should fire when assets become ready");
}
if (menu.container.style.display !== "none") {
  throw new Error("title should hide after start");
}

console.log("menu start queue ok");
