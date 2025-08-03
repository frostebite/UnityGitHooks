# Easily use git hooks in Unity projects

"Unity Git Hooks" comes with further options for common Unity workflow needs:
- Automatically find Unity installation folder and correct project editor version
- Open editor or immediately run jobs in the editor if already open
- Trigger any command-line tool, Unity test or Unity editor script
- Pre-made checks requiring compiling code to commit or push code
- Enforce local (git plugins)
- Tidy management of many Hooks (via Lefthook)

## What is Lefthook
"Unity Git Hooks" requires and helps you install "Lefthook", Lefthook allows you to easily set up and manage git hooks.

## All Supported Git Event Triggers
https://git-scm.com/docs/githooks

# Getting Started

## Installing a Unity Package from a Git URL

To install a Unity package from a Git URL, follow these steps:

1. **Open the Unity Package Manager**:
   - Go to `Window > Package Manager`.

2. **Add a Package from Git URL**:
   - Click the `+` button in the top left corner of the Package Manager window.
   - Select `Add package from git URL...`.

3. **Enter the Git URL**:
   - ```
     https://github.com/frostebite/UnityGitHooks.git#1.0.5
     ```

4. **Click Add**
  
## Install Lefthook

Warning:
- _You may need to restart your machine after installing Lefthook for Windows to recognize the installation!_
- _Every project contributor will be presented with the prompt and must install Lefthook on each development machine._

### Editor (Recommended)

1) Add the Lefthook package and start the editor
2) if you haven't installed Lefthook yet, you will be presented with an editor window with a button saying "Install Lefthook", press this button

__Powershell__
```
winget install evilmartians.lefthook -e
```
__NPM__
```
npm install lefthook --save-dev
```
__Manual__

https://github.com/evilmartians/lefthook

## Configure git hooks with Lefthook

1) Run ```lefthook install``` command or via editor prompt button to generate a new `lefthook.yml`
2) Example workflow
  ```
  pre-commit:
    parallel: false
    commands:
      init_unity_lefthook:
        run: node ./Library/PackageCache/com.frostebite.unitygithooks@0037422a62/~js/init-unity-lefthook.js
      run_unity_tests_lefthook:
        run: node ./Library/PackageCache/com.frostebite.unitygithooks@0037422a62/~js/run-unity-tests.js EditMode --category LefthookCore
  ```
3) push your new `lefthook.yml` for other project contributors to git!

Also
- [See more Lefthook examples](https://github.com/evilmartians/lefthook?tab=readme-ov-file#why-lefthook)

### Configuration options

#### init-unity-lefthook
- required, installs required NPM modules for Unity Lefthook.

#### run-unity-tests
- Allows you to run playmode or editmode tests with an optional `--category` filter
- Override the detected Unity editor path using `--unityPath <path>` when needed
- You can specifically run the `EditMode` test category `LefthookCore` to enforce your project is compiling locally and the installation of this tool is correct.
- If your Unity instance uses a custom port for the Git hook listener, supply `--port <port>` or set the `UNITY_GITHOOKS_PORT` environment variable so the script can reach it.

#### apply-lfs-plugin-module
- Used to apply a git plugin that will pull LFS files from a local folder rather than a remote repo. Combined with RClone this can be very effective for large project storage.



### Changing the listener port

By default Unity Git Hooks listens on port `8080`. If this port is unavailable, open Unity's Preferences (Edit > Preferences on Windows or Unity > Preferences on macOS), select **Unity Git Hooks**, and adjust the **Port** value. When invoking `run-unity-tests.js`, ensure the same port is used by passing `--port <port>` or setting the `UNITY_GITHOOKS_PORT` environment variable.
