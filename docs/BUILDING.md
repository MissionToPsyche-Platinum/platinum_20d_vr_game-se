# Building the two APK flavors

The game ships as one Android APK per game mode. Both are built from the same project;
the only difference is the mode the app boots into.

| Flavor | Boots into | Output file |
| --- | --- | --- |
| Event | the kiosk experience (mission-control event room) | `Builds/Android/PsycheVR-Event.apk` |
| Story | the narrative experience (bedroom desk) | `Builds/Android/PsycheVR-Story.apk` |

The `Builds/` folder sits at the repository root, next to `Psyche VR Experience/`, and is
ignored by git.

## From the editor

Tools > Build APK > **Event**, **Story**, or **Both**.

Each build writes the flavor's mode into `Assets/Resources/GameModeConfig.asset`, builds
`Assets/Scenes/Bedroom.unity` as the only scene, and then puts the asset back the way it
was. After a build, `git status` should not show `GameModeConfig.asset` as modified. If it
does (for example after the editor crashed mid-build), run
`git checkout -- "Psyche VR Experience/Assets/Resources/GameModeConfig.asset"`.

The Build Settings scene list is not used by these menu items, so its order does not
matter for the APKs.

## From the command line

Close the editor first. Replace the editor path with your install location.

```
"<Unity install>/Editor/Unity" -batchmode -nographics -quit \
  -buildTarget Android \
  -projectPath "<repo>/Psyche VR Experience" \
  -executeMethod PsycheVR.Modes.Editor.GameModeBuilder.BuildBoth \
  -logFile build.log
```

Use `BuildEvent` or `BuildStory` instead of `BuildBoth` for one flavor. The process exits
non-zero when a build fails; the reason is in `build.log`.

## Install on a headset

```
adb install -r Builds/Android/PsycheVR-Event.apk
```

The first log line from the game says which mode it booted into:
`[GameModeManager] Booting in Event mode.`

## Changing which mode the editor plays in

Press Play uses the mode saved in `GameModeConfig.asset` (currently Event). To try Story in
the editor without changing the asset, use the gated mode switch in the pause menu once it
lands, or temporarily change the asset and revert it before committing.
