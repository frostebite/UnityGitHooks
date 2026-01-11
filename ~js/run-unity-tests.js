const fs = require('fs');
const path = require('path');
const {exec} = require("child_process");

// Parse named arguments
const rawArgs = process.argv.slice(2);
const testMode = rawArgs[0];
let category = "All";
let unityPathArg = null;
let port = process.env.UNITY_GITHOOKS_PORT ? parseInt(process.env.UNITY_GITHOOKS_PORT, 10) : 8080;
let useBackgroundProject = process.env.UNITY_GITHOOKS_BACKGROUND_PROJECT_ENABLED === "true";
let backgroundProjectSuffix = process.env.UNITY_GITHOOKS_BACKGROUND_PROJECT_SUFFIX || "-BackgroundWorker";

for (let i = 1; i < rawArgs.length; i++) {
    switch (rawArgs[i]) {
        case "--category":
            if (i + 1 < rawArgs.length) {
                category = rawArgs[i + 1];
                i++;
            }
            break;
        case "--unityPath":
            if (i + 1 < rawArgs.length) {
                unityPathArg = rawArgs[i + 1];
                i++;
            }
            break;
        case "--port":
            if (i + 1 < rawArgs.length) {
                port = parseInt(rawArgs[i + 1], 10);
                i++;
            }
            break;
        case "--backgroundProject":
            useBackgroundProject = true;
            break;
        case "--backgroundProjectSuffix":
            if (i + 1 < rawArgs.length) {
                backgroundProjectSuffix = rawArgs[i + 1];
                i++;
            }
            break;
        default:
            break;
    }
}

// Find Unity project root by looking for ProjectSettings/ProjectVersion.txt
// Works from any execution context (project root, PackageCache, etc.)
function FindProjectRoot() {
    // Method 1: Check current working directory (works when called from lefthook.yml)
    const cwd = process.cwd();
    const cwdVersionFile = path.join(cwd, 'ProjectSettings', 'ProjectVersion.txt');
    if (fs.existsSync(cwdVersionFile)) {
        return cwd;
    }
    
    // Method 2: Walk up from script location (works when script is in PackageCache or submodule)
    let currentDir = __dirname;
    const maxDepth = 10; // Limit search depth
    for (let i = 0; i < maxDepth; i++) {
        const versionFile = path.join(currentDir, 'ProjectSettings', 'ProjectVersion.txt');
        if (fs.existsSync(versionFile)) {
            return currentDir;
        }
        
        const parentDir = path.dirname(currentDir);
        if (parentDir === currentDir) {
            // Reached filesystem root
            break;
        }
        currentDir = parentDir;
    }
    
    // Method 3: Walk up from current working directory (fallback)
    let checkDir = cwd;
    for (let i = 0; i < maxDepth; i++) {
        const versionFile = path.join(checkDir, 'ProjectSettings', 'ProjectVersion.txt');
        if (fs.existsSync(versionFile)) {
            return checkDir;
        }
        
        const parentDir = path.dirname(checkDir);
        if (parentDir === checkDir) {
            break;
        }
        checkDir = parentDir;
    }
    
    // Fallback: return current working directory (may cause errors, but better than failing immediately)
    console.warn('[UnityLefthook] Could not find ProjectSettings/ProjectVersion.txt, using current directory:', cwd);
    return cwd;
}

