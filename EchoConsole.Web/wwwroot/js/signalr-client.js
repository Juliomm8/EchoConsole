window.echoConsoleRealtime = (() => {
    let connection = null;
    let options = null;
    let refreshTimer = null;
    let relativeTimer = null;
    let refreshInFlight = false;
    let refreshPending = false;
    let lastRefreshCompletedAt = 0;
    const minimumRefreshIntervalMs = 1750;
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
        refreshPending = true;

        if (refreshInFlight) {
            return;
        }

        const elapsed =
            performance.now()
            - lastRefreshCompletedAt;

        const delay =
            immediate
                ? 0
                : Math.max(
                    0,
                    minimumRefreshIntervalMs
                    - elapsed);

        if (refreshTimer) {
            return;
        }

        refreshTimer = setTimeout(
            () => {
                refreshTimer = null;
                void refreshDashboard();
            },
            delay);
    }

    async function refreshDashboard() {
        if (refreshInFlight) {
            refreshPending = true;
            return;
        }

        refreshInFlight = true;
        refreshPending = false;

        try {
            const [overview, sessions] = await Promise.all([
                fetchOverview(),
                fetchLiveSessions()
            ]);

            await new Promise(resolve => {
                window.requestAnimationFrame(() => {
                    if (overview) {
                        applyOverview(overview);
                    }

                    if (sessions) {
                        currentSessions = sessions;
                        renderSessions(sessions);
                        applyDerivedKpisFromSessions(
                            sessions);
                    }

                    resolve();
                });
            });
        } catch (error) {
            console.error(
                "Dashboard refresh failed.",
                error);
        } finally {
            refreshInFlight = false;
            lastRefreshCompletedAt =
                performance.now();

            if (refreshPending) {
                scheduleRefresh(false);
            }
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
        setMetricValue(
            options.registeredInstallationsValueId,
            formatNumber(overview.registeredInstallations),
            Number(overview.registeredInstallations),
            "neutral");

        setMetricValue(
            options.activeSessionsValueId,
            formatNumber(overview.activeSessions),
            Number(overview.activeSessions),
            Number(overview.activeSessions) > 0
                ? "active"
                : "warning");

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
            setMetricValue(
                options.averageDurationValueId,
                "00:00:00",
                0,
                "warning");

            setMetricValue(
                options.latestHeartbeatValueId,
                "--",
                0,
                "warning");
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

            setMetricValue(
                options.averageDurationValueId,
                formatDurationFromMilliseconds(
                    averageMilliseconds),
                averageMilliseconds,
                "neutral");
        } else {
            setMetricValue(
                options.averageDurationValueId,
                "00:00:00",
                0,
                "warning");
        }

        const latestHeartbeat = sessions
            .map(session => parseDate(
                session.lastHeartbeatUtc))
            .filter(value => value !== null)
            .sort((left, right) =>
                right.getTime() - left.getTime())[0];

        if (latestHeartbeat) {
            setMetricValue(
                options.latestHeartbeatValueId,
                formatRelativeAge(
                    now.getTime() -
                    latestHeartbeat.getTime()),
                1,
                "neutral");
        } else {
            setMetricValue(
                options.latestHeartbeatValueId,
                "--",
                0,
                "warning");
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
                <td colspan="6" class="px-5 py-16 text-center">
                    <div class="mx-auto max-w-xl rounded border border-amber-500/20 bg-[linear-gradient(135deg,rgba(69,26,3,0.14),rgba(0,0,0,0.78))] px-6 py-8 shadow-[0_0_24px_rgba(245,158,11,0.05)]">
                        <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full border border-amber-500/25 bg-amber-500/5 text-amber-500">
                            !
                        </div>
                        <p class="mt-5 text-[11px] font-bold uppercase tracking-[0.24em] text-amber-400">
                            [ NO ACTIVE SIGNALS DETECTED ]
                        </p>
                        <p class="mt-3 text-sm leading-6 text-slate-600">
                            ${escapeHtml(getLabel("noLiveSessions", "No live sessions detected."))}
                        </p>
                        <p class="mt-5 overflow-hidden whitespace-nowrap text-[10px] tracking-[0.08em] text-slate-700">
                            [░░░░░░░░░░░░] CHANNEL_IDLE
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
                "border-b border-slate-900 bg-black transition-colors duration-150 hover:bg-slate-950/85";

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
                <td class="whitespace-nowrap px-5 py-4 text-xs font-bold uppercase tracking-[0.08em] text-[#67e8f9]">${escapeHtml(session.installationId ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-slate-300">${escapeHtml(session.currentScene ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-slate-400">${escapeHtml(session.currentGameState ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-slate-500">${escapeHtml(session.currentPhase ?? "-")}</td>
                <td class="whitespace-nowrap px-5 py-4 text-xs text-amber-500/80">${escapeHtml(lastHeartbeatLabel)}</td>
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

    function setMetricValue(
        elementId,
        displayValue,
        numericValue,
        state) {
        setText(
            elementId,
            displayValue);

        updateMetricVisualState(
            elementId,
            numericValue,
            state);

        updateMetricGauge(
            elementId,
            numericValue,
            state);
    }

    function updateMetricVisualState(
        elementId,
        numericValue,
        state) {
        if (!elementId) {
            return;
        }

        const element = document.getElementById(
            elementId);

        if (!element) {
            return;
        }

        const stateClasses = [
            "text-[#67e8f9]",
            "text-slate-200",
            "text-green-400",
            "text-amber-400"
        ];

        element.classList.remove(
            ...stateClasses);

        if (state === "warning" ||
            Number(numericValue) === 0) {
            element.classList.add(
                "text-amber-400");
            return;
        }

        if (state === "active") {
            element.classList.add(
                "text-green-400");
            return;
        }

        if (elementId ===
            options.averageDurationValueId) {
            element.classList.add(
                "text-slate-200");
            return;
        }

        element.classList.add(
            "text-[#67e8f9]");
    }

    function updateMetricGauge(
        elementId,
        numericValue,
        state) {
        if (!elementId) {
            return;
        }

        const gauge = document.getElementById(
            `${elementId}-gauge`);

        if (!gauge) {
            return;
        }

        const normalizedValue = Number.isFinite(
            Number(numericValue))
            ? Math.max(0, Number(numericValue))
            : 0;

        const percentage = state === "warning" ||
            normalizedValue === 0
            ? 0
            : Math.min(
                100,
                Math.max(
                    8,
                    Math.round(
                        normalizedValue % 101)));

        const segments = 12;
        const filledSegments = Math.round(
            percentage / 100 * segments);

        const bar =
            "█".repeat(filledSegments) +
            "░".repeat(
                segments - filledSegments);

        gauge.textContent =
            `[${bar}] ${String(percentage).padStart(2, "0")}%`;

        gauge.classList.remove(
            "text-[#22d3ee]/70",
            "text-green-500/80",
            "text-slate-500",
            "text-amber-500/80");

        if (percentage === 0) {
            gauge.classList.add(
                "text-amber-500/80");
        } else if (state === "active") {
            gauge.classList.add(
                "text-green-500/80");
        } else if (elementId ===
            options.averageDurationValueId) {
            gauge.classList.add(
                "text-slate-500");
        } else {
            gauge.classList.add(
                "text-[#22d3ee]/70");
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
