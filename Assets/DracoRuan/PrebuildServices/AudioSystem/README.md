# AudioSystem

A VContainer-driven audio service for Unity: channels routed through an `AudioMixer`, fire-rate
and concurrency gating, fades, cross-fades, Addressable or direct clip loading, and an Editor
workflow for authoring sounds and generating compile-time identifiers.

## Assemblies

| Assembly | Folder | Platform | Contains |
|---|---|---|---|
| `DracoRuan.PrebuildServices.AudioSystem.Logic` | `Logic/` | All | Pure C# logic: fire-rate gate, id registry/sanitizer, decibel/curve/value-range math. No `UnityEngine` dependency. |
| `DracoRuan.PrebuildServices.AudioSystem` | `Data/`, `Core/`, `Interfaces/`, `Installer/` | All | Runtime: `AudioEntry`/`AudioConfig`/`AudioCollection` ScriptableObjects, `AudioService`, mixer/voice/fade/loading internals, the VContainer installer. |
| `DracoRuan.PrebuildServices.AudioSystem.Editor` | `Editor/` | Editor | Audio Manager window, entry creation/deletion, id generation, Addressables/asset postprocessing. |
| `DracoRuan.PrebuildServices.AudioSystem.Tests` | `Tests/Editor/` | Editor, `UNITY_INCLUDE_TESTS` | EditMode tests for `Logic`. |
| `DracoRuan.PrebuildServices.AudioSystem.Editor.Tests` | `Editor/Tests/` | Editor, `UNITY_INCLUDE_TESTS` | EditMode tests for the Editor tooling (id source rendering). |

`Generated/AudioId.cs` is **not** part of any of these assemblies. It is written into your own
project's `Assets/` folder (see [Generated ids](#generated-ids-audioidcs) below) and compiles
against whichever assembly your game code uses.

### External dependencies

- `Cysharp.Threading.Tasks` (UniTask) + `UniTask.Addressables`
- `VContainer`
- `Unity.Addressables` + `Unity.ResourceManager`
- `Sirenix.OdinInspector` (Editor-only usage; precompiled, no asmdef needed)
- `DracoRuan.Foundation.Initializers` — `IAsyncInitializable`, `[AutoInstall]`
- `DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices` / `...Handlers` — `IUpdateHandler`,
  `UpdateServiceManager`

All are referenced by name in the `.asmdef` files; a consuming project needs the corresponding
packages/assemblies present, not necessarily this exact folder layout.

## Setting up in a new project

1. Install the dependencies above (UniTask, VContainer, Addressables via `manifest.json`; Odin as
   a plugin; the two DracoRuan Foundation assemblies alongside this one).
2. Create an **AudioConfig** asset: `Assets > Create > DracoRuan/AudioSystem/AudioConfig`. Assign
   your `AudioMixer` and declare channels (e.g. `Music`, `SFX`, `UI`).
3. Create an **AudioCollection** asset via the Audio Manager window (see below) — there must be
   exactly one per project; `AudioDatabaseLocator` errors if it finds zero or more than one.
4. Create an **AudioInstaller** asset (`Assets > Create > DracoRuan/AudioSystem/AudioInstaller`),
   assign the config and collection, and let your VContainer `LifetimeScope` pick it up (it is
   tagged `[AutoInstall]`).
5. Open **Tools > Foundations > Audio Editor > Audio Manager** to create `AudioEntry` assets, wire
   them to channels, and generate `AudioId`.
6. Call sounds by their generated constant: `audioService.Play(AudioId.Click);`.

## Generated ids (`AudioId.cs`)

`Tools > Foundations > Audio Editor > Regenerate Audio Ids` (or the button in the Audio Manager
window) scans every `AudioEntry` on disk and every channel in `AudioConfig`, and rewrites one file
with `AudioId.*` and `AudioChannelId.*` constants. It is always a full rebuild from disk — never
hand-edit it, the next generation overwrites it entirely.

**Where it's written** is controlled by `AudioConfig.GeneratedIdFolder` (defaults to
`Assets/DracoRuan/PrebuildServices/AudioSystem/Generated`). This folder is project-specific data,
not part of the package: point it at a folder your own game-code assembly can see, e.g.
`Assets/_Project/Generated`.

**Why it must live outside the package:** the ids in this file (`AudioId.Click`, `AudioId.Boom`,
…) are the names of *your* sounds, not the system's. Two different games using this same
AudioSystem package will generate two completely different `AudioId.cs` files. If this file were
checked into the package, every project would fight over its content, and a fresh clone of the
package would immediately break every project that hadn't regenerated it. Instead, treat it like a
build artifact: regenerate it after pulling changes, and optionally `.gitignore` it.

## Packaging as a UPM package

To publish this system for reuse across projects (e.g. on OpenUPM):

1. **Package only the assemblies above** (`Logic/`, the runtime folders, `Editor/`, and the two
   `Tests` folders). Do **not** include `Generated/` — it holds one project's `AudioId.cs`, not
   the package's.
2. **Do not ship any `AudioEntry`/`AudioConfig`/`AudioCollection` assets** — those are curated,
   per-project data. The package provides the `ScriptableObject` types; each consuming project
   creates its own assets.
3. Ship a `package.json` declaring the external dependencies above as UPM `dependencies` (UniTask,
   VContainer, Addressables) so Unity resolves them automatically when the package is added.
   Odin Inspector cannot be declared as a UPM dependency (it isn't distributed that way); note it
   as a manual prerequisite in the package README instead.
   `DracoRuan.Foundation.Initializers` and `DracoRuan.PrebuildServices.PlayerLoopSystem.*` need to
   ship either as their own packages or be vendored alongside this one — this package's asmdefs
   reference them by name, so they must exist somewhere in the consuming project.
4. After a consuming project adds the package, they follow [Setting up in a new
   project](#setting-up-in-a-new-project) above: create their own config/collection/installer
   assets and generate their own `AudioId.cs` into their own `Assets/`.

## Key runtime behavior

- **Lookup is O(1).** `AudioDatabase` builds a `Dictionary<string,int>` (`AudioIdRegistry`) once
  at `Initialize`, so `Play`, `TryGetEntry`, `TryGetIndex` and `GetOrRegisterTransient` are all a
  single dictionary lookup plus array indexing — no linear scans on the hot path.
- **`AudioCollection` is a flat list.** `Contains` is backed by a `HashSet<AudioEntry>` rebuilt in
  `OnValidate`, so registration checks in the Editor tooling are also O(1).
- **The service starts itself.** `AudioService`'s constructor kicks off `InitializeAsync` and
  registers as `IAsyncInitializable`; nothing calls an explicit `Initialize()` from outside.
