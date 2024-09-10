# Easily use git hooks in Unity projects

"Unity Lefthook" comes with further utilities to handle common Unity workflow needs
- Hook management (via Lefthook)
- Run tests
- Require compiling code
- Require git plugin installed

## What is Lefthook
This tool "Unity Lefthook" requires and helps install "Lefthook", Lefthook allows you to easily set up and manage git hooks.

## Trigger Events
https://git-scm.com/docs/githooks

# How to install

## Installing a Unity Package from a Git URL

To install a Unity package from a Git URL, follow these steps:

1. **Open the Unity Package Manager**:
   - Go to `Window > Package Manager`.

2. **Add a Package from Git URL**:
   - Click the `+` button in the top left corner of the Package Manager window.
   - Select `Add package from git URL...`.

3. **Enter the Git URL**:
   - In the text box that appears, enter the Git URL of the package. For example:
     ```
     https://github.com/frostebite/UnityLefthook.git
     ```

4. **Click Add**:
   - Click the `Add` button to install the package.
  
## Install Lefthook

There are many compatible ways to do this.

Recommended:
1) Add the Lefthook package and start the editor
2) if you haven't installed Lefthook yet, you will be presented with an editor window with a button saying "Install Lefthook", press this button

You can also run the powershell command
```
winget install evilmartians.lefthook -e
```
