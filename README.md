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
        run: "node ./Library/PackageCache/com.frostebite.unitygithooks@*/~js/init-unity-lefthook.js"
      run_unity_tests_lefthook:
        run: "node ./Library/PackageCache/com.frostebite.unitygithooks@*/~js/run-unity-tests.js EditMode --category LefthookCore"
  ```
  *The `*` wildcard resolves the package's versioned directory so the path remains valid when the package updates.*
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
- **⚠️ Execution Modes:**
  - **Background Project Mode (Recommended)**: Run tests against a synced copy of your project in batchmode/headless. This is the recommended approach as it isolates test execution from your main project and avoids conflicts with active editor instances.
    - Enable via `--backgroundProject` flag, `UNITY_GITHOOKS_BACKGROUND_PROJECT_ENABLED=true` environment variable, or Unity Preferences (`Edit > Preferences > Unity Git Hooks`)
    - Configure the background project suffix via `--backgroundProjectSuffix <suffix>` flag, `UNITY_GITHOOKS_BACKGROUND_PROJECT_SUFFIX` environment variable, or Unity Preferences (default: `-BackgroundWorker`)
    - The background project will be created at `<parent-directory>/<project-name><suffix>`
    - When background project mode is enabled, Unity runs in batchmode/headless mode (no HTTP connection attempt)
    - Requires rclone to be installed (see Background Project Support section below)
  - **Active Workspace Mode (Experimental)**: Attempts to connect to a running Unity editor instance via HTTP. This mode is experimental and may have issues with active editor instances or file conflicts.
    - If your Unity instance uses a custom port for the Git hook listener, supply `--port <port>` or set the `UNITY_GITHOOKS_PORT` environment variable so the script can reach it.
    - Falls back to batchmode/headless if HTTP connection fails

#### apply-lfs-plugin-module
- Used to apply a git plugin that will pull LFS files from a local folder rather than a remote repo. Combined with RClone this can be very effective for large project storage.


#### notify-git-events
- Sends git event details to a configurable backend API.
- Configure the API URL and authentication token via `Window > GitHooks > API Config` or environment variables `GIT_HOOKS_API_URL` and `GITHUB_TOKEN`.
- If no configuration is found, the hook skips without error.

Example `lefthook.yml` usage:
```
pre-commit:
  commands:
    notify_backend:
      run: node ./Library/PackageCache/com.frostebite.unitygithooks@VERSION/~js/notify-git-events.js pre-commit
```

### Changing the listener port (Active Workspace Mode only)

**Note:** This only applies to Active Workspace Mode. Background Worker Mode does not use HTTP connections.

By default Unity Git Hooks listens on port `8080` for Active Workspace Mode connections. If this port is unavailable, open Unity's Preferences (Edit > Preferences on Windows or Unity > Preferences on macOS), select **Unity Git Hooks**, and adjust the **Port** value. When invoking `run-unity-tests.js` in Active Workspace Mode, ensure the same port is used by passing `--port <port>` or setting the `UNITY_GITHOOKS_PORT` environment variable.

### Execution Modes: Background Worker vs Active Workspace

The `run-unity-tests` script supports two execution modes for running Unity tests:

#### Background Worker Mode (Recommended)

**How it works:**
- Creates a synced copy of your project in a separate directory (e.g., `C:\Projects\MyGame-BackgroundWorker`)
- Runs Unity in batchmode/headless mode directly as a command-line process
- Tests execute in complete isolation from your main project
- No HTTP connection attempt - Unity runs directly

**Advantages:**
- ✅ Isolated test execution - doesn't interfere with your active workspace
- ✅ No conflicts with open Unity editor instances
- ✅ Clean, controlled environment for each test run
- ✅ More reliable and predictable behavior
- ✅ Doesn't require Unity editor to be running

**Requirements:**
- rclone must be installed and available in your PATH
- Additional disk space for the background project copy

#### Active Workspace Mode (Experimental)

**How it works:**
- Attempts to connect to a running Unity editor instance via HTTP on port 8080 (configurable)
- Runs tests against your active project workspace
- Falls back to batchmode/headless if HTTP connection fails

**Advantages:**
- ✅ No additional disk space required
- ✅ No rclone dependency
- ✅ Uses existing Unity editor instance (if available)

**Disadvantages:**
- ⚠️ Experimental - may have reliability issues
- ⚠️ Potential conflicts with active editor instances
- ⚠️ File conflicts if workspace is modified during execution
- ⚠️ Requires Unity editor to be running (or falls back to batchmode)
- ⚠️ HTTP connection may fail, causing fallback behavior

**Recommendation:** Use **Background Worker Mode** for production use. Active Workspace Mode is provided for testing/debugging purposes.

### Background Project Support (Recommended)

Background project mode (also known as Background Worker Mode) allows you to run lefthook actions (like Unity tests) against a synced copy of your project instead of the main project. **This is the recommended approach** as it isolates test execution from your main project, avoids conflicts with active editor instances, and runs tests in a clean, controlled environment.

**Requirements:**
- rclone must be installed and available in your PATH
- Install rclone from https://rclone.org/install/

**Configuration:**

1. **Via Unity Preferences** (recommended):
   - Open Unity Preferences (Edit > Preferences on Windows or Unity > Preferences on macOS)
   - Select **Unity Git Hooks**
   - Enable "Enable Background Project"
   - Configure "Project Suffix" (default: `-BackgroundWorker`)

2. **Via Environment Variables:**
   - `UNITY_GITHOOKS_BACKGROUND_PROJECT_ENABLED=true` - Enable background project mode
   - `UNITY_GITHOOKS_BACKGROUND_PROJECT_SUFFIX=<suffix>` - Set the project suffix (default: `-BackgroundWorker`)

3. **Via Command Line Arguments:**
   - `--backgroundProject` - Enable background project mode
   - `--backgroundProjectSuffix <suffix>` - Set the project suffix

**How it works:**
- When enabled, before running jobs, the entire repository folder is synced to `<parent-directory>/<project-name><suffix>` using rclone
- All jobs then run against the background project instead of the main project
- The sync happens automatically before each job execution
- Unity runs in batchmode/headless mode (no HTTP connection attempt - runs directly as command line)

**Example usage in lefthook.yml:**
```yaml
pre-commit:
  commands:
    run_unity_tests:
      run: node ./Library/PackageCache/com.frostebite.unitygithooks@*/~js/run-unity-tests.js EditMode --category LefthookCore --backgroundProject
```

**Note:** The background project path is calculated as `<parent-directory>/<project-name><suffix>`. For example, if your project is at `C:\Projects\MyGame`, with suffix `-BackgroundWorker`, the background project will be at `C:\Projects\MyGame-BackgroundWorker`.
