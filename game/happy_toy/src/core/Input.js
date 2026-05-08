// 키보드와 마우스 입력을 한 곳에서 수집하는 모듈입니다.
// event.key와 event.code를 같이 저장해 한글 입력 상태에서도 WASD/E/F 물리키가 동작하게 합니다.

export class Input {
  constructor(targetElement) {
    this.targetElement = targetElement;
    this.keys = new Set();
    this.pressedThisFrame = new Set();
    this.pointerDelta = { x: 0, y: 0 };
    this.pointerLocked = false;

    this.handleKeyDown = this.handleKeyDown.bind(this);
    this.handleKeyUp = this.handleKeyUp.bind(this);
    this.handleMouseMove = this.handleMouseMove.bind(this);
    this.handlePointerLockChange = this.handlePointerLockChange.bind(this);
  }

  connect() {
    window.addEventListener("keydown", this.handleKeyDown);
    window.addEventListener("keyup", this.handleKeyUp);
    document.addEventListener("mousemove", this.handleMouseMove);
    document.addEventListener("pointerlockchange", this.handlePointerLockChange);
  }

  requestPointerLock() {
    const request = this.targetElement.requestPointerLock?.();
    request?.catch?.(() => {});
  }

  handleKeyDown(event) {
    const keyAliases = this.getKeyAliases(event);
    const wasAlreadyDown = keyAliases.some((key) => this.keys.has(key));

    for (const key of keyAliases) {
      this.keys.add(key);
    }

    if (!wasAlreadyDown) {
      for (const key of keyAliases) {
        this.pressedThisFrame.add(key);
      }
    }
  }

  handleKeyUp(event) {
    for (const key of this.getKeyAliases(event)) {
      this.keys.delete(key);
    }
  }

  handleMouseMove(event) {
    if (!this.pointerLocked) {
      return;
    }
    this.pointerDelta.x += event.movementX;
    this.pointerDelta.y += event.movementY;
  }

  handlePointerLockChange() {
    this.pointerLocked = document.pointerLockElement === this.targetElement;
  }

  isDown(...keys) {
    return keys.some((key) => this.keys.has(key.toLowerCase()));
  }

  consumePressed(key) {
    const normalized = key.toLowerCase();
    if (!this.pressedThisFrame.has(normalized)) {
      return false;
    }
    this.pressedThisFrame.delete(normalized);
    return true;
  }

  consumePointerDelta() {
    const delta = { ...this.pointerDelta };
    this.pointerDelta.x = 0;
    this.pointerDelta.y = 0;
    return delta;
  }

  clearKey(key) {
    const normalized = key.toLowerCase();
    this.keys.delete(normalized);
    this.pressedThisFrame.delete(normalized);
  }

  endFrame() {
    this.pressedThisFrame.clear();
  }

  getKeyAliases(event) {
    const aliases = new Set();
    if (event.key) {
      aliases.add(event.key.toLowerCase());
    }

    const layoutAlias = koreanLayoutToAlias(event.key);
    if (layoutAlias) {
      aliases.add(layoutAlias);
    }

    const codeAlias = keyCodeToAlias(event.code);
    if (codeAlias) {
      aliases.add(codeAlias);
    }

    return [...aliases];
  }
}

function keyCodeToAlias(code) {
  if (!code) {
    return null;
  }

  if (/^Key[A-Z]$/.test(code)) {
    return code.at(-1).toLowerCase();
  }

  if (/^Digit[0-9]$/.test(code)) {
    return code.at(-1);
  }

  const specialKeys = {
    ArrowUp: "arrowup",
    ArrowDown: "arrowdown",
    ArrowLeft: "arrowleft",
    ArrowRight: "arrowright",
    ShiftLeft: "shift",
    ShiftRight: "shift",
    Space: " ",
    Escape: "escape",
  };

  return specialKeys[code] || null;
}

function koreanLayoutToAlias(key) {
  const koreanAliases = {
    "ㅂ": "q",
    "ㅈ": "w",
    "ㄷ": "e",
    "ㄱ": "r",
    "ㅅ": "t",
    "ㅛ": "y",
    "ㅕ": "u",
    "ㅑ": "i",
    "ㅐ": "o",
    "ㅔ": "p",
    "ㅁ": "a",
    "ㄴ": "s",
    "ㅇ": "d",
    "ㄹ": "f",
  };

  return koreanAliases[key] || null;
}
