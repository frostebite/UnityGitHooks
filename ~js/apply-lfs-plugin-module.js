const fs = require('fs');
const { execSync } = require('child_process');

const configFileMain = process.argv[2];
const transferArgs = process.argv[3];

function handleModule(configFile) {
    console.log('Config file: ', configFile);
    try {
        execSync(`git config --file "${configFile}" --replace-all lfs.repositoryformatversion 0`);
        execSync(`git config --file "${configFile}" --replace-all lfs.standalonetransferagent lfs-folder`);
        execSync(`git config --file "${configFile}" --replace-all lfs.customtransfer.lfs-folder.path lfs-folderstore`);
        execSync(`git config --file "${configFile}" --replace-all lfs.customtransfer.lfs-folder.args "${transferArgs}"`);
        console.log(`Configured LFS plugin for ${configFile}`);
    } catch (err) {
        console.error(`Error configuring ${configFile}: ${err.message}`);
    }
}

handleModule(configFileMain);

// get submodules from .gitmodules
fs.readFile('.gitmodules', 'utf8', (err, data) => {
    if (err) {
        console.error(`Error reading file: ${err}`);
        return;
    }

    const lines = data.split('\n');
    for (const line of lines) {
        if (line.includes('submodule')) {
            let path = line.split(' ')[1];
            path = path.substring(1, path.length - 2);
            handleModule(`${path}/.git/config`);
        }
    }
});

