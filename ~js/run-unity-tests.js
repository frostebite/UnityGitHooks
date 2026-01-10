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
            exec(`${unityPath} -projectPath \"${projectPath}\" -batchmode -nographics -runTests -testPlatform ${testMode} ${testFilter} -logFile -`, (err, stdout, stderr) => {
                if (err) {
                    console.error(`Error running Unity: ${err}`);
                    process.exit(1);
                    return;
                }
                if (stderr && !stderr.includes('Batchmode') && !stderr.includes('nographics')) {
                    console.error(`Unity stderr: ${stderr}`);
                }
                console.log(`Unity stdout: ${stdout}`);
                // Exit code from Unity execution indicates test results
                process.exit(err ? 1 : 0);
            });
        });
        
        req.setTimeout(300000, () => {
            console.error('Request timeout after 5 minutes');
            req.destroy();
            process.exit(1);
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
