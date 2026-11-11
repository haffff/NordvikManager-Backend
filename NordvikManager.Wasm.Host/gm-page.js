// The GM's page for the browser host: log in to Central, pick or create a game, start its
// session. The host itself runs in this tab (HostPage.cs); everything here goes through its
// [JSExport]s. Kept small on purpose: the full GM interface is the player client, which the
// game's owner opens to join their own game as GM.

let host;

const $ = (selector) => document.querySelector(selector);

function el(tag, attrs = {}, ...children) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attrs)) {
        if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
        else node.setAttribute(key, value);
    }
    node.append(...children);
    return node;
}

async function call(method, path, body) {
    return JSON.parse(await host.Call(method, path, body === undefined ? null : JSON.stringify(body)));
}

function status(text, kind = "info") {
    const line = $("#status");
    line.textContent = text;
    line.dataset.kind = kind;
    console.log(`[gm-page] ${text}`);
}

function renderLogin() {
    const form = el("form", { id: "login", class: "card" },
        el("h2", {}, "Log in"),
        el("label", {}, "User name", el("input", { id: "username", name: "username", autocomplete: "username", required: "" })),
        el("label", {}, "Password", el("input", { id: "password", name: "password", type: "password", autocomplete: "current-password", required: "" })),
        el("button", { id: "login-submit", type: "submit" }, "Log in"));
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        status("Logging in…");
        if (await host.Login($("#username").value, $("#password").value)) {
            status(`Logged in as ${host.UserName()}.`, "ok");
            await renderGames();
        } else {
            status("Wrong user name or password, or Central can't be reached.", "error");
        }
    });
    $("#main").replaceChildren(form);
}

async function renderGames() {
    const games = await call("GET", "api/GameList/GetGames");
    const list = el("ul", { id: "games" });
    for (const game of games.body ?? []) {
        list.append(el("li", { "data-game": game.id },
            el("span", { class: "name" }, game.name),
            el("button", { class: "start", onclick: () => startGame(game) }, "Start session"),
            el("button", { class: "stop", onclick: () => stopGame(game) }, "Stop")));
    }
    // The addons shipped with the host (Basics), offered ticked as in the GM frontend.
    const featured = await call("GET", "api/GameList/GetFeaturedAddons");
    const addons = (featured.body ?? []).filter((addon) => addon.builtIn);
    const create = el("form", { id: "create", class: "card" },
        el("h2", {}, "New game"),
        el("label", {}, "Name", el("input", { id: "game-name", name: "game-name", required: "" })),
        ...addons.map((addon) => el("label", { class: "addon" },
            el("span", {}, el("input", { type: "checkbox", name: "addon", value: addon.key, checked: "" }), ` ${addon.name ?? addon.key}`),
            el("span", { class: "muted" }, addon.description ?? ""))),
        el("button", { id: "create-submit", type: "submit" }, "Create game"));
    create.addEventListener("submit", async (e) => {
        e.preventDefault();
        const addonsSelected = [...create.querySelectorAll('input[name="addon"]:checked')].map((box) => box.value);
        const result = await call("POST", "api/GameList/AddGame", { name: $("#game-name").value, passwordRequired: false, addonsSelected });
        if (result.status === 200) status(`Created "${$("#game-name").value}".`, "ok");
        else status(`Could not create the game: ${result.body?.error ?? result.status}`, "error");
        await renderGames();
    });
    $("#main").replaceChildren(
        el("section", { class: "card" }, el("h2", {}, "Your games"), games.body?.length ? list : el("p", {}, "No games yet.")),
        create,
        el("button", { id: "logout", onclick: () => { host.Logout(); status("Logged out."); renderLogin(); } }, "Log out"));
}

async function startGame(game) {
    status(`Starting "${game.name}"…`);
    const result = await call("POST", `api/Session/${game.id}/start`);
    if (result.status === 200)
        status(`"${game.name}" is running: Central session ${result.body.centralSessionId}. Keep this tab open while you play.`, "ok");
    else
        status(`Could not start "${game.name}": ${result.body?.error ?? result.status}`, "error");
}

async function stopGame(game) {
    const result = await call("POST", `api/Session/${game.id}/stop`);
    status(result.status === 200 ? `"${game.name}" stopped.` : `Could not stop "${game.name}": ${result.status}`, result.status === 200 ? "ok" : "error");
}

export function start(centralUrl) {
    globalThis.getDotnetRuntime(0).getAssemblyExports("NordvikManager.Wasm.Host").then((exports) => {
        host = exports.NordvikManager.Wasm.Host.HostPage;
        // Hidden or closing: save what changed since the last periodic save.
        document.addEventListener("visibilitychange", () => { if (document.visibilityState === "hidden") host.SaveNow(); });
        $("#central").textContent = centralUrl;
        renderLogin();
        status("Ready.");
    });
}
