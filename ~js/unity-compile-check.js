#!/usr/bin/env node
/**
 * Unity Compile Check Script
 *
 * Validates that the Unity project compiles without errors.
 * Uses the MAIN project (not background project).
 *
 * Strategy:
 *   1. Try HTTP to the running Unity editor (fast, non-blocking)
 *   2. If editor unavailable, launch Unity in batchmode (slower but reliable)
 *
 * Usage:
 *   node unity-compile-check.js [--port <port>] [--unityPath <path>] [--timeout <ms>]
 *
 * Environment variables:
 *   UNITY_GITHOOKS_PORT - HTTP listener port (default: 8080)
 *   UNITY_EDITOR_PATH   - Path to Unity.exe (skips auto-detection)
 */

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const http = require('http');
const lefthookState = require('./lefthook-state');

// Parse arguments
const rawArgs = process.argv.slice(2);
let port = process.env.UNITY_GITHOOKS_PORT ? parseInt(process.env.UNITY_GITHOOKS_PORT, 10) : 8080;
let unityPathArg = process.env.UNITY_EDITOR_PATH || null;
let timeoutMs = 600000; // 10 minutes default for batchmode

for (let i = 0; i < rawArgs.length; i++) {
    switch (rawArgs[i]) {
        case '--port':
            if (i + 1 < rawArgs.length) {
                port = parseInt(rawArgs[i + 1], 10);
                i++;
            }
            break;
        case '--unityPath':
            if (i + 1 < rawArgs.length) {
                unityPathArg = rawArgs[i + 1];
                i++;
            }
            break;
        case '--timeout':
            if (i + 1 < rawArgs.length) {
                timeoutMs = parseInt(rawArgs[i + 1], 10);
                i++;
            }
            break;
    }
}

/**
 * Find Unity project root by looking for ProjectSettings/ProjectVersion.txt
 */
function findProjectRoot() {
    const cwd = process.cwd();
    if (fs.existsSync(path.join(cwd, 'ProjectSettings', 'ProjectVersion.txt'))) {
        return cwd;
    }

    let dir = __dirname;
    for (let i = 0; i < 10; i++) {
        if (fs.existsSync(path.join(dir, 'ProjectSettings', 'ProjectVersion.txt'))) {
            return dir;
        }
        const parent = path.dirname(dir);
        if (parent === dir) break;
        dir = parent;
    }

    console.warn('[compile-check] Could not find project root, using cwd:', cwd);
    return cwd;
}

/**
 * Read Unity version from ProjectSettings/ProjectVersion.txt
 */
function getUnityVersion(projectRoot) {
    const versionFile = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
    try {
        const content = fs.readFileSync(versionFile, 'utf8');
        const match = content.match(/m_EditorVersion:\s*(.+)/);
        if (match) return match[1].trim();
    } catch (err) {
        console.error(`[compile-check] Failed to read Unity version: ${err.message}`);
    }
    return null;
}

/**
 * Find Unity executable path (Windows-focused).
 * Checks: env var -> registry -> Unity Hub paths
 */
function findUnityExecutable(version) {
    // Method 1: Explicit path
    if (unityPathArg && fs.existsSync(unityPathArg)) {
        return unityPathArg;
    }

    // Method 2: Registry lookup (Windows)
    if (version && process.platform === 'win32') {
        try {
            const regOutput = execSync(
                `reg query "HKEY_LOCAL_MACHINE\\SOFTWARE\\Unity Technologies\\Installer\\${version}" /v "Location x64"`,
                { encoding: 'utf8', timeout: 5000, stdio: ['pipe', 'pipe', 'pipe'] }
            );
            const match = regOutput.match(/Location x64\s+REG_SZ\s+(.+)/);
            if (match) {
                const unityExe = path.join(match[1].trim(), 'Editor', 'Unity.exe');
                if (fs.existsSync(unityExe)) {
                    console.log(`[compile-check] Found Unity ${version} from registry`);
                    return unityExe;
                }
            }
        } catch (_) {
            // Registry lookup failed, continue
        }
    }

    // Method 3: Unity Hub paths
    const hubPaths = [];
    if (process.platform === 'win32') {
        hubPaths.push('C:\\Program Files\\Unity\\Hub\\Editor');
        if (process.env.ProgramFiles) {
            hubPaths.push(path.join(process.env.ProgramFiles, 'Unity', 'Hub', 'Editor'));
        }
    } else if (process.platform === 'darwin') {
        hubPaths.push('/Applications/Unity/Hub/Editor');
    } else {
        hubPaths.push(path.join(process.env.HOME || '', 'Unity', 'Hub', 'Editor'));
    }

    for (const hubPath of hubPaths) {
        if (!fs.existsSync(hubPath)) continue;

        // Look for specific version first
        if (version) {
            const versionDir = path.join(hubPath, version);
            const exe = process.platform === 'win32'
                ? path.join(versionDir, 'Editor', 'Unity.exe')
                : path.join(versionDir, 'Unity.app', 'Contents', 'MacOS', 'Unity');
            if (fs.existsSync(exe)) {
                console.log(`[compile-check] Found Unity ${version} in Hub`);
                return exe;
            }
        }

        // Fallback: most recent version
        try {
            const versions = fs.readdirSync(hubPath).sort().reverse();
            for (const ver of versions) {
                const exe = process.platform === 'win32'
                    ? path.join(hubPath, ver, 'Editor', 'Unity.exe')
                    : path.join(hubPath, ver, 'Unity.app', 'Contents', 'MacOS', 'Unity');
                if (fs.existsSync(exe)) {
                    console.warn(`[compile-check] Using Unity ${ver} (fallback, wanted ${version})`);
                    return exe;
                }
            }
        } catch (_) {}
    }

    return null;
}

