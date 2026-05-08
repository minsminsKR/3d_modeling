// 플레이어 손전등의 on/off 상태를 관리하는 모듈입니다.
// 지금은 F키 토글만 담당하고, 나중에 배터리/깜빡임/고장 연출을 이곳에 확장합니다.

export class FlashlightController {
  constructor(spotLight, input, hud) {
    this.spotLight = spotLight;
    this.input = input;
    this.hud = hud;
    this.enabled = false;
    this.defaultIntensity = spotLight.intensity;
    this.applyState(false);
  }

  update() {
    if (this.input.consumePressed("f")) {
      this.toggle();
    }
  }

  toggle() {
    this.enabled = !this.enabled;
    this.applyState(true);
  }

  reset() {
    this.enabled = false;
    this.applyState(false);
  }

  applyState(showMessage) {
    this.spotLight.visible = this.enabled;
    this.spotLight.intensity = this.enabled ? this.defaultIntensity : 0;
    this.hud.setFlashlightEnabled(this.enabled);

    if (!showMessage) {
      return;
    }

    const message = this.enabled ? "후레쉬를 켰습니다." : "후레쉬를 껐습니다.";
    this.hud.setStatus(message, 1200);
  }
}
