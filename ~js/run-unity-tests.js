const fs = require('fs');
const {exec} = require("child_process");

// Parse named arguments
const rawArgs = process.argv.slice(2);
const testMode = rawArgs[0];
let category = "All";
let unityPathArg = null;
let port = process.env.UNITY_GITHOOKS_PORT ? parseInt(process.env.UNITY_GITHOOKS_PORT, 10) : 8080;

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
        default:
            break;
    }
}

function GetUnityEditorPath(version) {

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
        RunUnity(unityPath, '.');
        return;
    });

}

function RunUnity(unityPath, projectPath) {
    console.log('Running Unity: ', unityPath);

    // handle path with spaces
    unityPath = `"${unityPath}"`;

        // http get http://localhost:8080/ and log results
        const http = require('http');
        const options = {
            hostname: 'localhost',
            port: port,
            headers: {}
        };

    // Add headers including repo path
    options.headers['repoPath'] = projectPath;
    options.headers['testMode'] = testMode;
    options.headers['testCategory'] = category;


        const req = http.get(options, (res) => {
            console.log(`statusCode: ${res.statusCode}`);

            res.on('data', (d) => {
                process.stdout.write("data: "+ d);
                if(d.includes('Tests failed')) {
                    console.error('Tests failed');
                    process.exit(1);
                }
            });
        });
        req.on('error', (error) => {
            // run unity with command line args batchmode, nographics, run tests
            exec(`${unityPath} -projectPath \"${projectPath}\" -runTests -testPlatform ${testMode} -logFile \"-\"`, (err, stdout, stderr) => {
                if (err) {
                    console.error(`Error running Unity: ${err}`);

                    return;
                }
                console.log(`stdout: ${stdout}`);
                console.log(`stderr: ${stderr}`);
            });
        });

}

// read unity project version
fs.readFile('ProjectSettings/ProjectVersion.txt', 'utf8', (err, data) => {
    if (err) {
        console.error(`Error reading file: ${err}`);
        return;
    }

    // get m_EditorVersion
    let lines = data.split('\n');
    for (let line of lines) {
        if (line.includes('m_EditorVersion:')) {
            let version = line.split(' ')[1];
            console.log('Unity version: ', version);
            GetUnityEditorPath(version);
        }
    }

});
