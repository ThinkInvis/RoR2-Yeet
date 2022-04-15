﻿# Yeet Changelog

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

**1.2.2**

- Updated dependencies for RoR2 Expansion 1 (SotV).
- Added a config for dropping items as Command droplets while Artifact of Command is enabled (restores pre-1.2.1 behavior).

**1.2.1**

- Prevented tierless items from being dropped.
- Artifact of Command no longer creates Command droplets for items dropped from a player's inventory.

**1.2.0**

- Added support for dropping equipment.
- Added configs for disabling equipment dropping and/or item dropping.

**1.1.2**

- Fixed apparent compatibility issue with other mods (caused by improper console command setup).

**1.1.1**

- Added compatibility for TILER2.FakeInventory.
- Fixed being able to drop unremovable items.
- Updated dependencies for RoR2 1.0.

**1.1.0**

- Added some extra console logging to try to track down an issue with inventory clicking.
- Default base throw force lowered.
- Clicking and holding for up to 2 seconds increases throw force.
- Added config options for hold time and charged throw force.

**1.0.0**

- Initial version. Adds a console command to throw an item by name/index, and makes clicking items on the inventory screen execute this console command automatically.