from __future__ import annotations

import os
import sys
from pathlib import Path

from ursina import (
    AmbientLight,
    DirectionalLight,
    Entity,
    Sky,
    Text,
    Vec2,
    Vec3,
    color,
    held_keys,
    mouse,
    scene,
    time,
    window,
    Ursina,
)
from ursina.prefabs.first_person_controller import FirstPersonController


APP_DIR = Path(__file__).resolve().parent
MODEL_DIR = APP_DIR / "model"

SUPPORTED_MODEL_EXTENSIONS = (".glb", ".gltf", ".obj", ".bam")
RAW_MODEL_EXTENSIONS = (".fbx",)

GROUND_SIZE = 80
PLAYER_START = Vec3(0, 2, -14)
MODEL_START = Vec3(0, 0, 4)

MODEL_SCALE_STEP = 0.1
MODEL_ROTATION_SPEED = 60

DISPLAY_ERROR_MESSAGE = """
GUI 창을 열 수 없습니다.

이 프로그램은 3D 창을 띄우는 Ursina 앱이라 그래픽 디스플레이가 필요합니다.
현재 환경에서 X11/모니터 권한이 없거나 DISPLAY 연결이 막혀 있습니다.

해결 방법:
1. 데스크톱 화면이 있는 환경에서 실행
2. 원격 서버라면 VNC, NoMachine, X11 forwarding 같은 GUI 접속 사용
3. 단순 실행 확인만 필요하면 xvfb-run 사용
   xvfb-run -s "-screen 0 1280x720x24" python main.py
""".strip()


OBSTACLES = [
    {"name": "small_box_01", "model": "cube", "position": (-6, 0.5, -3), "scale": (1, 1, 1), "color": color.azure},
    {"name": "small_box_02", "model": "cube", "position": (5, 0.5, -6), "scale": (1.2, 1, 1.2), "color": color.cyan},
    {"name": "wide_step_01", "model": "cube", "position": (-10, 0.25, 6), "scale": (6, 0.5, 2), "color": color.lime},
    {"name": "wide_step_02", "model": "cube", "position": (9, 0.35, 7), "scale": (5, 0.7, 2.5), "color": color.green},
    {"name": "tall_wall_01", "model": "cube", "position": (-14, 2, -9), "scale": (1, 4, 8), "color": color.orange},
    {"name": "tall_wall_02", "model": "cube", "position": (14, 2, -1), "scale": (1, 4, 10), "color": color.red},
    {"name": "long_wall_01", "model": "cube", "position": (0, 1.5, 14), "scale": (14, 3, 1), "color": color.yellow},
    {"name": "pillar_01", "model": "cylinder", "position": (-5, 1.5, 11), "scale": (1.3, 3, 1.3), "color": color.violet},
    {"name": "pillar_02", "model": "cylinder", "position": (4, 2, 10), "scale": (1.8, 4, 1.8), "color": color.magenta},
    {"name": "large_block_01", "model": "cube", "position": (-2, 1.5, -11), "scale": (4, 3, 4), "color": color.rgba(130, 160, 255, 255)},
    {"name": "large_block_02", "model": "cube", "position": (10, 2.5, -13), "scale": (5, 5, 3), "color": color.rgba(255, 160, 130, 255)},
]


class ModelController(Entity):
    """Loads the current test model and keeps small keyboard controls together."""

    def __init__(self) -> None:
        super().__init__()
        self.loaded_model: Entity | None = None
        self.placeholder: Entity | None = None
        self.current_scale = 1.0
        self.status_text = Text(
            text="",
            origin=(-0.5, 0.5),
            position=(-0.86, 0.42),
            scale=0.9,
            background=True,
        )
        self.load_model_or_placeholder()

    def update(self) -> None:
        target = self.loaded_model or self.placeholder
        if target is None:
            return

        if held_keys["q"]:
            target.rotation_y -= MODEL_ROTATION_SPEED * time.dt
        if held_keys["e"]:
            target.rotation_y += MODEL_ROTATION_SPEED * time.dt
        if held_keys["r"]:
            self.reset_transform()
        if held_keys["="] or held_keys["+"]:
            self.set_scale(self.current_scale + MODEL_SCALE_STEP * time.dt * 4)
        if held_keys["-"]:
            self.set_scale(max(0.05, self.current_scale - MODEL_SCALE_STEP * time.dt * 4))

    def load_model_or_placeholder(self) -> None:
        model_path = find_first_supported_model()
        if model_path is None:
            raw_models = find_raw_models()
            raw_model_hint = f"\nFBX 원본 발견: {raw_models[0].name}" if raw_models else ""
            self.create_placeholder()
            self.status_text.text = (
                "model 폴더에 실행 가능한 모델이 없습니다.\n"
                "FBX 원본은 보관하고, GLB/GLTF/OBJ로 변환한 파일을 넣어주세요."
                f"{raw_model_hint}"
            )
            return

        self.loaded_model = Entity(
            model=str(model_path),
            position=MODEL_START,
            scale=self.current_scale,
            collider="box",
            name="test_model",
        )
        self.status_text.text = f"Loaded model: {model_path.name}\nQ/E 회전, +/- 크기, R 초기화"

    def create_placeholder(self) -> None:
        self.placeholder = Entity(
            model="cube",
            position=MODEL_START + Vec3(0, 1, 0),
            scale=(1, 2, 1),
            color=color.white,
            collider="box",
            name="placeholder_model",
        )
        Entity(
            parent=self.placeholder,
            model="sphere",
            position=(0, 0.8, 0),
            scale=0.6,
            color=color.rgb(210, 210, 210),
        )

    def set_scale(self, value: float) -> None:
        self.current_scale = round(value, 3)
        target = self.loaded_model or self.placeholder
        if target is not None:
            target.scale = self.current_scale if self.loaded_model else (self.current_scale, self.current_scale * 2, self.current_scale)

    def reset_transform(self) -> None:
        self.current_scale = 1.0
        target = self.loaded_model or self.placeholder
        if target is None:
            return
        target.position = MODEL_START if self.loaded_model else MODEL_START + Vec3(0, 1, 0)
        target.rotation = (0, 0, 0)
        self.set_scale(1.0)


