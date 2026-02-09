#!/usr/bin/env node
/**
 * Wrapper script that tracks step execution in lefthook state.
 *
 * Usage:
 *   node run-with-state.js <stepName> -- <command> [args...]
 *
 * Updates the step to "running" before executing the command,
 * then "completed" or "failed" based on exit code.
 *
 * If the command is not provided or empty, the step is marked as "skipped".
 */

const { execSync } = require('child_process');
const state = require('./lefthook-state');

const args = process.argv.slice(2);
const separatorIndex = args.indexOf('--');

if (separatorIndex < 0 || args.length === 0) {
    console.error('[run-with-state] Usage: run-with-state.js <stepName> -- <command> [args...]');
    process.exit(1);
}

const stepName = args.slice(0, separatorIndex).join(' ');
const commandParts = args.slice(separatorIndex + 1);

if (!stepName) {
    console.error('[run-with-state] Step name is required');
    process.exit(1);
}

if (commandParts.length === 0 || commandParts.join(' ').trim() === '') {
    state.skipStep(stepName, 'No command provided');
    process.exit(0);
}

const command = commandParts.join(' ');

// Mark step as running
state.startStep(stepName);

try {
    execSync(command, {
        stdio: 'inherit',
        shell: true
    });

    state.finishStep(stepName, true);
    process.exit(0);
} catch (err) {
    const exitCode = err.status || 1;
    state.finishStep(stepName, false, `Exit code: ${exitCode}`);
    process.exit(exitCode);
}
