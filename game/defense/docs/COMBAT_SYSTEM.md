# Combat System

## 목표

`defense` 전투는 모바일 광고형 군중 슈팅의 핵심 감각을 만든다. 플레이어와 아군이 전방으로 자동 사격하고, 적 웨이브를 밀어내면서 아군 수, 무기 등급, 콤보, 보상이 계속 커지는 구조다.

## 주요 흐름

1. `GameManager.update()`가 런 상태를 갱신한다.
2. `PlayerController`와 `AllyManager`가 같은 `WeaponSystem`으로 자동 발사한다.
3. `WeaponSystem`은 무기 클래스별 스탯으로 `BulletPool`에서 탄을 꺼내 발사한다.
4. `EnemyManager`가 적 이동, 적 사격, 총알 충돌, 플레이어/아군 충돌을 처리한다.
5. 처치된 적은 `RunProgression` 경험치/콤보/재화를 올리고 `RewardManager`가 픽업을 생성한다.

## 무기 구조

무기는 `BaseWeapon`을 상속한 클래스로 분리되어 있다.

| 무기 | 역할 |
| --- | --- |
| `Pistol` | 기본 단발 무기 |
| `DualPistol` | 초반 탄 수 증가 |
| `SMG` | 빠른 연사 |
| `Rifle` | 높은 단발 피해 |
| `Shotgun` | 넓은 확산과 다중 투사체 |
| `Laser` | 빠른 탄속, 안정적인 화력 |
| `Minigun` | 화면을 덮는 연사 |
| `RocketLauncher` | 느리지만 폭발 피해 |

런 업그레이드는 `WeaponSystem.set_run_modifiers()`로 반영된다.

- `fire_rate_mult`: 발사 간격 감소
- `damage_mult`: 피해 증가
- `extra_projectiles`: 추가 투사체
- `spread_bonus`: 확산 증가
- `crit_chance`: 치명타 확률

## 타격감

- 총알은 짧은 trail 엔티티를 함께 움직인다.
- 발사 시 `MuzzleFlashPool`에서 muzzle flash를 재사용한다.
- 피격 시 적 색상을 짧게 흰색으로 바꾸고 knockback을 준다.
- 처치 시 particle burst와 camera shake를 만든다.
- 콤보가 높으면 hit feedback이 더 강해진다.
- 큰 게이트나 무기 승급은 짧은 slow motion과 camera shake를 발생시킨다.

## 충돌 규칙

- 총알-적 충돌은 단순 X/Z 박스 판정으로 처리한다.
- 폭발탄은 명중 위치 주변 적에게 splash damage를 준다.
- 적이 플레이어와 충돌하면 아군이 있으면 아군 1명이 소모된다.
- 아군이 없을 때 플레이어가 충돌하면 게임 오버다.
- 너무 많은 아군 전체와 충돌 검사하지 않도록 앞쪽 일부 아군만 검사한다.

## 확장 기준

새 무기를 추가할 때는 `weapons/weapon_system.py`에 `BaseWeapon` 하위 클래스를 만들고 `WEAPON_CLASSES`에 추가한다. 적 타입별 특수 피해, 관통탄, 상태 이상은 `Bullet` 필드와 `Enemy.take_damage()`를 확장하는 방식이 가장 안전하다.
