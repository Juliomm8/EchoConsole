window.echoConsoleRealtime = (() => {
    let connection = null;
    let options = null;
    let refreshTimer = null;
    let relativeTimer = null;
    let currentSessions = [];

    function init(config) {
        options = {
            apiBaseUrl: normalizeBaseUrl(config.apiBaseUrl),
            tableBodyId: config.tableBodyId,
            registeredInstallationsValueId: config.registeredInstallationsValueId,
            activeSessionsValueId: config.activeSessionsValueId,
            averageDurationValueId: config.averageDurationValueId,
            latestHeartbeatValueId: config.latestHeartbeatValueId,
            serverTimeValueId: config.serverTimeValueId,
            topbarServerTimeValueId: config.topbarServerTimeValueId
        };

        if (!options.apiBaseUrl) {
            console.error("EchoConsole realtime: apiBaseUrl is missing.");
            return;
        }

        if (!window.signalR) {
            console.error("EchoConsole realtime: SignalR library not found.");
            return;
        }

        buildConnection();
        startConnection();
        refreshDashboard();
        startRelativeTicker();
    }

    function buildConnection() {
        const hubUrl = buildApiUrl("/hubs/telemetry");

        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                withCredentials: true
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("ReceiveTelemetryUpdate", onTelemetryEvent);
        connection.on("sessionStarted", onTelemetryEvent);
        connection.on("sessionHeartbeat", onTelemetryEvent);
        connection.on("sessionEnded", onTelemetryEvent);
        connection.on("sessionExpired", onTelemetryEvent);
        connection.on("installationUpdated", onTelemetryEvent);

        connection.onreconnecting(() => {
            console.warn("SignalR reconnecting...");
        });

        connection.onreconnected(() => {
            console.info("SignalR reconnected.");
            scheduleRefresh(true);
        });

        connection.onclose(() => {
            console.warn("SignalR connection closed. Retrying...");
            setTimeout(startConnection, 5000);
        });
    }

    async function startConnection() {
        if (!connection) {
            return;
        }

        try {
            await connection.start();
            console.info("SignalR connected.");
            scheduleRefresh(true);
        } catch (error) {
            console.error("SignalR connection failed.", error);
            setTimeout(startConnection, 5000);
        }
    }

    function onTelemetryEvent() {
        scheduleRefresh(false);
    }

    function scheduleRefresh(immediate) {
        if (refreshTimer) {
            clearTimeout(refreshTimer);
        }

        refreshTimer = setTimeout(() => {
            refreshDashboard();
        }, immediate ? 0 : 250);
    }

    async function refreshDashboard() {
        try {
            const [overview, sessions] = await Promise.all([
                fetchOverview(),
                fetchLiveSessions()
            ]);

            if (overview) {
                applyOverview(overview);
            }

            if (sessions) {
                currentSessions = sessions;
                renderSessions(sessions);
                applyDerivedKpisFromSessions(sessions);
            }
        } catch (error) {
            console.error("Dashboard refresh failed.", error);
        }
    }

    async function fetchOverview() {
        const url = buildApiUrl("/api/admin/dashboard/overview");
        const response = await fetch(url, {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        });

        if (!response.ok) {
            throw new Error(`Overview request failed with status ${response.status}`);
        }

        return await response.json();
    }

    async function fetchLiveSessions() {
        const url = buildApiUrl("/api/admin/dashboard/live-sessions");
        const response = await fetch(url, {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        });

        if (!response.ok) {
            throw new Error(`Live sessions request failed with status ${response.status}`);
        }

        return await response.json();
    }

    function applyOverview(overview) {
        setText(options.registeredInstallationsValueId, formatNumber(overview.registeredInstallations));
        setText(options.activeSessionsValueId, formatNumber(overview.activeSessions));

        if (overview.serverTimeUtc) {
            const serverTime = new Date(overview.serverTimeUtc);
            const formatted = formatServerTime(serverTime);
            setText(options.serverTimeValueId, formatted);
            setText(options.topbarServerTimeValueId, formatted);
        }
    }

    function applyDerivedKpisFromSessions(sessions) {
        const now = new Date();

        if (!Array.isArray(sessions) || sessions.length === 0) {
            setText(options.averageDurationValueId, "00:00:00");
            setText(options.latestHeartbeatValueId, "--");
            return;
        }

        const validDurations = sessions
            .map(x => {
                const startedAt = parseDate(x.startedAtUtc);
                if (!startedAt) {
                    return null;
                }

                const durationMs = now.getTime() - startedAt.getTime();
                return durationMs >= 0 ? durationMs : null;
            })
            .filter(x => x !== null);

        if (validDurations.length > 0) {
            const avgMs = validDurations.reduce((sum, value) => sum + value, 0) / validDurations.length;
            setText(options.averageDurationValueId, formatDurationFromMilliseconds(avgMs));
        } else {
            setText(options.averageDurationValueId, "00:00:00");
        }

        const latestHeartbeat = sessions
            .map(x => parseDate(x.lastHeartbeatUtc))
            .filter(x => x !== null)
            .sort((a, b) => b.getTime() - a.getTime())[0];

        if (latestHeartbeat) {
            const ageMs = now.getTime() - latestHeartbeat.getTime();
            setText(options.latestHeartbeatValueId, formatRelativeAge(ageMs));
        } else {
            setText(options.latestHeartbeatValueId, "--");
        }
    }

    function renderSessions(sessions) {
        const tableBody = document.getElementById(options.tableBodyId);

        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";

        if (!Array.isArray(sessions) || sessions.length === 0) {
            const emptyRow = document.createElement("tr");
            emptyRow.innerHTML = `
                <td colspan="6" class="px-5 py-6 text-center text-sm text-slate-400">
                    No live sessions detected.
                </td>
            `;
            tableBody.appendChild(emptyRow);
            return;
        }

        const sorted = [...sessions].sort((a, b) => {
            const left = parseDate(a.lastHeartbeatUtc)?.getTime() ?? 0;
            const right = parseDate(b.lastHeartbeatUtc)?.getTime() ?? 0;
            return right - left;
        });

        for (const session of sorted) {
            const row = document.createElement("tr");
            row.setAttribute("data-session-id", session.sessionId ?? "");
            row.className = "transition-colors duration-150 hover:bg-slate-900/70";

            const lastHeartbeatDate = parseDate(session.lastHeartbeatUtc);
            const lastHeartbeatLabel = lastHeartbeatDate
                ? formatRelativeAge(Date.now() - lastHeartbeatDate.getTime())
                : "--";

            const statusLabel = mapStatusLabel(session.status);
            const statusClasses = mapStatusClasses(session.status);

            row.innerHTML = `
                <td class="px-5 py-4 text-sm text-cyan-300">${escapeHtml(session.installationId ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-200">${escapeHtml(session.currentScene ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-200">${escapeHtml(session.currentGameState ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-300">${escapeHtml(session.currentPhase ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-400">${escapeHtml(lastHeartbeatLabel)}</td>
                <td class="px-5 py-4">
                    <span class="inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] ${statusClasses}">
                        <span class="h-2 w-2 rounded-full ${mapStatusDotClass(session.status)}"></span>
                        ${escapeHtml(statusLabel)}
                    </span>
                </td>
            `;

            tableBody.appendChild(row);
        }
    }

    function startRelativeTicker() {
        if (relativeTimer) {
            clearInterval(relativeTimer);
        }

        relativeTimer = setInterval(() => {
            applyDerivedKpisFromSessions(currentSessions);
        }, 5000);
    }

    function mapStatusLabel(status) {
        const numericStatus = Number(status);

        switch (numericStatus) {
            case 1:
                return "Active";
            case 2:
                return "Ended";
            case 3:
                return "Expired";
            default:
                return `Unknown (${status})`;
        }
    }

    function mapStatusClasses(status) {
        const numericStatus = Number(status);

        switch (numericStatus) {
            case 1:
                return "border-emerald-500/30 bg-emerald-500/10 text-emerald-400";
            case 2:
                return "border-amber-500/30 bg-amber-500/10 text-amber-400";
            case 3:
                return "border-rose-500/30 bg-rose-500/10 text-rose-400";
            default:
                return "border-slate-500/30 bg-slate-500/10 text-slate-300";
        }
    }

    function mapStatusDotClass(status) {
        const numericStatus = Number(status);

        switch (numericStatus) {
            case 1:
                return "bg-emerald-400";
            case 2:
                return "bg-amber-400";
            case 3:
                return "bg-rose-400";
            default:
                return "bg-slate-400";
        }
    }

    function formatDurationFromMilliseconds(milliseconds) {
        const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;

        return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    }

    function formatRelativeAge(milliseconds) {
        const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));

        if (totalSeconds < 60) {
            return `${totalSeconds}s ago`;
        }

        const totalMinutes = Math.floor(totalSeconds / 60);
        if (totalMinutes < 60) {
            return `${totalMinutes}m ago`;
        }

        const totalHours = Math.floor(totalMinutes / 60);
        if (totalHours < 24) {
            return `${totalHours}h ago`;
        }

        const totalDays = Math.floor(totalHours / 24);
        return `${totalDays}d ago`;
    }

    function formatServerTime(date) {
        return date.toLocaleString("en-GB", {
            year: "numeric",
            month: "short",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false,
            timeZone: "UTC"
        }) + " UTC";
    }

    function formatNumber(value) {
        const numeric = Number(value);
        if (Number.isNaN(numeric)) {
            return "0";
        }

        return numeric.toLocaleString("en-US");
    }

    function parseDate(value) {
        if (!value) {
            return null;
        }

        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    function setText(elementId, value) {
        if (!elementId) {
            return;
        }

        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = value;
        }
    }

    function buildApiUrl(path) {
        return `${options.apiBaseUrl}${path}`;
    }

    function normalizeBaseUrl(baseUrl) {
        if (!baseUrl) {
            return "";
        }

        return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
    }

    function pad(value) {
        return String(value).padStart(2, "0");
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    return {
        init
    };
})();