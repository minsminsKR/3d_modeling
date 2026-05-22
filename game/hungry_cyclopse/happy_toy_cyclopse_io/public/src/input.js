export class InputController {
  constructor(canvas) {
    this.canvas = canvas;
    this.keys = new Set();
    this.godMode = false;
    this.flashlightOn = false;
    this.yaw = Math.PI;
    this.pitch = 18 * Math.PI / 180;
    this.pointerLocked = false;
    this.rotationSpeed = 0.006;
    this.pitchMin = -10 * Math.PI / 180;
    this.pitchMax = 38 * Math.PI / 180;
    this.cameraZoom = 1;
    this.cameraZoomMin = 0.35;
    this.cameraZoomMax = 2.8;

    canvas.addEventListener(
      "wheel",
      (event) => {
        event.preventDefault();
        const step = event.deltaY > 0 ? 0.1 : -0.1;
        this.cameraZoom = Math.max(this.cameraZoomMin, Math.min(this.cameraZoomMax, this.cameraZoom + step));
      },
      { passive: false }
    );

    window.addEventListener("keydown", (event) => {
      if (event.code === "KeyF") {
        if (event.repeat) return;
        this.flashlightOn = !this.flashlightOn;
        return;
      }
      if (event.code === "Numpad0") {
        if (event.repeat) return;
        this.godMode = !this.godMode;
        window.dispatchEvent(new CustomEvent("godmodechange", { detail: { enabled: this.godMode } }));
        return;
      }
      this.keys.add(event.key.toLowerCase());
    });
    window.addEventListener("keyup", (event) => this.keys.delete(event.key.toLowerCase()));
    document.addEventListener("pointerlockchange", () => {
      this.pointerLocked = document.pointerLockElement === this.canvas;
    });
    window.addEventListener("mousemove", (event) => {
      if (!this.pointerLocked) return;
      this.yaw -= event.movementX * this.rotationSpeed;
      this.pitch = Math.max(this.pitchMin, Math.min(this.pitchMax, this.pitch + event.movementY * this.rotationSpeed));
    });
    canvas.addEventListener("pointerdown", (event) => {
      if (event.button === 0) canvas.requestPointerLock();
    });
  }

  snapshot() {
    return {
      up: this.keys.has("w") || this.keys.has("arrowup"),
      down: this.keys.has("s") || this.keys.has("arrowdown"),
      left: this.keys.has("a") || this.keys.has("arrowleft"),
      right: this.keys.has("d") || this.keys.has("arrowright"),
      sprint: this.keys.has("shift"),
      godMode: this.godMode,
      flashlightOn: this.flashlightOn,
      cameraZoom: this.cameraZoom,
      yaw: this.yaw,
      pitch: this.pitch
    };
  }
}