def find_first_supported_model() -> Path | None:
    MODEL_DIR.mkdir(exist_ok=True)
    for extension in SUPPORTED_MODEL_EXTENSIONS:
        candidates = sorted(MODEL_DIR.glob(f"*{extension}"))
        if candidates:
            return candidates[0]
    return None


def find_raw_models() -> list[Path]:
    MODEL_DIR.mkdir(exist_ok=True)
    raw_models: list[Path] = []
    for extension in RAW_MODEL_EXTENSIONS:
        raw_models.extend(sorted(MODEL_DIR.glob(f"*{extension}")))
    return raw_models


def create_ground() -> None:
    Entity(
        model="cube",
        position=(0, -0.1, 0),
        scale=(GROUND_SIZE, 0.2, GROUND_SIZE),
        color=color.rgb(74, 94, 82),
        texture="white_cube",
        texture_scale=(GROUND_SIZE // 2, GROUND_SIZE // 2),
        collider="box",
        name="ground",
    )

    for coordinate in range(-GROUND_SIZE // 2, GROUND_SIZE // 2 + 1, 5):
        Entity(
            model="cube",
            position=(coordinate, 0.01, 0),
            scale=(0.03, 0.03, GROUND_SIZE),
            color=color.rgba(255, 255, 255, 45),
            name="grid_line_x",
        )
        Entity(
            model="cube",
            position=(0, 0.012, coordinate),
            scale=(GROUND_SIZE, 0.03, 0.03),
            color=color.rgba(255, 255, 255, 45),
            name="grid_line_z",
        )


def create_obstacles() -> None:
    for obstacle in OBSTACLES:
        Entity(
            model=obstacle["model"],
            position=obstacle["position"],
            scale=obstacle["scale"],
            color=obstacle["color"],
            collider="box",
            name=obstacle["name"],
        )


def create_lights() -> None:
    AmbientLight(color=color.rgba(120, 120, 120, 255))
    sun = DirectionalLight()
    sun.look_at(Vec3(1, -2, -1))


def create_player() -> FirstPersonController:
    player = FirstPersonController(
        position=PLAYER_START,
        speed=7,
        jump_height=1.2,
        mouse_sensitivity=Vec2(40, 40),
    )
    player.cursor.visible = False
    return player


def create_help_text() -> None:
    Text(
        text=(
            "WASD 이동 | Space 점프 | 마우스 시점\n"
            "Q/E 모델 회전 | +/- 모델 크기 | R 모델 초기화 | Esc 마우스 해제"
        ),
        origin=(-0.5, 0.5),
        position=(-0.86, 0.49),
        scale=0.9,
        background=True,
    )


def input(key: str) -> None:
    if key == "escape":
        mouse.locked = False


def create_app() -> Ursina:
    if not os.environ.get("DISPLAY"):
        print(DISPLAY_ERROR_MESSAGE, file=sys.stderr)
        sys.exit(1)

    try:
        return Ursina(size=(1280, 720))
    except Exception as exc:
        if "Could not open window" in str(exc):
            print(DISPLAY_ERROR_MESSAGE, file=sys.stderr)
            sys.exit(1)
        raise


def run_headless_check() -> None:
    supported_model = find_first_supported_model()
    raw_models = find_raw_models()

    print("Model Test Map check")
    print(f"- model folder: {MODEL_DIR}")
    print(f"- obstacles: {len(OBSTACLES)}")
    print(f"- player start: {PLAYER_START}")
    print(f"- model start: {MODEL_START}")

    if supported_model:
        print(f"- runtime model: {supported_model.name}")
    else:
        print("- runtime model: none")

    if raw_models:
        print("- raw FBX models:")
        for raw_model in raw_models:
            print(f"  - {raw_model.name}")
    else:
        print("- raw FBX models: none")

    print("Check complete. GUI 실행은 python main.py 를 사용하세요.")


def main() -> None:
    if "--check" in sys.argv:
        run_headless_check()
        return

    app = create_app()
    window.title = "Model Test Map"
    window.borderless = False
    window.fullscreen = False
    window.exit_button.visible = False
    window.fps_counter.enabled = True

    create_ground()
    create_obstacles()
    create_lights()
    create_help_text()
    create_player()
    ModelController()
    Sky()

    scene.fog_density = 0.015
    scene.fog_color = color.rgb(120, 150, 180)

    app.run()


if __name__ == "__main__":
    main()
