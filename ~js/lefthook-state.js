/**
 * Lefthook State Module
 *
 * File-based state system for tracking hook execution progress.
 * State is stored in .git/lefthook-state/hook-state.json.
 *
 * Consumers: Node.js hook scripts, PowerShell automation, C# Unity editor.
 */

const fs = require('fs');
const path = require('path');
const os = require('os');

const STATE_FILENAME = 'hook-state.json';
const STATE_DIR_NAME = 'lefthook-state';

/**
 * Find the .git directory by walking up from cwd.
 */
function findGitDir() {
    let dir = process.cwd();
    const maxDepth = 10;
    for (let i = 0; i < maxDepth; i++) {
        const gitDir = path.join(dir, '.git');
        if (fs.existsSync(gitDir)) {
            // Handle .git file (worktrees) vs .git directory
            const stat = fs.statSync(gitDir);
            if (stat.isDirectory()) {
                return gitDir;
            }
            // .git is a file pointing to the real git dir
            const content = fs.readFileSync(gitDir, 'utf8').trim();
            const match = content.match(/^gitdir:\s*(.+)$/);
            if (match) {
                const resolved = path.resolve(dir, match[1]);
                if (fs.existsSync(resolved)) {
                    return resolved;
                }
            }
        }
        const parent = path.dirname(dir);
        if (parent === dir) break;
        dir = parent;
    }
    return null;
}

/**
 * Get the state directory path (.git/lefthook-state/), creating it if needed.
 * Returns null if .git cannot be found.
 */
function getStateDir() {
    const gitDir = findGitDir();
    if (!gitDir) return null;

    const stateDir = path.join(gitDir, STATE_DIR_NAME);
    if (!fs.existsSync(stateDir)) {
        fs.mkdirSync(stateDir, { recursive: true });
    }
    return stateDir;
}

/**
 * Get the path to hook-state.json.
 */
function getStatePath() {
    const stateDir = getStateDir();
    if (!stateDir) return null;
    return path.join(stateDir, STATE_FILENAME);
}

/**
 * Read the current hook state. Returns null if missing or corrupt.
 */
function readState() {
    const statePath = getStatePath();
    if (!statePath || !fs.existsSync(statePath)) return null;

    try {
        const content = fs.readFileSync(statePath, 'utf8');
        return JSON.parse(content);
    } catch (err) {
        console.warn(`[lefthook-state] Failed to read state: ${err.message}`);
        return null;
    }
}

/**
 * Write state atomically (write to .tmp then rename).
 */
function writeState(state) {
    const statePath = getStatePath();
    if (!statePath) {
        console.warn('[lefthook-state] Cannot write state: .git directory not found');
        return false;
    }

    const tmpPath = statePath + '.tmp';
    try {
        fs.writeFileSync(tmpPath, JSON.stringify(state, null, 2), 'utf8');
        fs.renameSync(tmpPath, statePath);
        return true;
    } catch (err) {
        console.warn(`[lefthook-state] Failed to write state: ${err.message}`);
        // Clean up tmp file if rename failed
        try { fs.unlinkSync(tmpPath); } catch (_) {}
        return false;
    }
}

/**
 * Initialize hook state with all steps set to "pending".
 * @param {string} hookName - e.g., "pre-commit"
 * @param {string[]} stepNames - ordered list of step names
 */
function startHook(hookName, stepNames) {
    const state = {
        hookName: hookName,
        status: 'running',
        startTime: new Date().toISOString(),
        endTime: null,
        pid: process.pid,
        machineName: os.hostname(),
        steps: stepNames.map(name => ({
            name: name,
            status: 'pending',
            startTime: null,
            endTime: null,
            detail: null
        })),
        result: null,
        error: null,
        testSummary: null
    };
    writeState(state);
    return state;
}

/**
 * Update a specific step's properties.
 * @param {string} stepName - the step to update
 * @param {object} updates - fields to merge (status, startTime, endTime, detail)
 */
function updateStep(stepName, updates) {
    const state = readState();
    if (!state) return null;

    const step = state.steps && state.steps.find(s => s.name === stepName);
    if (!step) {
        console.warn(`[lefthook-state] Step not found: ${stepName}`);
        return state;
    }

    Object.assign(step, updates);
    writeState(state);
    return state;
}

/**
 * Mark a step as "running".
 */
function startStep(stepName, detail) {
    const updates = {
        status: 'running',
        startTime: new Date().toISOString()
    };
    if (detail !== undefined) {
        updates.detail = detail;
    }
    return updateStep(stepName, updates);
}

/**
 * Mark a step as "completed" or "failed".
 */
function finishStep(stepName, success, detail) {
    const updates = {
        status: success ? 'completed' : 'failed',
        endTime: new Date().toISOString()
    };
    if (detail !== undefined) {
        updates.detail = detail;
    }
    return updateStep(stepName, updates);
}

/**
 * Mark a step as "skipped".
 */
function skipStep(stepName, detail) {
    return updateStep(stepName, {
        status: 'skipped',
        detail: detail || null
    });
}

/**
 * Finalize the hook execution.
 * @param {string} status - "passed", "failed", or "error"
 * @param {string|null} error - error message if status is "error" or "failed"
 * @param {object|null} testSummary - { passCount, failCount, testMode, category, logFile }
 */
function finishHook(status, error, testSummary) {
    const state = readState();
    if (!state) return null;

    state.status = status;
    state.endTime = new Date().toISOString();
    state.error = error || null;
    state.testSummary = testSummary || null;

    writeState(state);
    return state;
}

module.exports = {
    findGitDir,
    getStateDir,
    getStatePath,
    readState,
    writeState,
    startHook,
    updateStep,
    startStep,
    finishStep,
    skipStep,
    finishHook
};
