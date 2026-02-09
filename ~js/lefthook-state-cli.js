#!/usr/bin/env node
/**
 * CLI for lefthook state operations.
 *
 * Usage:
 *   node lefthook-state-cli.js init <hookName> <step1,step2,step3,...>
 *   node lefthook-state-cli.js start-step <stepName> [detail]
 *   node lefthook-state-cli.js finish-step <stepName> <pass|fail> [detail]
 *   node lefthook-state-cli.js skip-step <stepName> [detail]
 *   node lefthook-state-cli.js finish <passed|failed|error> [errorMessage]
 *   node lefthook-state-cli.js read
 */

const state = require('./lefthook-state');

const args = process.argv.slice(2);
const command = args[0];

if (!command) {
    console.error('Usage: lefthook-state-cli.js <command> [args...]');
    process.exit(1);
}

switch (command) {
    case 'init': {
        const hookName = args[1];
        const stepsCsv = args[2];
        if (!hookName || !stepsCsv) {
            console.error('Usage: lefthook-state-cli.js init <hookName> <step1,step2,...>');
            process.exit(1);
        }
        const steps = stepsCsv.split(',').map(s => s.trim()).filter(s => s);
        state.startHook(hookName, steps);
        console.log(`[lefthook-state] Initialized ${hookName} with ${steps.length} steps`);
        break;
    }

    case 'start-step': {
        const stepName = args[1];
        if (!stepName) {
            console.error('Usage: lefthook-state-cli.js start-step <stepName> [detail]');
            process.exit(1);
        }
        state.startStep(stepName, args[2]);
        break;
    }

    case 'finish-step': {
        const stepName = args[1];
        const result = args[2];
        if (!stepName || !result) {
            console.error('Usage: lefthook-state-cli.js finish-step <stepName> <pass|fail> [detail]');
            process.exit(1);
        }
        state.finishStep(stepName, result === 'pass', args[3]);
        break;
    }

    case 'skip-step': {
        const stepName = args[1];
        if (!stepName) {
            console.error('Usage: lefthook-state-cli.js skip-step <stepName> [detail]');
            process.exit(1);
        }
        state.skipStep(stepName, args[2]);
        break;
    }

    case 'finish': {
        const status = args[1];
        if (!status) {
            console.error('Usage: lefthook-state-cli.js finish <passed|failed|error> [errorMessage]');
            process.exit(1);
        }
        state.finishHook(status, args[2] || null, null);
        console.log(`[lefthook-state] Hook finished: ${status}`);
        break;
    }

    case 'read': {
        const current = state.readState();
        if (current) {
            console.log(JSON.stringify(current, null, 2));
        } else {
            console.log('No state file found');
        }
        break;
    }

    default:
        console.error(`Unknown command: ${command}`);
        process.exit(1);
}