/**
 * Try compile check via HTTP to running Unity editor.
 * Returns a promise that resolves true (success), false (compile error), or rejects (connection failed).
 */
function tryHttpCompileCheck(projectRoot) {
    return new Promise((resolve, reject) => {
        const options = {
            hostname: 'localhost',
            port: port,
            path: '/',
            method: 'GET',
            headers: {
                'command': 'compile-check',
                'repoPath': projectRoot
            },
            timeout: timeoutMs
        };

        console.log(`[compile-check] Trying HTTP compile check on port ${port}...`);

        const req = http.request(options, (res) => {
            let data = '';

            res.on('data', (chunk) => {
                const text = chunk.toString();
                process.stdout.write(text);
                data += text;
            });

            res.on('end', () => {
                if (res.statusCode === 200) {
                    console.log('\n[compile-check] Compile check passed via HTTP');
                    resolve(true);
                } else {
                    console.error(`\n[compile-check] Compile check failed (HTTP ${res.statusCode})`);
                    resolve(false);
                }
            });
        });

        req.on('error', (err) => {
            reject(err);
        });

        req.on('timeout', () => {
            req.destroy();
            reject(new Error('HTTP request timed out'));
        });

        req.end();
    });
}

/**
 * Run compile check via Unity batchmode.
 * Returns true on success, false on failure.
 */
function runBatchmodeCompileCheck(unityPath, projectRoot) {
    console.log(`[compile-check] Running Unity batchmode compile check...`);
    console.log(`[compile-check] Unity: ${unityPath}`);
    console.log(`[compile-check] Project: ${projectRoot}`);

    const cmd = `"${unityPath}" -projectPath "${projectRoot}" -batchmode -nographics -executeMethod BuildMethodEditor.CompileCheck -logFile -`;
    console.log(`[compile-check] Command: ${cmd}`);

    try {
        execSync(cmd, {
            stdio: 'inherit',
            timeout: timeoutMs,
            shell: true
        });
        console.log('[compile-check] Compile check passed via batchmode');
        return true;
    } catch (err) {
        const exitCode = err.status || 1;
        console.error(`[compile-check] Compile check failed (exit code ${exitCode})`);
        return false;
    }
}

/**
 * Main entry point.
 */
async function main() {
    const projectRoot = findProjectRoot();
    console.log(`[compile-check] Project root: ${projectRoot}`);

    lefthookState.updateStep('compile_check', {
        detail: 'Starting compile check'
    });

    // Strategy 1: Try HTTP to running editor
    try {
        const result = await tryHttpCompileCheck(projectRoot);
        if (result) {
            lefthookState.finishStep('compile_check', true);
            process.exit(0);
        } else {
            lefthookState.finishStep('compile_check', false, 'Compilation errors detected');
            process.exit(1);
        }
    } catch (httpErr) {
        console.log(`[compile-check] Editor HTTP not available (${httpErr.message}), falling back to batchmode...`);
    }

    // Strategy 2: Batchmode fallback
    const version = getUnityVersion(projectRoot);
    if (!version) {
        console.error('[compile-check] Cannot determine Unity version');
        lefthookState.finishStep('compile_check', false, 'Cannot determine Unity version');
        process.exit(1);
    }

    const unityExe = findUnityExecutable(version);
    if (!unityExe) {
        console.error(`[compile-check] Cannot find Unity ${version} executable`);
        lefthookState.finishStep('compile_check', false, `Unity ${version} not found`);
        process.exit(1);
    }

    lefthookState.updateStep('compile_check', {
        detail: `Batchmode compile check (Unity ${version})`
    });

    const success = runBatchmodeCompileCheck(unityExe, projectRoot);
    lefthookState.finishStep('compile_check', success, success ? null : 'Compilation errors in batchmode');
    process.exit(success ? 0 : 1);
}

main().catch(err => {
    console.error(`[compile-check] Unexpected error: ${err.message}`);
    lefthookState.finishStep('compile_check', false, `Unexpected error: ${err.message}`);
    process.exit(1);
});
