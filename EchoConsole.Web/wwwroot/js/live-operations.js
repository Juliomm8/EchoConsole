window.echoConsoleLiveOperations = (() => {
    let connection = null;
    let options = null;
    let refreshTimer = null;
    let pollingTimer = null;
    let refreshInProgress = false;
    let serverTimeUtc = null;
    let localSyncTime = null;
    let relativeTimeFormatter = null;
    let numberFormatter = null;

    function init(config) {
        options = {
            hubUrl: config.hubUrl,
            snapshotUrl: config.snapshotUrl,
            refreshIntervalMilliseconds:
                config.refreshIntervalMilliseconds || 15000,
            culture: config.culture || "en",
            labels: config.labels || {}
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
            setConnectionState("Unavailable");
            startPolling();
            startServerClock();
            refreshSnapshot();
            return;
        }

        buildConnection();
        startConnection();
        startPolling();
        startServerClock();
        refreshSnapshot();
    }

    function buildConnection() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl, {
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
            "LiveOperationsRefresh",
            () => {
                scheduleRefresh(250);
            });

        connection.onreconnecting(() => {
            setConnectionState("Reconnecting");
        });

        connection.onreconnected(() => {
            setConnectionState("Connected");
            scheduleRefresh(0);
        });

        connection.onclose(() => {
            setConnectionState("Disconnected");

            setTimeout(
                startConnection,
                5000);
        });
    }

    async function startConnection() {
        if (!connection ||
            connection.state !==
                signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            setConnectionState("Connecting");

            await connection.start();

            setConnectionState("Connected");
            scheduleRefresh(0);
        } catch (error) {
            console.error(
                "Live operations SignalR connection failed.",
                error);

            setConnectionState("Disconnected");

            setTimeout(
                startConnection,
                5000);
        }
    }

    function startPolling() {
        if (pollingTimer) {
            clearInterval(pollingTimer);
        }

        pollingTimer = setInterval(
            refreshSnapshot,
            options.refreshIntervalMilliseconds);
    }

    function startServerClock() {
        setInterval(() => {
            if (!serverTimeUtc || !localSyncTime) {
                return;
            }

            const elapsed =
                Date.now() - localSyncTime.getTime();

            const currentServerTime =
                new Date(
                    serverTimeUtc.getTime() + elapsed);

            setText(
                "live-ops-server-time",
                formatUtcDateTime(currentServerTime));
        }, 1000);
    }

    function scheduleRefresh(delay) {
        if (refreshTimer) {
            clearTimeout(refreshTimer);
        }

        refreshTimer = setTimeout(
            refreshSnapshot,
            delay);
    }

    async function refreshSnapshot() {
        if (refreshInProgress) {
            return;
        }

        refreshInProgress = true;

        try {
            const response = await fetch(
                options.snapshotUrl,
                {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        "Accept": "application/json"
                    }
                });

            if (!response.ok) {
                throw new Error(
                    `Snapshot request failed with status ${response.status}`);
            }

            const snapshot = await response.json();

            applySnapshot(snapshot);
        } catch (error) {
            console.error(
                "Live operations refresh failed.",
                error);
        } finally {
            refreshInProgress = false;
        }
    }

    function applySnapshot(snapshot) {
        animateCounter(
            "live-ops-active-installations",
            snapshot.activeInstallations);

        animateCounter(
            "live-ops-degraded-installations",
            snapshot.degradedInstallations);

        animateCounter(
            "live-ops-inactive-installations",
            snapshot.inactiveInstallations);

        animateCounter(
            "live-ops-active-sessions",
            snapshot.activeSessions);

        animateCounter(
            "live-ops-events-5",
            snapshot.eventsLast5Minutes);

        animateCounter(
            "live-ops-events-15",
            snapshot.eventsLast15Minutes);

        animateCounter(
            "live-ops-unresolved-alerts",
            snapshot.unresolvedAlerts);

        setText(
            "live-ops-alert-rate",
            Number(
                snapshot.alertRatePerMinute || 0)
                .toLocaleString(
                    options.culture,
                    {
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2
                    }));

        setText(
            "live-ops-total-installations",
            formatNumber(
                snapshot.totalInstallations));

        applySpikeState(snapshot);

        renderInstallations(
            snapshot.installations || []);

        syncServerTime(
            snapshot.serverTimeUtc);

        setText(
            "live-ops-last-refresh",
            formatUtcTime(
                new Date(snapshot.serverTimeUtc)));
    }

    function applySpikeState(snapshot) {
        const state =
            snapshot.eventSpikeState || "Quiet";

        const card = document.getElementById(
            "live-ops-spike-card");

        const value = document.getElementById(
            "live-ops-event-spike");

        const detail = document.getElementById(
            "live-ops-event-spike-detail");

        if (value) {
            value.textContent = localizeSpikeState(state);
        }

        const current =
            Number(snapshot.eventsLast5Minutes || 0);

        const previous =
            Number(snapshot.previousFiveMinuteEvents || 0);

        const multiplier =
            Number(snapshot.eventSpikeMultiplier || 0);

        if (detail) {
            detail.textContent =
                `${formatNumber(current)} ${getLabel("current", "current")} · ${formatNumber(previous)} ${getLabel("previous", "previous")} · x${multiplier.toFixed(2)}`;
        }

        if (!card || !value) {
            return;
        }

        if (state === "Spike") {
            card.className =
                "rounded-2xl border border-rose-400/40 bg-rose-500/10 p-6 shadow-[0_0_30px_rgba(251,113,133,0.12)]";

            value.className =
                "mt-3 font-display text-3xl uppercase text-rose-300 animate-pulse";

            return;
        }

        if (state === "Elevated") {
            card.className =
                "rounded-2xl border border-amber-400/30 bg-amber-500/10 p-6";

            value.className =
                "mt-3 font-display text-3xl uppercase text-amber-300";

            return;
        }

        if (state === "Normal") {
            card.className =
                "rounded-2xl border border-cyan-500/20 bg-slate-950/70 p-6";

            value.className =
                "mt-3 font-display text-3xl uppercase text-cyan-300";

            return;
        }

        card.className =
            "rounded-2xl border border-slate-700 bg-slate-950/70 p-6";

        value.className =
            "mt-3 font-display text-3xl uppercase text-slate-400";
    }

    function renderInstallations(installations) {
        const grid = document.getElementById(
            "live-ops-installation-grid");

        const empty = document.getElementById(
            "live-ops-empty-state");

        if (!grid || !empty) {
            return;
        }

        grid.innerHTML = "";

        setText(
            "live-ops-visible-installations",
            formatNumber(installations.length));

        if (installations.length === 0) {
            empty.classList.remove("hidden");
            return;
        }

        empty.classList.add("hidden");

        for (const installation of installations) {
            const state = normalizeState(
                installation.operationalState);

            const classes = getStateClasses(state);

            const lastUpdate = formatRelativeAge(
                installation.lastUpdateUtc);

            const lastHeartbeat =
                installation.lastHeartbeatUtc
                    ? formatRelativeAge(
                        installation.lastHeartbeatUtc)
                    : getLabel(
                        "noHeartbeat",
                        "No heartbeat");

            const article = document.createElement("article");

            article.className =
                `rounded-2xl border ${classes.border} ${classes.background} p-5 transition duration-300 hover:-translate-y-1`;

            article.innerHTML = `
                <div class="flex items-start justify-between gap-4">
                    <div class="min-w-0">
                        <div class="truncate text-base font-semibold text-white">
                            ${escapeHtml(installation.deviceName || "-")}
                        </div>

                        <div class="mt-1 text-xs text-slate-500">
                            ${escapeHtml(installation.platform || "-")} · ${escapeHtml(installation.buildVersion || "-")}
                        </div>
                    </div>

                    <span class="inline-flex shrink-0 items-center gap-2 rounded-full border border-current/20 px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] ${classes.text}">
                        <span class="h-2 w-2 rounded-full ${classes.dot} ${state === "Active" ? "animate-pulse" : ""}"></span>
                        ${escapeHtml(localizeOperationalState(state))}
                    </span>
                </div>

                <div class="mt-5 grid grid-cols-2 gap-3">
                    <div class="rounded-xl border border-slate-800/80 bg-slate-950/50 p-3">
                        <div class="text-[10px] uppercase tracking-[0.18em] text-slate-600">
                            ${escapeHtml(getLabel("scene", "Scene"))}
                        </div>

                        <div class="mt-1 truncate text-sm text-slate-200">
                            ${escapeHtml(installation.currentScene || "-")}
                        </div>
                    </div>

                    <div class="rounded-xl border border-slate-800/80 bg-slate-950/50 p-3">
                        <div class="text-[10px] uppercase tracking-[0.18em] text-slate-600">
                            ${escapeHtml(getLabel("gameState", "Game State"))}
                        </div>

                        <div class="mt-1 truncate text-sm text-slate-200">
                            ${escapeHtml(installation.currentGameState || "-")}
                        </div>
                    </div>
                </div>

                <div class="mt-4 flex items-center justify-between gap-4 text-xs text-slate-600">
                    <span>${escapeHtml(getLabel("update", "Update"))}: ${escapeHtml(lastUpdate)}</span>
                    <span>${escapeHtml(lastHeartbeat)}</span>
                </div>

                <div class="mt-3 truncate text-xs text-slate-700"
                     title="${escapeHtml(installation.installationId || "")}">
                    ${escapeHtml(installation.installationId || "-")}
                </div>
            `;

            grid.appendChild(article);
        }
    }

    function setConnectionState(state) {
        const badge = document.getElementById(
            "live-ops-connection-badge");

        const dot = document.getElementById(
            "live-ops-connection-dot");

        const text = document.getElementById(
            "live-ops-connection-text");

        if (text) {
            text.textContent = localizeConnectionState(state);
        }

        if (!badge || !dot || !text) {
            return;
        }

        if (state === "Connected") {
            badge.className =
                "inline-flex items-center gap-3 rounded-2xl border border-emerald-500/25 bg-emerald-500/10 px-5 py-3";

            dot.className =
                "h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse";

            text.className =
                "mt-1 text-sm font-semibold text-emerald-300";

            return;
        }

        if (state === "Reconnecting" ||
            state === "Connecting") {
            badge.className =
                "inline-flex items-center gap-3 rounded-2xl border border-amber-500/25 bg-amber-500/10 px-5 py-3";

            dot.className =
                "h-2.5 w-2.5 rounded-full bg-amber-400 animate-pulse";

            text.className =
                "mt-1 text-sm font-semibold text-amber-300";

            return;
        }

        badge.className =
            "inline-flex items-center gap-3 rounded-2xl border border-rose-500/25 bg-rose-500/10 px-5 py-3";

        dot.className =
            "h-2.5 w-2.5 rounded-full bg-rose-400";

        text.className =
            "mt-1 text-sm font-semibold text-rose-300";
    }

    function syncServerTime(value) {
        const parsed = new Date(value);

        if (Number.isNaN(parsed.getTime())) {
            return;
        }

        serverTimeUtc = parsed;
        localSyncTime = new Date();

        setText(
            "live-ops-server-time",
            formatUtcDateTime(parsed));
    }

    function animateCounter(id, targetValue) {
        const element = document.getElementById(id);

        if (!element) {
            return;
        }

        const startValue = Number(
            element.textContent
                .replaceAll(",", "")
                .replaceAll(".", "")
                .trim()) || 0;

        const target = Number(targetValue || 0);
        const startedAt = performance.now();
        const duration = 450;

        function update(now) {
            const progress = Math.min(
                1,
                (now - startedAt) / duration);

            const eased =
                1 - Math.pow(1 - progress, 3);

            const value = Math.round(
                startValue +
                (target - startValue) * eased);

            element.textContent = formatNumber(value);

            if (progress < 1) {
                requestAnimationFrame(update);
            }
        }

        requestAnimationFrame(update);
    }

    function getStateClasses(state) {
        if (state === "Active") {
            return {
                border: "border-emerald-400/30",
                background: "bg-emerald-500/10",
                text: "text-emerald-300",
                dot: "bg-emerald-400"
            };
        }

        if (state === "Degraded") {
            return {
                border: "border-amber-400/30",
                background: "bg-amber-500/10",
                text: "text-amber-300",
                dot: "bg-amber-400"
            };
        }

        return {
            border: "border-slate-700",
            background: "bg-slate-900/70",
            text: "text-slate-400",
            dot: "bg-slate-500"
        };
    }

    function normalizeState(value) {
        if (value === "Active" ||
            value === "Degraded") {
            return value;
        }

        return "Inactive";
    }

    function localizeOperationalState(state) {
        if (state === "Active") {
            return getLabel("stateActive", "Active");
        }

        if (state === "Degraded") {
            return getLabel("stateDegraded", "Degraded");
        }

        return getLabel("stateInactive", "Inactive");
    }

    function localizeSpikeState(state) {
        if (state === "Spike") {
            return getLabel("spikeSpike", "Spike");
        }

        if (state === "Elevated") {
            return getLabel("spikeElevated", "Elevated");
        }

        if (state === "Normal") {
            return getLabel("spikeNormal", "Normal");
        }

        return getLabel("spikeQuiet", "Quiet");
    }

    function localizeConnectionState(state) {
        if (state === "Connected") {
            return getLabel("connectionConnected", "Connected");
        }

        if (state === "Reconnecting") {
            return getLabel("connectionReconnecting", "Reconnecting");
        }

        if (state === "Connecting") {
            return getLabel("connectionConnecting", "Connecting");
        }

        if (state === "Unavailable") {
            return getLabel("connectionUnavailable", "Unavailable");
        }

        return getLabel("connectionDisconnected", "Disconnected");
    }

    function formatRelativeAge(value) {
        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "--";
        }

        const seconds = Math.max(
            0,
            Math.floor(
                (Date.now() - date.getTime()) / 1000));

        if (seconds < 60) {
            return relativeTimeFormatter.format(
                -seconds,
                "second");
        }

        const minutes = Math.floor(seconds / 60);

        if (minutes < 60) {
            return relativeTimeFormatter.format(
                -minutes,
                "minute");
        }

        const hours = Math.floor(minutes / 60);

        return relativeTimeFormatter.format(
            -hours,
            "hour");
    }

    function formatUtcDateTime(value) {
        return value
            .toISOString()
            .replace("T", " ")
            .substring(0, 19);
    }

    function formatUtcTime(value) {
        return value
            .toISOString()
            .substring(11, 19) + " UTC";
    }

    function formatNumber(value) {
        return numberFormatter.format(
            Number(value || 0));
    }

    function getLabel(key, fallback) {
        const value = options.labels?.[key];

        return typeof value === "string" && value.length > 0
            ? value
            : fallback;
    }

    function setText(id, value) {
        const element = document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
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
