// just run npm install

var fs = require('fs');
var { exec } = require("child_process");

function runNpmInstall() {
// current script dir
    var options = {
        cwd: __dirname
    };
    exec('npm install', options, (err, stdout, stderr) => {
        if (err || stderr) {
            console.error(`Error running npm install: ${err}`);
            console.error(`stderr: ${stderr}`);
            return;
        }
        console.log('npm install complete');
    });
}

runNpmInstall();