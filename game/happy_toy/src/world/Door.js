// 열고 닫을 수 있는 문 하나를 표현하는 모듈입니다.
// 렌더링 메시, 상호작용 거리, 닫힌 상태 충돌체를 함께 보유합니다.

import * as THREE from "three";
import { makeAabbFromCenter, smoothStep } from "../utils/math.js";

export class Door {
  constructor(config, material) {
    this.id = config.id;
    this.label = config.label;
    this.position = new THREE.Vector3(...config.position);
    this.size = config.size;
    this.openDirection = config.openDirection ?? 1;
    this.connectedRoomId = config.connectedRoomId || null;
    this.isLocked = Boolean(config.locked);
    this.isBlocked = Boolean(config.blocked);
    this.blockedReason = config.blockedReason || "문이 단단히 막혀 있습니다.";
    this.isOpen = false;
    this.openAmount = 0;
    this.axis = this.size[0] < this.size[2] ? "z" : "x";
    this.panelSpan = this.axis === "z" ? this.size[2] : this.size[0];
    this.slideDistance = config.slideDistance ?? Math.max(0.72, this.panelSpan * 0.52);
    this.panelPositions = [];

    this.group = new THREE.Group();
    this.group.name = this.id;
    this.group.position.copy(this.position);

    this.sharedMaterial = material;
    this.panels = this.createSlidingPanels(material);
    this.createTracks();
  }

  toggle() {
    if (this.isLocked || this.isBlocked) {
      return false;
    }
    this.isOpen = !this.isOpen;
    return true;
  }

  getPrompt() {
    if (this.isLocked || this.isBlocked) {
      return `E - ${this.label} 확인`;
    }
    const action = this.isOpen ? "닫기" : "열기";
    return `E - ${this.label} ${action}`;
  }

  interact(context) {
    if (!this.toggle()) {
      context.hud?.setStatus(this.blockedReason, 1500);
      console.warn(`[Door] ${this.id} is locked/blocked. connectedRoom=${this.connectedRoomId ?? "none"}`);
      return;
    }
    const action = this.isOpen ? "열었습니다" : "닫았습니다";
    context.hud?.setStatus(`${this.label} 문을 ${action}.`, 1400);
  }

  update(deltaTime) {
    const target = this.isOpen ? 1 : 0;
    this.openAmount = smoothStep(this.openAmount, target, 7.5, deltaTime);
    for (const panelState of this.panelPositions) {
      panelState.mesh.position.copy(panelState.closed);
      panelState.mesh.position[this.axis] += panelState.direction * this.slideDistance * this.openAmount;
    }
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isBlocking() {
    return this.isLocked || this.isBlocked || (!this.isOpen && this.openAmount < 0.72);
  }

  getAabb() {
    return makeAabbFromCenter([this.position.x, this.position.y + this.size[1] / 2, this.position.z], this.size);
  }

  getDebugInfo() {
    return {
      id: this.id,
      label: this.label,
      connectedRoomId: this.connectedRoomId,
      mode: "sliding",
      locked: this.isLocked,
      blocked: this.isBlocked,
      open: this.isOpen,
    };
  }

  createSlidingPanels(material) {
    const panels = [];
    const thicknessX = this.size[0];
    const thicknessZ = this.size[2];
    const panelWidth = this.panelSpan / 2 + 0.04;
    const handleMaterial = new THREE.MeshStandardMaterial({ color: 0x16120e, roughness: 0.6, metalness: 0.2 });

    for (const side of [-1, 1]) {
      const panelSize = this.axis === "z"
        ? [thicknessX, this.size[1], panelWidth]
        : [panelWidth, this.size[1], thicknessZ];
      const panel = new THREE.Mesh(new THREE.BoxGeometry(...panelSize), material);
      panel.name = `${this.id}-panel-${side < 0 ? "left" : "right"}`;
      const closed = new THREE.Vector3(0, this.size[1] / 2, 0);
      closed[this.axis] = side * this.panelSpan * 0.25;
      panel.position.copy(closed);
      panel.castShadow = true;
      panel.receiveShadow = true;
      this.group.add(panel);

      const handle = new THREE.Mesh(
        new THREE.CylinderGeometry(0.045, 0.045, 0.08, 12),
        handleMaterial,
      );
      handle.rotation.z = Math.PI / 2;
      handle.position.y = 1.18 - this.size[1] / 2;
      if (this.axis === "z") {
        handle.position.x = -Math.sign(this.position.x || -1) * (thicknessX / 2 + 0.035);
        handle.position.z = -side * panelWidth * 0.24;
      } else {
        handle.position.x = -side * panelWidth * 0.24;
        handle.position.z = -Math.sign(this.position.z || -1) * (thicknessZ / 2 + 0.035);
      }
      panel.add(handle);

      this.panelPositions.push({
        mesh: panel,
        closed,
        direction: side,
      });
      panels.push(panel);
    }

    return panels;
  }

  createTracks() {
    const trackMaterial = new THREE.MeshStandardMaterial({ color: 0x15100d, roughness: 0.86 });
    const trackSpan = this.panelSpan + this.slideDistance * 2 + 0.22;
    const trackSize = this.axis === "z"
      ? [this.size[0] + 0.08, 0.08, trackSpan]
      : [trackSpan, 0.08, this.size[2] + 0.08];

    for (const y of [0.05, this.size[1] + 0.05]) {
      const track = new THREE.Mesh(new THREE.BoxGeometry(...trackSize), trackMaterial);
      track.position.set(0, y, 0);
      track.castShadow = true;
      this.group.add(track);
    }
  }

  dispose() {
    this.group.traverse((child) => {
      if (child.isMesh) {
        child.geometry?.dispose();
        if (child.material) {
          if (Array.isArray(child.material)) {
            child.material.forEach((m) => {
              if (m !== this.sharedMaterial) {
                m.dispose();
              }
            });
          } else if (child.material !== this.sharedMaterial) {
            child.material.dispose();
          }
        }
      }
    });
  }
}
