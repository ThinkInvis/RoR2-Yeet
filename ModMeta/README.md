# Yeet

## Description

Clicking an item or equipment in your inventory will remove one stack of it and forcefully toss it in the direction you're aiming. Functions on both client and server by way of the `yeet [item name/index]` console command, which may also be used on its own.

Holding the mouse button down for longer will throw the item farther.

Has config options to prevent dropping: lunar items (on by default), void items (on by default), all items, all equipment.

Has config options for cooldown on dropping (10s by default) and on picking up an item you just dropped (5s by default).

Has a config option to prevent the Recycler from working on dropped items.

Has a config option to drop Command droplets instead of items while the Artifact of Command is active.

Has a serverside convar that disables dropping items at all for all clients.

## Issues/TODO

- Could do with some sort of UI indicator that items can be clicked.
- "Drop last valid pickup" command/keybind.
- Primary skill is fired while clicking in inventory to drop items (the alternative is 'sticky' UI focus when adding buttons; picked lesser of two evils for now).
- See the GitHub repo for more!

## Changelog

**2.1.1**

- Fixed `yeet_on` convar defaulting to 0 (intended default value is 1).

**2.1.0**

- Added a server convar (`yeet_on`) that enables/disables most mod functionality.

**2.0.1**

- Added a config for disabling using Recycler on dropped items.

**2.0.0**

- Implemented a cooldown on item dropping. Defaults to 10s.
- Implemented a cooldown on picking up an item you just dropped. Defaults to 5s.
- Significant changes to the inner workings of the mod to support these features.

**1.2.3**

- Fixed dropped items disappearing on spawn.
- Fixed dropping equipment also dropping items with the same numeric ID.
- Added a config for disabling void item dropping.