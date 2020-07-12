# Yeet

A mod for Risk of Rain 2. Built with BepInEx and R2API.

Allows throwing items on the ground by clicking them in the inventory screen.

## Installation

Release builds are published to Thunderstore: https://thunderstore.io/package/ThinkInvis/Yeet/
	
**Use of a mod manager is recommended**. If not using a mod manager: Extract ThinkInvis-Yeet-[version].zip into your BepInEx plugins folder such that the following path exists: `[RoR2 game folder]/BepInEx/Plugins/ThinkInvis-Yeet-[version]/Yeet.dll`.

## Building

Building Yeet locally will require setup of the postbuild event:
- All xcopy calls need to either be updated with the path to your copy of RoR2, or removed entirely if you don't want copies of the mod moved for testing.