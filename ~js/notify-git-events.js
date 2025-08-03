#!/usr/bin/env node
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const hook = process.argv[2];
const cwd = process.cwd();
const configPath = path.join(cwd, 'gitHooksApiConfig.json');
let url = process.env.GIT_HOOKS_API_URL;
let token = process.env.GITHUB_TOKEN;

if (fs.existsSync(configPath)) {
  try {
    const cfg = JSON.parse(fs.readFileSync(configPath, 'utf8'));
    url = url || cfg.url;
    token = token || cfg.token;
  } catch (_) {}
}

if (!token) {
  try {
    token = execSync('gh auth token', { encoding: 'utf8' }).trim();
  } catch (_) {}
}

if (!url || !token) {
  process.exit(0);
}

function git(cmd) {
  try {
    return execSync(cmd, { encoding: 'utf8' }).trim();
  } catch (_) {
    return '';
  }
}

const payload = JSON.stringify({
  event: hook,
  commit: git('git rev-parse HEAD'),
  branch: git('git rev-parse --abbrev-ref HEAD'),
  repoPath: cwd
});

const http = url.startsWith('https') ? require('https') : require('http');
const req = http.request(url, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  }
}, res => {
  res.on('data', () => {});
});

req.on('error', err => {
  console.error('Failed to notify backend:', err.message);
});

req.write(payload);
req.end();
