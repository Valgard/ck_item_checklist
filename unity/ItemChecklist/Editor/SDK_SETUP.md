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
