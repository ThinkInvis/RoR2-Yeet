# Yeet

## Description

Clicking an item or equipment in your inventory will remove one stack of it and forcefully toss it in the direction you're aiming. Functions on both client and server by way of the `yeet [item name/index]` console command, which may also be used on its own.

Holding the mouse button down for longer will throw the item farther.

Config options include (default values):

- Server:
	- Blacklist, prevent dropping:
		- Items and equipment by name token (none)
		- Items by tier (Lunar and all Void)
		- Tierless/hidden/non-removable items (all on)
		- Non-Lunar or Lunar equipment (both off)
		- All items (off)
		- All equipment (off).
	- Cooldown on dropping (10s) and on picking up items you just dropped (5s)
	- Prevent Recycler on dropped items (on)
	- Drop Command droplets if relevant artifact is active (off)
	- Limit maximum allowable items dropped per click if dropping multiple (1/off)
	- Entirely disable mod temporarily (on/not disabled)
	- Announce dropped items in chat (on)
	- Minimum and maximum throw force (30, 150)
- Client:
	- Make left and/or right click drop multiple items (1/off, 1/off)
	- Click hold time required to reach maximum throw force (2 sec)

## Issues/TODO

- Doesn't support controllers.
- Could do with some sort of UI indicator that items can be clicked.
- "Drop last valid pickup" command/keybind.
- Primary skill is fired while clicking in inventory to drop items (the alternative is 'sticky' UI focus when adding buttons; picked lesser of two evils for now).
- See the GitHub repo for more!

## Changelog

The 5 latest updates are listed below. For a full changelog, see: https://github.com/ThinkInvis/RoR2-Yeet/blob/master/changelog.md

**3.0.4**

- Recompiled for Seekers of the Storm. No other changes appear to be necessary.

**3.0.3**

- Fixed fatal errors caused by the base game Devotion Update.

**3.0.2**

- AllowYeet is now a config option instead of a ConVar.
- For developers: Now uses local NuGet config, removing the requirement for manual addition of a source.
- Updated R2API dependency to 5.0.6 (now using split assembly).
- Updated TILER2 dependency to 7.3.4.
- Updated BepInExPack dependency to 5.4.2103.

**3.0.1**

- Fixed only one item being removed from inventory while dropping multiple copies of that item.
- Slightly optimized order of checks before dropping an item.

**3.0.0**

- Made serverside blacklist configs more granular.
	- Added BlacklistTier and BlacklistItem comma-delimited strings. BlacklistItem affects equipments, too.
	- Added PreventLunarEquipment and PreventNonLunarEquipment booleans for equipments.
	- Added PreventHidden, PreventCantRemove, and PreventTierless booleans for items.
	- PreventLunar and PreventVoid have been removed (merged into tier blacklist).
- Added an option to announce dropped items to all players in chat, enabled by default.
- Added an option to drop multiple items per click, disabled by default.
- Added an option to drop differing item counts between left and right click, disabled by default.
- ConCmdYeet now uses chat to tell the calling player why it failed in some cases.
- Removed code that was still unnecessarily treating TILER2 as a soft dependency.
- Switched to TILER2 NetUtil for networked chat messages, was previously using an internal implementation.
- Updated dependencies, and updated lang version to C#9.