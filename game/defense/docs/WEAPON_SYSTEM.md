# Weapon System Documentation

The weapon system provides progression and firepower scaling as the user grows their mob or accumulates kills.

## Weapon Configurations

| Weapon Tier | Fire Rate (s) | Bullet Speed | Damage | Spread (deg) | Bullets per Shot | Color |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Pistol** | 0.45 | 35.0 | 12 | 0.0 | 1 | Yellow |
| **Dual Pistol** | 0.35 | 35.0 | 12 | 1.5 | 2 (Left & Right) | Cyan |
| **SMG** | 0.14 | 40.0 | 8 | 4.0 | 1 | Lime |
| **Rifle** | 0.22 | 45.0 | 22 | 0.8 | 1 | Orange |
| **Minigun** | 0.07 | 50.0 | 10 | 6.0 | 1 | Red |

## Upgrade Progression Logic

The current active weapon is upgraded dynamically inside the game loop using the player's total crowd size (player + allies) and total kills:

```python
def get_weapon_for_allies(self, ally_count: int, kills: int) -> Weapon:
    if ally_count >= 40 or kills >= 100:
        return WEAPONS["Minigun"]
    elif ally_count >= 20 or kills >= 50:
        return WEAPONS["Rifle"]
    elif ally_count >= 10 or kills >= 25:
        return WEAPONS["SMG"]
    elif ally_count >= 4 or kills >= 10:
        return WEAPONS["Dual Pistol"]
    return WEAPONS["Pistol"]
```

## Bullet Pooling

To handle high volumes of bullets (especially with 40+ soldiers firing Miniguns), the weapon system incorporates an object pool of `Bullet` entities.
- Bullets are pre-allocated upon initialization.
- When shooting, the system fetches an inactive bullet, resets its position/velocity/damage, and marks it active.
- When range limit or collision occurs, the bullet is deactivated (hidden) instead of destroyed, removing garbage collector spikes.
