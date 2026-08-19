# Issue 9239: Java 25 and legacy Fabric compatibility

## Context

Issue #9239 reports that automatic Java selection fails for a Minecraft 1.21.4
instance, while manually selecting Java 21 succeeds. The supplied PCL log shows
that PCL selects Java 25 from the official runtime directory because it is first
in the configured Java list and satisfies the current `[21.0, +infinity)`
requirement.

Although the issue description says no mods are installed, the supplied launch
script and crash log show Fabric Loader `0.16.12`. The crash is caused by its
Mixin/ASM stack attempting to read a Java 25 class (class-file major version
69), which it does not support.

## Current implementation

- `GetJavaRequirement` in `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb`
  assigns Minecraft 1.20.5+ a lower Java bound of 21.
- Fabric instances only add lower bounds today, so Fabric 0.16.12 retains an
  unbounded upper range.
- Automatic selection walks the user-configured Java list and selects the first
  checked Java in that range. The list sorter prioritizes official runtime
  locations, making Java 25 selected before installed Java 21 entries.

## Design

For Fabric Loader versions older than `0.17.0`, add an exclusive Java 25 upper
bound (`< 25.0`) during requirement analysis. This makes Java 21--24 eligible
for Minecraft 1.21.4 while preventing the known incompatible Java 25 runtime
from being selected. Fabric Loader 0.17.0 and later retains the existing open
upper bound so compatible newer loaders may use Java 25.

The boundary is based on the loader-published dependencies: Fabric Loader
0.16.12 uses ASM 9.7.1, while 0.17.0 upgrades to ASM 9.8 with Java 25
class-file support.

The constraint is applied only in automatic selection. Explicit Java choices
and user-defined Java ranges remain authoritative, preserving the existing
escape hatches for advanced users.

## Verification

- Confirm the 1.21.4/Fabric 0.16.12 requirement becomes `[21.0, 25.0)`.
- Confirm Java 25 is skipped and Java 21 is selected when both are configured.
- Confirm a Fabric Loader version at or above 0.17.0 retains its existing
  Java-version range.
- Build the solution and review the final diff for scope and regressions.
