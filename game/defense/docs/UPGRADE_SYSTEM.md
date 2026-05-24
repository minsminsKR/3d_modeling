# Upgrade System

## 목표

런마다 성장 선택을 제공해서 단순한 게이트 수집을 넘어 `Vampire Survivors`식 누적 성장감을 만든다. 플레이 도중 레벨업하면 업그레이드 선택창이 뜨고, 선택 결과는 즉시 전투 수치에 반영된다.

## 런 상태

런 성장 상태는 `core/progression.py`의 `RunProgression`이 관리한다.

| 값 | 설명 |
| --- | --- |
| `level` | 현재 런 레벨 |
| `exp`, `exp_to_next` | 경험치와 다음 레벨 필요량 |
| `coins`, `gems` | 런 중 획득 재화 |
| `combo`, `combo_timer` | 연속 처치 표시와 보상 배율 |
| `fire_rate_mult` | 발사 간격 배율 |
| `damage_mult` | 피해 배율 |
| `extra_projectiles` | 추가 탄 수 |
| `spread_bonus` | 확산 보너스 |
| `crit_chance` | 치명타 확률 |
| `move_speed_mult` | 이동 속도 배율 |
| `ally_cap` | 최대 아군 수 |

## 레벨업 흐름

1. 적 처치 시 `RunProgression.add_kill_reward(points)`가 호출된다.
2. 경험치가 `exp_to_next` 이상이면 레벨이 오르고 `pending_upgrade`가 켜진다.
3. `GameManager.show_upgrade_choice()`가 `UpgradeUI`를 표시하고 게임 시간을 멈춘다.
4. 선택한 업그레이드는 `GameManager.apply_upgrade()`를 통해 적용된다.
5. 무기 관련 수치는 `WeaponSystem.set_run_modifiers()`로 전달된다.

## 업그레이드 목록

| 키 | 효과 |
| --- | --- |
| `fire_rate` | 발사 간격 20% 감소 |
| `damage` | 피해 30% 증가 |
| `ally_spawn` | 아군 5명 즉시 추가 |
| `spread` | 확산 사격 강화 |
| `double_projectile` | 발사체 1개 추가 |
| `crit` | 치명타 확률 10% 증가 |
| `move_speed` | 이동 속도 10% 증가 |
| `ally_cap` | 최대 아군 수 25 증가 |

## 게이트와의 관계

게이트는 아군 증가뿐 아니라 일부 런 업그레이드를 즉시 적용한다.

- `+5`, `+10`, `+25`: 아군 추가
- `x2`, `x3`: 플레이어 포함 군중 수를 곱한 뒤 아군 수로 환산
- `Fire Rate`: 발사 간격 감소
- `Damage`: 피해 증가
- `Spread`: 확산 강화
- `Random Gun`: 현재 무기를 중상위 무기 중 하나로 즉시 변경

## 밸런스 기준

초반에는 `ally_spawn`, `fire_rate`, `damage`가 체감이 커야 한다. 중반부터는 `extra_projectiles`, `spread`, `crit`가 화면을 채우는 화력 상승을 만든다. 후반 성능 문제가 생기면 업그레이드 수치를 낮추기보다 `AllyManager`의 update throttling과 `BulletPool.max_size`를 먼저 조정한다.
