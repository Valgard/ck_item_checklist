# SDK setup for ItemChecklist

This mod requires CoreLib 4.0.0 as a Unity Package. The reference is
NOT committed to the CoreKeeperModSDK repo (per user policy on the
shared SDK clone). You must add it locally once per SDK clone before
the first build:

Open `CoreKeeperModSDK/Packages/manifest.json` and add the following
line to the `dependencies` block (alphabetical position — usually at
the top before any `com.*` entry):

```json
"ck.modding.corelib": "https://github.com/CoreKeeperMods/CoreLib.git?path=/Assets/CoreLibPackage#4.0.0",
```

Pinned to `#4.0.0` (not `#main`) to avoid surprise breakage from
upstream CoreLib updates. To update CoreLib later, bump the version
tag here AND in this mod's `ModManifest.json` `modDependencies` entry.

After this one-time edit, regular builds via `../utils/build.sh` work
as usual — the change stays uncommitted in the SDK repo by design.

## Note on ModManifest vs .asset

The runtime mod's CoreLib dependency is declared in
`unity/ItemChecklist.asset`'s `metadata.dependencies` (the
`ModBuilderSettings` block), not in `ModManifest.json`. Pugstorm's
`ModBuilder.BuildMod` overwrites the published `ModManifest.json`
from the `.asset` at build time, so any `modDependencies` in
the source `ModManifest.json` is silently dropped.

The schema in the `.asset` is `dependencies: [{modName, required}]`
(case-sensitive `modName`, e.g. `CoreLib`, not `corelib`).
Because `required` is a C# `bool`, Unity YAML serializes it as
`1` for true / `0` for false.
