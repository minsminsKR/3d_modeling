# Game Rules

- Player starts at internal size `5`.
- Visual scale is `size / 5`.
- Eating a smaller enemy adds `+1` size and `+10` score.
- Survival adds `+1` score per second.
- There is no max player size.
- Movement speed does not scale with size.
- Sprinting consumes stamina and is faster than walking.
- Stamina recovers while not sprinting.

## Contact

Contact uses distance:

`distance < player_radius + enemy_radius`

- Player larger than enemy: enemy is eaten.
- Player smaller than enemy: instant death.
- Equal size: both are separated with simple knockback.

## Death

On death, the camera shakes, the screen darkens, and the game over UI displays final score and survival time.
