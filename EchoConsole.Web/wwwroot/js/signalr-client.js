window.echoConsoleRealtime = (() => {
    let connection = null;
    let options = null;
    let refreshTimer = null;
    let relativeTimer = null;
    let currentSessions = [];
    let relativeTimeFormatter = null;
    let numberFormatter = null;

    function init(config) {
        options = {
            webBaseUrl: normalizeBaseUrl(
                config?.webBaseUrl || window.location.origin),
            culture: config?.culture || "en",
            labels: config?.labels || {},
            tableBodyId: config.tableBodyId,
            registeredInstallationsValueId:
                config.registeredInstallationsValueId,
            activeSessionsValueId:
                config.activeSessionsValueId,
            averageDurationValueId:
                config.averageDurationValueId,
            latestHeartbeatValueId:
                config.latestHeartbeatValueId,
            serverTimeValueId:
                config.serverTimeValueId,
            topbarServerTimeValueId:
                config.topbarServerTimeValueId
        };

        relativeTimeFormatter = new Intl.RelativeTimeFormat(
            options.culture,
            {
                numeric: "always",
                style: "narrow"
            });

        numberFormatter = new Intl.NumberFormat(
            options.culture);

        if (!window.signalR) {
            console.error(
                "EchoConsole realtime: SignalR library not found.");
            return;
        }

        buildConnection();
        startConnection();
        refreshDashboard();
        startRelativeTicker();
    }

    function buildConnection() {
        const hubUrl = buildWebUrl(
            "/hubs/admin-telemetry");

        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                withCredentials: true
            })
            .withAutomaticReconnect([
                0,
                2000,
                5000,
                10000,
                15000
            ])
            .configureLogging(
                signalR.LogLevel.Warning)
            .build();

        connection.on(
            "ReceiveTelemetryUpdate",
            onTelemetryEvent);

        connection.onreconnecting(() => {
            console.warn(
                "SignalR reconnecting...");
        });

        connection.onreconnected(() => {
            console.info(
                "SignalR reconnected.");
            scheduleRefresh(true);
        });

        connection.onclose(() => {
            console.warn(
                "SignalR connection closed. Retrying...");
            setTimeout(
                startConnection,
                5000);
        });
    }

    async function startConnection() {
        if (!connection) {
            return;
        }

        try {
            await connection.start();
            console.info(
                "SignalR connected.");
            scheduleRefresh(true);
        } catch (error) {
            console.error(
                "SignalR connection failed.",
                error);
            setTimeout(
                startConnection,
                5000);
        }
    }

    function onTelemetryEvent() {
        scheduleRefresh(false);
    }

    function scheduleRefresh(immediate) {
        if (refreshTimer) {
            clearTimeout(refreshTimer);
        }

        refreshTimer = setTimeout(
            refreshDashboard,
            immediate ? 0 : 250);
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
            console.error(
                "Dashboard refresh failed.",
                error);
        }
    }

    async function fetchOverview() {
        const response = await fetch(
            buildWebUrl("/Dashboard/Overview"),
            {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

        if (!response.ok) {
            throw new Error(
                `Overview request failed with status ${response.status}`);
        }

        return await response.json();
    }

    async function fetchLiveSessions() {
        const response = await fetch(
            buildWebUrl("/Dashboard/LiveSessions"),
            {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

        if (!response.ok) {
            throw new Error(
                `Live sessions request failed with status ${response.status}`);
        }

        return await response.json();
    }

    function applyOverview(overview) {
        setText(
            options.registeredInstallationsValueId,
            formatNumber(overview.registeredInstallations));

        setText(
            options.activeSessionsValueId,
            formatNumber(overview.activeSessions));

        if (overview.serverTimeUtc) {
            const serverTime = new Date(
                overview.serverTimeUtc);

            const formatted = formatServerTime(
                serverTime);

            setText(
                options.serverTimeValueId,
                formatted);

            setText(
                options.topbarServerTimeValueId,
                formatted);
        }
    }

    function applyDerivedKpisFromSessions(sessions) {
        const now = new Date();

        if (!Array.isArray(sessions) ||
            sessions.length === 0) {
            setText(
                options.averageDurationValueId,
                "00:00:00");

            setText(
                options.latestHeartbeatValueId,
                "--");
            return;
        }

        const validDurations = sessions
            .map(session => {
                const startedAt = parseDate(
                    session.startedAtUtc);

                if (!startedAt) {
                    return null;
                }

                const durationMilliseconds =
                    now.getTime() - startedAt.getTime();

                return durationMilliseconds >= 0
                    ? durationMilliseconds
                    : null;
            })
            .filter(value => value !== null);

        if (validDurations.length > 0) {
            const averageMilliseconds =
                validDurations.reduce(
                    (sum, value) => sum + value,
                    0) / validDurations.length;

            setText(
                options.averageDurationValueId,
                formatDurationFromMilliseconds(
                    averageMilliseconds));
        } else {
            setText(
                options.averageDurationValueId,
                "00:00:00");
        }

        const latestHeartbeat = sessions
            .map(session => parseDate(
                session.lastHeartbeatUtc))
            .filter(value => value !== null)
            .sort((left, right) =>
                right.getTime() - left.getTime())[0];

        if (latestHeartbeat) {
            setText(
                options.latestHeartbeatValueId,
                formatRelativeAge(
                    now.getTime() -
                    latestHeartbeat.getTime()));
        } else {
            setText(
                options.latestHeartbeatValueId,
                "--");
        }
    }

    function renderSessions(sessions) {
        const tableBody = document.getElementById(
            options.tableBodyId);

        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";

        if (!Array.isArray(sessions) ||
            sessions.length === 0) {
            const emptyRow = document.createElement("tr");

            emptyRow.innerHTML = `
                <td colspan="6" class="px-5 py-14 text-center">
                    <div class="mx-auto max-w-lg">
                        <p class="text-[10px] font-bold uppercase tracking-[0.22em] text-green-800">
                            &gt; _ NO_ACTIVE_SIGNAL
                        </p>
                        <p class="mt-3 text-sm text-green-900">
                            ${escapeHtml(getLabel("noLiveSessions", "No live sessions detected."))}
                        </p>
                    </div>
                </td>
            `;

            tableBody.appendChild(emptyRow);
            return;
        }

        const sorted = [...sessions].sort(
            (left, right) => {
                const leftTime = parseDate(
                    left.lastHeartbeatUtc)?.getTime() ?? 0;

                const rightTime = parseDate(
                    right.lastHeartbeatUtc)?.getTime() ?? 0;

                return rightTime - leftTime;
            });

        for (const session of sorted) {
            const row = document.createElement("tr");

            row.setAttribute(
                "data-session-id",
                session.sessionId ?? "");

            row.className =
                "border-b border-green-500/10 bg-black transition-colors duration-150 hover:bg-green-500/[0.035]";

            const lastHeartbeatDate = parseDate(
                session.lastHeartbeatUtc);

            const lastHeartbeatLabel = lastHeartbeatDate
                ? formatRelativeAge(
                    Date.now() - lastHeartbeatDate.getTime())
                : "--";

            const statusLabel = mapStatusLabel(
                session.status);

            const statusClasses = mapStatusClasses(
                session.status);

            row.innerHTML = `
                <td class="whitespace-nowrap px-5 py-4 text-xs font-bold uppercase tracking-[0.08em] text-green-300">${escapeHtml(session.installationId ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-green-600">${escapeHtml(session.currentScene ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-green-600">${escapeHtml(session.currentGameState ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-green-700">${escapeHtml(session.currentPhase ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-green-800">${escapeHtml(lastHeartbeatLabel)}</td>
                <td class="whitespace-nowrap px-5 py-4">
                    <span class="inline-flex items-center gap-2 rounded border px-3 py-1.5 text-[9px] font-bold uppercase tracking-[0.16em] ${statusClasses}">
                        <span class="h-1.5 w-1.5 rounded-full ${mapStatusDotClass(session.status)}"></span>
                        [${escapeHtml(statusLabel)}]
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
            applyDerivedKpisFromSessions(
                currentSessions);

            renderSessions(
                currentSessions);
        }, 5000);
    }

    function mapStatusLabel(status) {
        switch (Number(status)) {
            case 1:
                return getLabel(
                    "statusActive",
                    "Active");
            case 2:
                return getLabel(
                    "statusEnded",
                    "Ended");
            case 3:
                return getLabel(
                    "statusExpired",
                    "Expired");
            default:
                return `${getLabel("statusUnknown", "Unknown")} (${status})`;
        }
    }

    function mapStatusClasses(status) {
        switch (Number(status)) {
            case 1:
                return "border-green-500/35 bg-green-500/10 text-green-300";
            case 2:
                return "border-amber-500/35 bg-amber-500/10 text-amber-300";
            case 3:
                return "border-rose-500/35 bg-rose-500/10 text-rose-300";
            default:
                return "border-slate-500/25 bg-slate-500/5 text-slate-400";
        }
    }

    function mapStatusDotClass(status) {
        switch (Number(status)) {
            case 1:
                return "bg-green-400";
            case 2:
                return "bg-amber-400";
            case 3:
                return "bg-rose-400";
            default:
                return "bg-slate-400";
        }
    }

    function formatDurationFromMilliseconds(milliseconds) {
        const totalSeconds = Math.max(
            0,
            Math.floor(milliseconds / 1000));

        const hours = Math.floor(
            totalSeconds / 3600);

        const minutes = Math.floor(
            (totalSeconds % 3600) / 60);

        const seconds = totalSeconds % 60;

        return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    }

    function formatRelativeAge(milliseconds) {
        const totalSeconds = Math.max(
            0,
            Math.floor(milliseconds / 1000));

        if (totalSeconds < 60) {
            return relativeTimeFormatter.format(
                -totalSeconds,
                "second");
        }

        const totalMinutes = Math.floor(
            totalSeconds / 60);

        if (totalMinutes < 60) {
            return relativeTimeFormatter.format(
                -totalMinutes,
                "minute");
        }

        const totalHours = Math.floor(
            totalMinutes / 60);

        if (totalHours < 24) {
            return relativeTimeFormatter.format(
                -totalHours,
                "hour");
        }

        const totalDays = Math.floor(
            totalHours / 24);

        return relativeTimeFormatter.format(
            -totalDays,
            "day");
    }

    function formatServerTime(date) {
        return date.toLocaleString(
            options.culture,
            {
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

        return numberFormatter.format(numeric);
    }

    function getLabel(key, fallback) {
        const value = options.labels?.[key];

        return typeof value === "string" && value.length > 0
            ? value
            : fallback;
    }

    function parseDate(value) {
        if (!value) {
            return null;
        }

        const date = new Date(value);

        return Number.isNaN(date.getTime())
            ? null
            : date;
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

    function buildWebUrl(path) {
        return `${options.webBaseUrl}${path}`;
    }

    function normalizeBaseUrl(baseUrl) {
        if (!baseUrl) {
            return "";
        }

        return baseUrl.endsWith("/")
            ? baseUrl.slice(0, -1)
            : baseUrl;
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
