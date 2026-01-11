# Unity Package Compatibility Review

## Overview
This document reviews UnityLefthook for compatibility when used as a Unity package (via PackageCache) vs as a submodule in Assets.

## Current Status: ✅ MOSTLY COMPATIBLE

The package is already designed to work as a Unity package, with README examples showing PackageCache paths. However, there are a few considerations:

## ✅ Working Correctly

1. **package.json** - Correctly configured with `com.frostebite.unitygithooks` package name
2. **README.md** - Already includes PackageCache examples: `Library/PackageCache/com.frostebite.unitygithooks@*/~js/`
3. **C# Scripts** - Use `Application.dataPath` which works correctly regardless of package location
4. **Scripts use `__dirname`** - JavaScript files use `__dirname` which resolves correctly from PackageCache

## ⚠️ Considerations / Potential Issues

### 1. Script Execution Context
**Issue:** The Node.js scripts (especially `run-unity-tests.js`) rely on being executed from the project root directory.

**Current Behavior:**
- Scripts read `ProjectSettings/ProjectVersion.txt` using relative path
- Scripts use `path.resolve('.')` to get project root
- Lefthook runs from project root, so this works correctly

**Why it works:**
- When called from `lefthook.yml`, lefthook executes from the project root
- Relative paths resolve correctly
- The README shows correct usage patterns

**Potential issue if called directly:**
- If someone tries to run the script directly from PackageCache (e.g., `node Library/PackageCache/.../run-unity-tests.js`), `path.resolve('.')` would resolve to the wrong location
- **Mitigation:** Scripts are documented to be called via lefthook.yml, not directly

### 2. process.chdir() Usage
**Location:** `run-unity-tests.js` line 122 - `process.chdir(__dirname)` inside `GetUnityEditorPath()`

**Issue:** This changes working directory to the script's location (PackageCache), but this happens AFTER reading ProjectVersion.txt, so it's safe.

**Current Behavior:**
- ProjectVersion.txt is read BEFORE any directory changes
- Directory change only affects Unity registry query (which doesn't need project root)
- Unity execution uses explicit `-projectPath` argument, so it's not affected

**Status:** ✅ Safe - No issue

### 3. lefthook.yml Configuration
**Location:** User's project root `lefthook.yml` (not in the package)

**Current Example in README:**
```yaml
pre-commit:
  commands:
    run_unity_tests:
      run: "node ./Library/PackageCache/com.frostebite.unitygithooks@*/~js/run-unity-tests.js EditMode"
```

**Issue:** Users must use PackageCache path in their lefthook.yml, not submodule path.

**Status:** ✅ Documented correctly in README

## ✅ No Hard-Coded Paths Found

- No references to `Assets/_Game/Submodules/UnityLefthook` in the package code
- All paths are relative or use `Application.dataPath` (C#) or `__dirname`/`path.resolve('.')` (JS)
- Scripts are location-agnostic

## Recommendations

### ✅ No Changes Required
The package is already compatible with Unity Package Manager. The current implementation correctly handles both scenarios:

1. **As Submodule:** Works when scripts are in `Assets/_Game/Submodules/UnityLefthook/~js/`
2. **As Package:** Works when scripts are in `Library/PackageCache/com.frostebite.unitygithooks@*/~js/`

Both work because:
- Lefthook runs from project root
- Scripts use relative paths from execution context
- README documents correct usage for both scenarios

### 📝 Documentation Note
The README already correctly shows PackageCache examples. Consider adding a note that:
- Scripts must be called from lefthook.yml (which runs from project root)
- Direct script execution from PackageCache is not supported/recommended

## Conclusion

**Status:** ✅ **Package is compatible with Unity Package Manager**

No code changes are required. The package correctly uses relative paths and execution context, making it work in both submodule and package scenarios.
