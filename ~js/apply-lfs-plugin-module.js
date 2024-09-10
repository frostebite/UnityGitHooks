const fs = require('fs');



const STRING_TO_ADD = `[lfs]
repositoryformatversion = 0
standalonetransferagent = lfs-folder
[lfs "customtransfer.lfs-folder"]
path = lfs-folderstore
args = '` + process.argv[4] + `'`;

const configFileMain = process.argv[2];

function HandleModule(configFile){
    console.log("Config file: ", configFile);
    // pwd
    console.log("PWD: ", process.cwd());

    fs.readFile(configFile, 'utf8', (err, data) => {
        if (err) {
            console.error(`Error reading file: ${err}`);
            return;
        }

        if (!data.includes(STRING_TO_ADD)) {
            fs.appendFile(configFile, `\n${STRING_TO_ADD}`, (err) => {
                if (err) {
                    console.error(`Error appending to file: ${err}`);
                    return;
                }
                console.log(`Added string to ${configFile}`);
            });
        } else {
            console.log(`String already present in ${configFile}`);
        }
    });
}

HandleModule(configFileMain);


// get submodules from .gitmodules

fs.readFile ('.gitmodules', 'utf8', (err, data) => {
    if (err) {
        console.error(`Error reading file: ${err}`);
        return;
    }

    console.log(data);
    var lines = data.split('\n');
    // log line count
    console.log('Line count: ', lines.length);
    
    // find each line with [submodule
    for (let line of lines) {
        if (line.includes('submodule')) {
            console.log('Found submodule: ', line);

            // get the path
            let path = line.split(' ')[1];
            path = path.substring(1, path.length - 2);
            console.log('Path: ', path);

            // HandleModule
            HandleModule(path + '/.git/config');
        }
    }
});