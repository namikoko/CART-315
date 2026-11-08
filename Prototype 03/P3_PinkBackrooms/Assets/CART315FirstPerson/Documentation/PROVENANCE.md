# Source and classroom changes

Adapted from [GTroubley / Simple-First-Person-Controller](https://github.com/GTroubley/Simple-First-Person-Controller), commit `28d1cf3d680ffea62afccb1c9210f790bcb2d99a`, retrieved 2026-10-23. The upstream MIT license is retained in `LICENSE.txt`. The classroom modifications and accompanying new files are provided under the same MIT terms.

## Selection

This was the closest of the inspected existing options: CharacterController, Input System, sprint, jump, head bob, and footsteps, with no Cinemachine dependency. [Showerss/unity-first-person-controller](https://github.com/Showerss/unity-first-person-controller) includes substantial additional gameplay systems and a UGUI-based sample. [ytaxx/UnityPlayerController](https://github.com/ytaxx/UnityPlayerController) includes stamina and compatibility shims, and constructs input actions in code. GTroubley's structure was a better starting point for the agreed classroom scope.

## Changes from upstream

- Retain the movement/slope core and optional jump/sprint component design; scope classes to `Cart315.FirstPerson`.
- Replace the generated input wrapper with an assigned, editable Input Actions asset, cloned per player and cleaned up on destruction.
- Click to capture; Escape, loss of focus, and disabling input release control. Ignore the capture frame's mouse delta.
- Preserve the prefab's starting yaw; respect CharacterController centre when probing the ground.
- Use built-in Ignore Raycast on the prefab instead of requiring a custom Player layer.
- Remove the gravity-by-mass setting; expose a gravity multiplier. Reset stale slope state when airborne.
- Fix jump/sprint interaction that zeroed horizontal movement on the jump frame; clear buffered jumps when control is released.
- Drive footsteps and head bob from actual horizontal displacement. Empty/null audio entries are safe. Bobbing stops against walls and returns to rest; sprint increases cadence without increasing intensity.
- Omit camera shake. Supply a newly generated, pipeline-independent prefab, simple demo shader and scene, setup guide, export/validation tools, and instructor-only tests.

No third-party sounds, textures, or models are included. The demo uses primitive geometry and an original unlit grid shader. The runtime controller requires only Unity's built-in modules and `com.unity.inputsystem`; the instructor test suite additionally uses Unity Test Framework. Exported student packages omit tests.

## Maintainer commands

Run from a **closed** project (or use a temporary copy if the editor is open):

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
# For GUI executables in PowerShell, use Start-Process -Wait when scripting exit codes.
& $unity -batchmode -nographics -quit -projectPath <project> -executeMethod Cart315.FirstPerson.Editor.ControllerTools.Prepare -logFile <log>
& $unity -batchmode -runTests -testPlatform PlayMode -projectPath <project> -testResults <results.xml> -logFile <log>
& $unity -batchmode -quit -projectPath <project> -executeMethod Cart315.FirstPerson.Editor.ControllerTools.BuildWindows -logFile <log>
& $unity -batchmode -quit -projectPath <project> -executeMethod Cart315.FirstPerson.Editor.ControllerTools.BuildWebGL -logFile <log>
```

`Prepare` creates missing prefab/demo assets, validates wiring, and exports `Deliverables/CART315-FirstPerson.unitypackage`. It does not overwrite existing prefab or scene edits. Use CART315 → Export Student Package to export updated settings. Build methods explicitly build the playground without replacing the project's scene list.
