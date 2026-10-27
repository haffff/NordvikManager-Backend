NordvikManager - game server
============================

Starting
--------
Windows: double-click NordvikManager.exe.
Linux:   run ./NordvikManager in a terminal.

A window opens and shows the address of your server (usually http://localhost:8213).
Your browser opens it on its own; if it doesn't, copy the address into the browser.
Keep the window open while you play. To stop the server, close the window or press Ctrl+C.

The first account that logs in becomes this server's administrator.

What's in this folder
---------------------
NordvikManager(.exe)  the server
appsettings.json      settings (ports, database, Central Server address)
wwwroot/              the web app your browser loads
BuiltInAddons/        addons offered when you create a game
data/                 YOUR DATA: the database, uploaded files, backups, logs.
                      Created on the first start. Keep it when you update.

Updating
--------
Stop the server, unzip the new version into a new folder, copy your data/ folder (and
appsettings.json, if you changed it) into the new one, and start the new NordvikManager.
Don't unzip over this folder: it would replace your appsettings.json with the default one.

Something went wrong?
---------------------
If the server can't start, the window says why and waits for Enter.
Logs are in data/logs - include the newest file when you ask for help.

Options
-------
--no-browser                   don't open the browser on start
"OpenBrowser": false           the same, set in appsettings.json

Help: https://nordvikmanager.pl/user-guide
