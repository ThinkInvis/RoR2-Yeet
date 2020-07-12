# Yeet

## Description

Clicking an item in your inventory will remove one stack of it and forcefully toss it in the direction you're aiming. Functions on both client and server by way of the `yeet [item name/index]` console command, which may also be used on its own.

Has a config option to prevent lunar items from being dropped (enabled by default).

## Issues/TODO

- Support for dropping equipment.
- Could do with some sort of UI indicator that items can be clicked.
- "Drop last valid pickup" command/keybind.
- Primary skill is fired while clicking in inventory to drop items (the alternative is 'sticky' UI focus when adding buttons; picked lesser of two evils for now).
- See the GitHub repo for more!

## Changelog

**1.0.0**

- Initial version. Adds a console command to throw an item by name/index, and makes clicking items on the inventory screen execute this console command automatically.