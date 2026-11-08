# CART315 First Person — setup guide

**Unity 6.3 LTS · keyboard + mouse · Input System**

## Get moving

1. Install **Input System** in Window → Package Manager → Unity Registry. In Project Settings → Player → Other Settings, set **Active Input Handling** to **Input System Package (New)** (or Both). Restart if Unity asks. The provided project already has this configured.
2. Import `CART315-FirstPerson.unitypackage` using Assets → Import Package → Custom Package. Keep the included files together.
3. Drag `CART315FirstPerson/Prefabs/CART315 First Person` into your scene, or use **CART315 → Add First Person Player**. Place its feet just above your floor. Keep its scale at **1, 1, 1** and rotate only its Y axis to choose the starting direction.
4. Remove or disable your scene's old camera and Audio Listener. The player includes both. Floors, walls, and stairs need non-trigger **Colliders**. Keep world geometry on the Default layer; the prefab already uses Ignore Raycast for its ground checks.
5. Press Play, click the Game view, and explore. **WASD** moves, **mouse** looks, **Left Shift** sprints forward, **Space** jumps, and **Escape** releases the mouse. Click again to resume. Try **CART315 → Open Controller Playground** for a ready-made scene.

## Make it yours — select the player's root in the Hierarchy

| Component | Main settings |
| --- | --- |
| FP Controller | Movement Speed (metres/second), Acceleration, Mouse Sensitivity, Max Camera Y (look limit). |
| FP Sprinting | Speed Multiplier (default 1.5 × walking speed). Uncheck the component to disable sprint. |
| FP Jumping | Jump Height (metres). Uncheck the component to disable jump. |
| FP Head Bobbing | Enable Head Bob, Intensity (metres), Frequency. Turn it off for a steady camera. |
| FP Footsteps | Sounds: drag your own audio clips into the list. Empty means silent. Step Distance sets metres between steps; Volume sets loudness. Uncheck to disable. |
| Character Controller | Step Offset (default 0.3 m), Slope Limit (45°). Start with the supplied values. |

Edit key bindings by double-clicking `Input/CART315Controls.inputactions`, selecting **Player**, changing a binding, and saving the asset. Keep the action names. No generated C# class, Player Input component, or event wiring is needed.

## If something feels wrong

- **No control:** click the Game view; check Input System is active and the FP Input Manager's Actions field is assigned. Escape intentionally stops input.
- **Falling through:** add a Collider to the ground, uncheck Is Trigger, and spawn above it. Keep the ground off Ignore Raycast.
- **Camera/audio warnings:** keep only one active camera and Audio Listener for this player.
- **No footsteps:** add valid clips to Sounds, enable FP Footsteps and the Audio Source, and check Game view audio isn't muted. Steps occur only after travelling on the ground. Holding W against a wall is silent.
- **Motion discomfort:** turn off head bob, then adjust sensitivity and the child Player Camera's Field of View.

## Share your prototype

In File → Build Profiles, add your scene to the scene list and choose Windows, macOS, Linux, or Web. Install the matching build-support module in Unity Hub. For Web, serve the build through a web server or a host such as itch.io; do not double-click `index.html`. Click inside the game to capture the mouse; Escape releases it. Browser pointer-lock/audio permissions must be checked in an actual browser build.

Keep `LICENSE.txt` and `Documentation/PROVENANCE.md` when sharing the controller. Add only audio you have permission to redistribute. Moving platforms, pushing physics objects, gamepad input, and per-surface footsteps are outside this starter's scope.
