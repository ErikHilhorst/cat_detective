---
name: scaffold-case
description: Scaffolds a complete multi-room case. Generates the folder structure, case_config.json, room_config.json files, and minimal Tiled room_map.json files with wired transfers and spawns.
user_invocable: true
---

# Scaffold Case

This skill creates the directory structure and base configuration files for a new multi-room case in Cat Detective.

## Workflow
When the user asks to scaffold a case (e.g., "Scaffold the Malibu Mansion with Entrance, Living Room, and Kitchen. Entrance connects to Living Room, Living Room to Kitchen"):

1. **Create Folders**: Use the Bash tool to create `Content/Levels/<case_id>/` and a subfolder for each room (e.g., `Content/Levels/<case_id>/<room_id>/Interactables/`).
2. **Create `case_config.json`**: In the root of the case folder, create a JSON file with empty arrays for `clues` and an empty string for `finalSolveSentence`.
3. **Create `room_config.json`**: In each room folder, create a JSON file with empty arrays for `props` and `interactables`, and an empty string for `localDeductionSentence`.
4. **Create `room_map.json`**: In each room folder, generate a minimal valid Tiled JSON map.
   - It MUST include layers: `background` (imagelayer), `Collisions` (objectgroup), `Transfers` (objectgroup), `Spawn` (objectgroup), and `Interactables` (objectgroup).
   - **Spawns**: For every room this room connects to, create a point object in the `Spawn` layer named `spawn_from_<other_room>`. Also create a `spawn_default`.
   - **Transfers**: For every room this room connects to, create a rectangle object in the `Transfers` layer. It must have custom string properties: `TargetRoom` (the id of the destination room) and `TargetSpawn` (e.g., `spawn_from_<this_room>`).

## Tiled JSON Template
Use this minimal structure for `room_map.json`:
```json
{
 "compressionlevel": -1,
 "infinite": true,
 "layers":[
  { "id": 1, "name": "background", "type": "imagelayer", "image": "bg_base.jpg", "opacity": 1, "visible": true, "x": 0, "y": 0 },
  { "id": 2, "name": "Collisions", "type": "objectgroup", "objects": [] },
  { "id": 3, "name": "Transfers", "type": "objectgroup", "objects":[] },
  { "id": 4, "name": "Spawn", "type": "objectgroup", "objects":[] },
  { "id": 5, "name": "Interactables", "type": "objectgroup", "objects":[] }
 ],
 "nextlayerid": 6, "nextobjectid": 100,
 "orientation": "orthogonal", "renderorder": "right-down",
"tileheight": 32, "tilewidth": 32, "width": 43, "height": 24, "type": "map", "version": "1.10"
}
```

**Note**: Make sure to inject the Transfer and Spawn objects into the correct layers.

**Remind the User**: Tell the user to add the new bg_base.png files to the directories, and remind them to add them to Content.mgcb.