// Sync to background project if enabled
function SyncToBackgroundProject(projectRoot, callback) {
    if (!useBackgroundProject) {
        callback(null, projectRoot);
        return;
    }

    console.log('[UnityLefthook] Background project mode enabled');
    
    // Check if rclone is available
    exec('rclone version', (err, stdout, stderr) => {
        if (err) {
            console.error('[UnityLefthook] rclone not found. Background project mode requires rclone to be installed.');
            console.error('[UnityLefthook] Please install rclone from https://rclone.org/install/');
            process.exit(1);
            return;
        }

        const projectName = path.basename(projectRoot);
        const parentDir = path.dirname(projectRoot);
        const backgroundProjectPath = path.join(parentDir, projectName + backgroundProjectSuffix);

        console.log('[UnityLefthook] Syncing project to background project...');
        console.log(`  Source: ${projectRoot}`);
        console.log(`  Destination: ${backgroundProjectPath}`);

        // Ensure destination directory exists
        if (!fs.existsSync(backgroundProjectPath)) {
            fs.mkdirSync(backgroundProjectPath, { recursive: true });
            console.log(`[UnityLefthook] Created destination directory: ${backgroundProjectPath}`);
        }

        // Use rclone sync to sync the entire repo folder
        // Sync everything (no exclusions) as requested
        const rcloneArgs = [
            'sync',
            `"${projectRoot}"`,
            `"${backgroundProjectPath}"`,
            '--progress'
        ];

        const rcloneCmd = `rclone ${rcloneArgs.join(' ')}`;
        console.log(`[UnityLefthook] Running: ${rcloneCmd}`);

        exec(rcloneCmd, (err, stdout, stderr) => {
            if (err) {
                console.error(`[UnityLefthook] rclone sync failed: ${err.message}`);
                if (stderr) {
                    console.error(`[UnityLefthook] rclone stderr: ${stderr}`);
                }
                process.exit(1);
                return;
            }

            if (stdout) {
                console.log(stdout);
            }
            console.log('[UnityLefthook] Sync completed successfully');
            callback(null, backgroundProjectPath);
        });
    });
}

function GetUnityEditorPath(version, projectPath) {
    // install winreg npm
    // install winreg in the script dir
    var options = {
        cwd: __dirname
    };
    // log version
    version = version.trim();
    console.log('Unity version:', version);
    let unityVersion = version;
    // set cwd so child processes run in this directory
    process.chdir(__dirname);
    exec(`cmd /c reg query "HKEY_LOCAL_MACHINE\\SOFTWARE\\Unity Technologies\\Installer\\${unityVersion}" /v "Location x64"`,
        options, (err, stdout, stderr) => {
        if (err) {
            console.error(`Failed to query Unity Editor path from registry: ${err.message}`);
            return;
        }
        if (stderr) {
            console.error(`Registry query stderr: ${stderr}`);
            return;
        }

        let unityPath = null;

        // parse stdout
        let lines = stdout.split('\n');
        lines.forEach(item => {
            if (item.includes('Location x64')) {
                // get the path
                let path = item.split('Location x64')[1].trim();
                path = path.split(' ');
                // remove first item then join with spaces
                path.shift();
                path = path.join(' ');
                // remove leading spaces
                path = path.trim();
                unityPath = path;
                console.log('Unity Editor path found in registry: ', unityPath);
            }
        }
        );

        if (unityPathArg != null && unityPath == null) {
            unityPath = unityPathArg;
            console.log('Using Unity Editor path from command line: ', unityPath);
        }

        if(unityPath == null) {
            console.error('Unity Editor path not found');
            return;
        }

        unityPath += '/Editor/Unity.exe';
        // Skip HTTP and run in batchmode when background project is enabled
        RunUnity(unityPath, projectPath || '.', useBackgroundProject);
        return;
    });

}

function RunUnity(unityPath, projectPath, skipHttp = false) {
    console.log('Running Unity: ', unityPath);
    console.log('Project Path: ', projectPath);

    // handle path with spaces
    unityPath = `"${unityPath}"`;

    // When background project is enabled, skip HTTP and run directly in batchmode
    if (skipHttp) {
        console.log('[UnityLefthook] Background project mode: Running Unity in batchmode/headless...');
        RunUnityBatchmode(unityPath, projectPath);
        return;
    }

    // Try HTTP first for normal mode
    const http = require('http');
    const options = {
        hostname: 'localhost',
        port: port,
        headers: {}
    };

    // Add headers including repo path
    options.headers['repoPath'] = projectPath;
    options.headers['testMode'] = testMode;
    // Category can be comma-separated for multiple categories (e.g., "Category1,Category2")
    // Unity Test Runner will match tests that have ALL specified categories
    options.headers['testCategory'] = category;

    const req = http.get(options, (res) => {
        console.log(`statusCode: ${res.statusCode}`);

        let dataBuffer = '';
        let exitCode = 0;

        res.on('data', (d) => {
            const data = d.toString();
            process.stdout.write(data);
            dataBuffer += data;
            
            if (dataBuffer.includes('Tests failed') || res.statusCode >= 400) {
                exitCode = 1;
            }
        });

        res.on('end', () => {
            if (exitCode !== 0 || res.statusCode >= 400) {
                console.error('Tests failed or error occurred');
                process.exit(exitCode || 1);
            } else {
                console.log('Tests completed successfully');
                process.exit(0);
            }
        });

        res.on('error', (error) => {
            console.error(`Response error: ${error.message}`);
            process.exit(1);
        });
    });
    
    req.on('error', (error) => {
        console.log(`Unity Editor listener not available (${error.message}), running Unity in batch mode...`);
        RunUnityBatchmode(unityPath, projectPath);
    });
    
    req.setTimeout(300000, () => {
        console.error('Request timeout after 5 minutes');
        req.destroy();
        process.exit(1);
    });
}

