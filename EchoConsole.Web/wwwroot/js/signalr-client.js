window.echoConsoleLiveMonitoring = (() => {
    let connection;
    let tableBody;
    let activeSessionsValue;
    let serverTimeValue;

    function init(options) {
        tableBody = document.getElementById(options.tableBodyId);
        activeSessionsValue = document.getElementById(options.activeSessionsValueId);
        serverTimeValue = document.getElementById(options.serverTimeValueId);

        if (!window.signalR || !tableBody) {
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on("sessionStarted", onSessionStarted);
        connection.on("sessionHeartbeat", onSessionHeartbeat);
        connection.on("sessionEnded", onSessionRemoved);
        connection.on("sessionExpired", onSessionRemoved);

        connection.start()
            .then(updateActiveSessionsCount)
            .catch(console.error);
    }

    function onSessionStarted(payload) {
        const existing = findRow(payload.sessionId);

        if (existing) {
            updateRow(existing, payload);
            updateServerTime();
            updateActiveSessionsCount();
            return;
        }

        const row = document.createElement("tr");
        row.setAttribute("data-session-id", payload.sessionId);
        row.className = "transition-colors duration-150 hover:bg-slate-900/70";

        row.innerHTML = `
            <td class="px-5 py-4 text-sm text-cyan-300">${payload.installationId ?? "-"}</td>
            <td class="px-5 py-4 text-sm text-slate-200 cell-current-scene">${payload.currentScene ?? "-"}</td>
            <td class="px-5 py-4 text-sm text-slate-200 cell-game-state">${payload.currentGameState ?? "-"}</td>
            <td class="px-5 py-4 text-sm text-slate-300 cell-current-phase">${payload.currentPhase ?? "-"}</td>
            <td class="px-5 py-4 text-sm text-slate-400 cell-last-heartbeat">just now</td>
            <td class="px-5 py-4">
                <span class="inline-flex items-center gap-2 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-emerald-400 cell-status">
                    <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                    Active
                </span>
            </td>
        `;

        tableBody.prepend(row);
        updateServerTime();
        updateActiveSessionsCount();
    }

    function onSessionHeartbeat(payload) {
        const row = findRow(payload.sessionId);

        if (!row) {
            onSessionStarted(payload);
            return;
        }

        updateRow(row, payload);
        row.querySelector(".cell-last-heartbeat").textContent = "just now";
        updateServerTime();
        updateActiveSessionsCount();
    }

    function onSessionRemoved(payload) {
        const row = findRow(payload.sessionId);

        if (!row) {
            updateServerTime();
            updateActiveSessionsCount();
            return;
        }

        row.remove();
        updateServerTime();
        updateActiveSessionsCount();
    }

    function updateRow(row, payload) {
        const currentSceneCell = row.querySelector(".cell-current-scene");
        const gameStateCell = row.querySelector(".cell-game-state");
        const currentPhaseCell = row.querySelector(".cell-current-phase");
        const statusCell = row.querySelector(".cell-status");

        if (currentSceneCell) {
            currentSceneCell.textContent = payload.currentScene ?? "-";
        }

        if (gameStateCell) {
            gameStateCell.textContent = payload.currentGameState ?? "-";
        }

        if (currentPhaseCell) {
            currentPhaseCell.textContent = payload.currentPhase ?? "-";
        }

        if (statusCell) {
            statusCell.innerHTML = `
                <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                Active
            `;
        }
    }

    function findRow(sessionId) {
        return tableBody.querySelector(`[data-session-id="${sessionId}"]`);
    }

    function updateActiveSessionsCount() {
        if (!activeSessionsValue || !tableBody) {
            return;
        }

        const count = tableBody.querySelectorAll("tr").length;
        activeSessionsValue.textContent = count.toString();
    }

    function updateServerTime() {
        if (!serverTimeValue) {
            return;
        }

        const now = new Date();
        const formatted = now.toLocaleString("en-US", {
            month: "short",
            day: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false
        });

        serverTimeValue.textContent = formatted;
    }

    return { init };
})();