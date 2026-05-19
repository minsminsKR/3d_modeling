# AI Behavior

## Regular Enemies

Regular enemies wander by default. Occasionally they reevaluate their behavior:

- If smaller than the player, they have a 50% chance to flee.
- If larger than the player, they have a 50% chance to chase.
- Otherwise they wander.

## Giant Cyclopse

Giant Cyclopse has two states:

- `wandering`: random patrol movement
- `chasing`: moves toward the player

It starts chasing when the player is within vision range and gives up when the player gets far enough away.