function RunUnityBatchmode(unityPath, projectPath) {
    // run unity with command line args batchmode, nographics, run tests
    // Unity's testFilter syntax uses semicolons for multiple categories (e.g., "category1;category2")
    // But we receive comma-separated, so convert commas to semicolons for Unity command line
    let testFilter = "";
    if (category !== "All") {
        // Convert comma-separated categories to semicolon-separated for Unity testFilter
        const categories = category.split(',').map(c => c.trim()).filter(c => c.length > 0);
        const unityFilter = categories.join(';');
        testFilter = `-testFilter "category=${unityFilter}"`;
    }
    
    const unityCmd = `${unityPath} -projectPath \"${projectPath}\" -batchmode -nographics -runTests -testPlatform ${testMode} ${testFilter} -logFile -`;
    console.log(`[UnityLefthook] Running Unity in batchmode: ${unityCmd}`);
    
    exec(unityCmd, (err, stdout, stderr) => {
        if (err) {
            console.error(`Error running Unity: ${err}`);
            process.exit(1);
            return;
        }
        if (stderr && !stderr.includes('Batchmode') && !stderr.includes('nographics')) {
            console.error(`Unity stderr: ${stderr}`);
        }
        if (stdout) {
            console.log(`Unity stdout: ${stdout}`);
        }
        // Exit code from Unity execution indicates test results
        process.exit(err ? 1 : 0);
    });
}

// read unity project version
// First try relative path (maintains backward compatibility when called from project root via lefthook.yml)
fs.readFile('ProjectSettings/ProjectVersion.txt', 'utf8', (err, data) => {
    let projectRoot;
    let versionFile;
    
    // If the relative path doesn't work, try to find project root automatically
    if (err) {
        console.log('[UnityLefthook] ProjectSettings/ProjectVersion.txt not found in current directory, searching for project root...');
        projectRoot = FindProjectRoot();
        versionFile = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
        
        // Try reading from detected project root
        try {
            data = fs.readFileSync(versionFile, 'utf8');
            err = null;
            console.log(`[UnityLefthook] Found project root: ${projectRoot}`);
        } catch (readErr) {
            console.error(`Error reading file: ${versionFile}`);
            console.error(`Error: ${readErr.message}`);
            console.error(`Project root: ${projectRoot}`);
            process.exit(1);
            return;
        }
    } else {
        // Original behavior: use current working directory as project root
        projectRoot = path.resolve('.');
        versionFile = 'ProjectSettings/ProjectVersion.txt';
    }
    
    if (err) {
        console.error(`Error reading file: ${versionFile}`);
        console.error(`Error: ${err.message}`);
        process.exit(1);
        return;
    }

    // get m_EditorVersion
    let lines = data.split('\n');
    for (let line of lines) {
        if (line.includes('m_EditorVersion:')) {
            let version = line.split(' ')[1];
            console.log('Unity version: ', version);
            
            // Sync to background project if enabled, then get Unity path
            SyncToBackgroundProject(projectRoot, (err, finalProjectPath) => {
                if (err) {
                    console.error(`[UnityLefthook] Background project sync error: ${err}`);
                    process.exit(1);
                    return;
                }
                
                // Store the final project path for use in RunUnity
                GetUnityEditorPath(version, finalProjectPath);
            });
        }
    }

});
