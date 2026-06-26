(() => {
    "use strict";

    const root = document.querySelector(
        "[data-profile-dashboard]"
    );

    if (!root) {
        return;
    }

    const endpoints = {
        live: root.dataset.liveUrl,
        statistics: root.dataset.statisticsUrl,
        hub: root.dataset.hubUrl
    };

    const labels = {
        online: root.dataset.labelOnline ?? "ONLINE",
        degraded:
            root.dataset.labelDegraded ?? "DEGRADED",
        offline:
            root.dataset.labelOffline ?? "OFFLINE",
        noActiveSession:
            root.dataset.labelNoActiveSession ??
            "NO ACTIVE SESSION",
        notAvailable:
            root.dataset.labelNotAvailable ?? "N/A",
        minutes:
            root.dataset.labelMinutes ?? "m",
        hours:
            root.dataset.labelHours ?? "h",
        retrying:
            root.dataset.labelRetrying ?? "RETRYING",
        eventLogEmpty:
            root.dataset.labelEventLogEmpty ??
            "NO RECENT TELEMETRY EVENTS",
        sessionStarted:
            root.dataset.labelSessionStarted ??
            "SESSION STARTED",
        sessionEnded:
            root.dataset.labelSessionEnded ??
            "SESSION ENDED",
        sessionExpired:
            root.dataset.labelSessionExpired ??
            "SESSION EXPIRED"
    };

    const connectedLiveReconcileMilliseconds =
        60000;

    const disconnectedLiveReconcileMilliseconds =
        10000;

    const statisticsReconcileMilliseconds =
        300000;

    const statisticsDebounceMilliseconds =
        10000;

    const maximumEventLogEntries = 5;

    let connection = null;
    let reconnectTimerId = null;
    let liveTimerId = null;
    let statisticsTimerId = null;
    let statisticsDebounceTimerId = null;
    let realtimeConnected = false;
    let currentLiveState = null;
    let lastHeartbeatValue = null;
    let recentEvents = [];

    initialize();

    function initialize() {
        void refreshLive();
        void refreshStatistics();
        initializeRealtime();

        document.addEventListener(
            "visibilitychange",
            () => {
                if (document.hidden) {
                    clearPollTimers();
                    return;
                }

                void refreshLive();
                void refreshStatistics();

                if (
                    connection &&
                    connection.state ===
                        signalR.HubConnectionState.Disconnected
                ) {
                    void startRealtimeConnection();
                }
            }
        );

        window.addEventListener(
            "beforeunload",
            () => {
                clearAllTimers();

                if (connection) {
                    void connection.stop();
                }
            }
        );
    }

    function initializeRealtime() {
        if (
            !window.signalR ||
            !endpoints.hub
        ) {
            realtimeConnected = false;
            scheduleLiveRefresh();
            return;
        }

        connection =
            new signalR.HubConnectionBuilder()
                .withUrl(
                    endpoints.hub,
                    {
                        withCredentials: true
                    })
                .withAutomaticReconnect([
                    0,
                    2000,
                    5000,
                    10000,
                    20000
                ])
                .configureLogging(
                    signalR.LogLevel.Warning
                )
                .build();

        connection.on(
            "ProfileTelemetryUpdate",
            handleTelemetryUpdate
        );

        connection.onreconnecting(() => {
            realtimeConnected = false;
            setSyncState(false);
            scheduleLiveRefresh();
        });

        connection.onreconnected(() => {
            realtimeConnected = true;
            clearReconnectTimer();
            void refreshLive();
            void refreshStatistics();
        });

        connection.onclose(() => {
            realtimeConnected = false;
            setSyncState(false);
            scheduleRealtimeReconnect();
            scheduleLiveRefresh();
        });

        void startRealtimeConnection();
    }

    async function startRealtimeConnection() {
        if (
            !connection ||
            connection.state !==
                signalR.HubConnectionState.Disconnected
        ) {
            return;
        }

        try {
            await connection.start();
            realtimeConnected = true;
            clearReconnectTimer();
            setSyncState(true);
            scheduleLiveRefresh();
        } catch {
            realtimeConnected = false;
            setSyncState(false);
            scheduleRealtimeReconnect();
        }
    }

    function scheduleRealtimeReconnect() {
        if (
            reconnectTimerId !== null ||
            document.hidden
        ) {
            return;
        }

        reconnectTimerId =
            window.setTimeout(
                () => {
                    reconnectTimerId = null;
                    void startRealtimeConnection();
                },
                5000
            );
    }

    function handleTelemetryUpdate(envelope) {
        if (!envelope) {
            return;
        }

        const eventType =
            String(envelope.eventType ?? "");

        const payload =
            envelope.payload ?? {};

        const serverTimeUtc =
            envelope.serverTimeUtc ??
            new Date().toISOString();

        switch (eventType) {
            case "sessionStarted":
                currentLiveState = {
                    connectionStatus: "Online",
                    hasActiveSession: true,
                    activeSessionId:
                        payload.sessionId ?? null,
                    currentScene:
                        payload.currentScene ??
                        labels.notAvailable,
                    currentGameState:
                        payload.currentGameState ??
                        labels.notAvailable,
                    currentPhase:
                        payload.currentPhase ??
                        labels.notAvailable,
                    sessionStartedAtUtc:
                        payload.startedAtUtc ??
                        serverTimeUtc,
                    lastHeartbeatUtc:
                        payload.startedAtUtc ??
                        serverTimeUtc,
                    serverTimeUtc
                };

                renderLive(currentLiveState);
                prependEvent({
                    id:
                        `started:${payload.sessionId ?? serverTimeUtc}`,
                    eventType:
                        labels.sessionStarted,
                    scene:
                        payload.currentScene ??
                        labels.notAvailable,
                    occurredAtUtc:
                        payload.startedAtUtc ??
                        serverTimeUtc
                });
                scheduleStatisticsRefresh();
                break;

            case "sessionHeartbeat":
                currentLiveState = {
                    ...(currentLiveState ?? {}),
                    connectionStatus: "Online",
                    hasActiveSession: true,
                    activeSessionId:
                        payload.sessionId ??
                        currentLiveState?.activeSessionId ??
                        null,
                    currentScene:
                        payload.currentScene ??
                        currentLiveState?.currentScene ??
                        labels.notAvailable,
                    currentGameState:
                        payload.currentGameState ??
                        currentLiveState?.currentGameState ??
                        labels.notAvailable,
                    currentPhase:
                        payload.currentPhase ??
                        currentLiveState?.currentPhase ??
                        labels.notAvailable,
                    sessionStartedAtUtc:
                        payload.startedAtUtc ??
                        currentLiveState?.sessionStartedAtUtc ??
                        null,
                    lastHeartbeatUtc:
                        payload.lastHeartbeatUtc ??
                        serverTimeUtc,
                    serverTimeUtc
                };

                renderLive(currentLiveState);
                break;

            case "sessionEventRecorded":
                currentLiveState = {
                    ...(currentLiveState ?? {}),
                    connectionStatus: "Online",
                    hasActiveSession: true,
                    activeSessionId:
                        payload.sessionId ??
                        currentLiveState?.activeSessionId ??
                        null,
                    currentScene:
                        payload.scene ??
                        currentLiveState?.currentScene ??
                        labels.notAvailable,
                    currentGameState:
                        payload.gameState ??
                        currentLiveState?.currentGameState ??
                        labels.notAvailable,
                    currentPhase:
                        payload.phase ??
                        currentLiveState?.currentPhase ??
                        labels.notAvailable,
                    sessionStartedAtUtc:
                        payload.startedAtUtc ??
                        currentLiveState?.sessionStartedAtUtc ??
                        null,
                    lastHeartbeatUtc:
                        payload.createdAtUtc ??
                        serverTimeUtc,
                    serverTimeUtc
                };

                renderLive(currentLiveState);

                prependEvent({
                    id:
                        payload.eventId ??
                        `event:${payload.sessionId ?? ""}:${payload.createdAtUtc ?? serverTimeUtc}`,
                    eventType:
                        payload.eventType ??
                        "TelemetryEvent",
                    scene:
                        payload.scene ??
                        labels.notAvailable,
                    occurredAtUtc:
                        payload.createdAtUtc ??
                        serverTimeUtc
                });

                scheduleStatisticsRefresh();
                break;

            case "sessionEnded":
                currentLiveState = {
                    ...(currentLiveState ?? {}),
                    connectionStatus: "Degraded",
                    hasActiveSession: false,
                    activeSessionId: null,
                    lastHeartbeatUtc:
                        payload.endedAtUtc ??
                        serverTimeUtc,
                    serverTimeUtc
                };

                renderLive(currentLiveState);

                prependEvent({
                    id:
                        `ended:${payload.sessionId ?? serverTimeUtc}`,
                    eventType:
                        labels.sessionEnded,
                    scene:
                        currentLiveState.currentScene ??
                        labels.notAvailable,
                    occurredAtUtc:
                        payload.endedAtUtc ??
                        serverTimeUtc
                });

                scheduleLiveRefresh(1000);
                scheduleStatisticsRefresh(1500);
                break;

            case "sessionExpired":
                currentLiveState = {
                    ...(currentLiveState ?? {}),
                    connectionStatus: "Degraded",
                    hasActiveSession: false,
                    activeSessionId: null,
                    lastHeartbeatUtc:
                        payload.lastHeartbeatUtc ??
                        serverTimeUtc,
                    serverTimeUtc
                };

                renderLive(currentLiveState);

                prependEvent({
                    id:
                        `expired:${payload.sessionId ?? serverTimeUtc}`,
                    eventType:
                        labels.sessionExpired,
                    scene:
                        currentLiveState.currentScene ??
                        labels.notAvailable,
                    occurredAtUtc:
                        payload.lastHeartbeatUtc ??
                        serverTimeUtc
                });

                scheduleLiveRefresh(1000);
                scheduleStatisticsRefresh(1500);
                break;
        }

        setSyncState(true);
    }

    async function refreshLive() {
        clearLiveTimer();

        if (document.hidden) {
            return;
        }

        try {
            const data = await requestJson(
                endpoints.live
            );

            currentLiveState = data;
            renderLive(data);
            setSyncState(true);
        } catch {
            setSyncState(false);
        } finally {
            scheduleLiveRefresh();
        }
    }

    async function refreshStatistics() {
        clearStatisticsTimer();

        if (document.hidden) {
            return;
        }

        try {
            const data = await requestJson(
                endpoints.statistics
            );

            renderStatistics(data);
        } catch {
            setSyncState(false);
        } finally {
            statisticsTimerId =
                window.setTimeout(
                    () => {
                        void refreshStatistics();
                    },
                    statisticsReconcileMilliseconds
                );
        }
    }

    function scheduleLiveRefresh(
        delayMilliseconds = null
    ) {
        clearLiveTimer();

        if (document.hidden) {
            return;
        }

        const delay =
            delayMilliseconds ??
            (
                realtimeConnected
                    ? connectedLiveReconcileMilliseconds
                    : disconnectedLiveReconcileMilliseconds
            );

        liveTimerId =
            window.setTimeout(
                () => {
                    void refreshLive();
                },
                delay
            );
    }

    function scheduleStatisticsRefresh(
        delayMilliseconds =
            statisticsDebounceMilliseconds
    ) {
        if (
            statisticsDebounceTimerId !== null
        ) {
            window.clearTimeout(
                statisticsDebounceTimerId
            );
        }

        statisticsDebounceTimerId =
            window.setTimeout(
                () => {
                    statisticsDebounceTimerId =
                        null;

                    void refreshStatistics();
                },
                delayMilliseconds
            );
    }

    async function requestJson(url) {
        const response = await fetch(
            url,
            {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                headers: {
                    Accept: "application/json"
                }
            }
        );

        if (response.status === 401) {
            window.location.reload();

            throw new Error(
                "Authentication is required."
            );
        }

        if (!response.ok) {
            throw new Error(
                `Request failed with ${response.status}.`
            );
        }

        const payload = await response.json();

        return payload.data;
    }

    function renderLive(data) {
        if (!data) {
            return;
        }

        const status =
            normalizeConnectionStatus(
                data.connectionStatus
            );

        setText(
            "[data-connection-status]",
            labels[status]
        );

        updateConnectionIndicator(status);

        const activeSession =
            root.querySelector(
                "[data-active-session]"
            );

        const noActiveSession =
            root.querySelector(
                "[data-no-active-session]"
            );

        activeSession?.classList.toggle(
            "hidden",
            !data.hasActiveSession
        );

        noActiveSession?.classList.toggle(
            "hidden",
            data.hasActiveSession
        );

        setText(
            "[data-current-scene]",
            data.currentScene ||
            labels.notAvailable
        );

        setText(
            "[data-current-phase]",
            data.currentPhase ||
            labels.notAvailable
        );

        setText(
            "[data-current-state]",
            data.currentGameState ||
            labels.notAvailable
        );

        setText(
            "[data-session-duration]",
            data.hasActiveSession
                ? formatLiveDuration(
                    data.sessionStartedAtUtc,
                    data.serverTimeUtc
                )
                : `0${labels.minutes}`
        );

        setText(
            "[data-last-heartbeat]",
            formatDateTime(
                data.lastHeartbeatUtc
            )
        );

        pulseHeartbeatIfChanged(
            data.lastHeartbeatUtc
        );
    }

    function renderStatistics(data) {
        if (!data) {
            return;
        }

        setText(
            "[data-total-play-time]",
            formatDuration(
                data.totalPlayTimeMinutes
            )
        );

        setText(
            "[data-total-sessions]",
            formatNumber(
                data.totalSessions
            )
        );

        setText(
            "[data-linked-nodes]",
            formatNumber(
                data.linkedNodeCount
            )
        );

        setText(
            "[data-longest-session]",
            formatDuration(
                data.longestSessionMinutes
            )
        );

        setText(
            "[data-favorite-build]",
            data.favoriteBuild ||
            labels.notAvailable
        );

        setText(
            "[data-last-activity]",
            formatDateTime(
                data.lastActivityUtc
            )
        );

        renderActivityTrend(
            data.activityLastSevenDays
        );

        renderEventLog(
            data.recentEvents
        );
    }

    function renderActivityTrend(points) {
        const normalizedPoints =
            Array.isArray(points)
                ? points.slice(-7)
                : [];

        const line =
            root.querySelector(
                "[data-activity-line]"
            );

        const area =
            root.querySelector(
                "[data-activity-area]"
            );

        const labelsContainer =
            root.querySelector(
                "[data-activity-labels]"
            );

        if (
            !line ||
            !area ||
            !labelsContainer
        ) {
            return;
        }

        const width = 280;
        const height = 68;
        const topPadding = 6;

        const values = normalizedPoints.map(
            point =>
                Math.max(
                    0,
                    Number(point.minutes ?? 0)
                )
        );

        while (values.length < 7) {
            values.unshift(0);
        }

        const maximum = Math.max(
            1,
            ...values
        );

        const coordinates = values.map(
            (value, index) => {
                const x =
                    (width / 6) * index;

                const y =
                    height -
                    (
                        value / maximum
                    ) *
                    (
                        height -
                        topPadding
                    );

                return `${x.toFixed(2)},${y.toFixed(2)}`;
            }
        );

        line.setAttribute(
            "points",
            coordinates.join(" ")
        );

        area.setAttribute(
            "points",
            [
                "0,68",
                ...coordinates,
                "280,68"
            ].join(" ")
        );

        const dateFormatter =
            new Intl.DateTimeFormat(
                document.documentElement.lang ||
                "en",
                {
                    weekday: "narrow"
                }
            );

        const sourcePoints =
            normalizedPoints.length === 7
                ? normalizedPoints
                : Array.from(
                    { length: 7 },
                    (_, index) => ({
                        date: new Date(
                            Date.now() -
                            (6 - index) *
                            86400000
                        ).toISOString()
                    })
                );

        labelsContainer.innerHTML =
            sourcePoints
                .map(point => {
                    const date =
                        new Date(point.date);

                    const label =
                        Number.isNaN(
                            date.getTime()
                        )
                            ? "-"
                            : dateFormatter
                                .format(date);

                    return `<span>${escapeHtml(label)}</span>`;
                })
                .join("");

        setText(
            "[data-activity-total]",
            formatDuration(
                values.reduce(
                    (total, value) =>
                        total + value,
                    0
                )
            )
        );
    }

    function renderEventLog(events) {
        const incomingEvents =
            normalizeRecentEvents(events);

        const mergedEvents = [
            ...recentEvents,
            ...incomingEvents
        ];

        const uniqueEvents =
            new Map();

        for (const event of mergedEvents) {
            uniqueEvents.set(
                String(event.id),
                event
            );
        }

        recentEvents = Array
            .from(uniqueEvents.values())
            .sort(
                (left, right) =>
                    new Date(
                        right.occurredAtUtc
                    ).getTime() -
                    new Date(
                        left.occurredAtUtc
                    ).getTime()
            )
            .slice(
                0,
                maximumEventLogEntries
            );

        drawEventLog();
    }

    function prependEvent(event) {
        const normalized =
            normalizeEvent(event);

        if (!normalized) {
            return;
        }

        const eventKey =
            String(normalized.id);

        recentEvents = [
            normalized,
            ...recentEvents.filter(
                item =>
                    String(item.id) !==
                    eventKey
            )
        ].slice(
            0,
            maximumEventLogEntries
        );

        drawEventLog();
    }

    function drawEventLog() {
        const container =
            root.querySelector(
                "[data-event-log]"
            );

        if (!container) {
            return;
        }

        if (recentEvents.length === 0) {
            container.innerHTML = `
                <li class="border-l border-green-500/20 pl-4 text-slate-600">
                    ${escapeHtml(labels.eventLogEmpty)}
                </li>
            `;

            return;
        }

        container.innerHTML =
            recentEvents
                .map(event => {
                    const time =
                        formatTime(
                            new Date(
                                event.occurredAtUtc
                            )
                        );

                    const eventLabel =
                        formatEventLabel(
                            event.eventType
                        );

                    const scene =
                        event.scene &&
                        event.scene !==
                            labels.notAvailable
                            ? ` // ${event.scene}`
                            : "";

                    return `
                        <li class="border-l border-green-500/20 pl-4">
                            <span class="text-green-500">${escapeHtml(time)}</span>
                            <span class="text-slate-600"> - </span>
                            <span>${escapeHtml(eventLabel)}</span>
                            <span class="text-slate-600">${escapeHtml(scene)}</span>
                        </li>
                    `;
                })
                .join("");
    }

    function normalizeRecentEvents(events) {
        if (!Array.isArray(events)) {
            return [];
        }

        return events
            .map(normalizeEvent)
            .filter(Boolean)
            .sort(
                (left, right) =>
                    new Date(
                        right.occurredAtUtc
                    ).getTime() -
                    new Date(
                        left.occurredAtUtc
                    ).getTime()
            )
            .slice(
                0,
                maximumEventLogEntries
            );
    }

    function normalizeEvent(event) {
        if (!event) {
            return null;
        }

        const occurredAtUtc =
            event.occurredAtUtc ??
            new Date().toISOString();

        return {
            id:
                event.id ??
                `${event.eventType ?? "event"}:${occurredAtUtc}`,
            eventType:
                event.eventType ??
                "TelemetryEvent",
            scene:
                event.scene ??
                labels.notAvailable,
            occurredAtUtc
        };
    }

    function formatEventLabel(value) {
        const normalized =
            String(value ?? "TelemetryEvent")
                .replace(
                    /([a-z0-9])([A-Z])/g,
                    "$1 $2"
                )
                .replace(
                    /[_-]+/g,
                    " "
                )
                .trim();

        if (!normalized) {
            return "Telemetry Event";
        }

        return normalized
            .split(/\s+/)
            .map(
                (part, index) =>
                    index === 0
                        ? part.charAt(0).toUpperCase() +
                          part.slice(1)
                        : part.toLowerCase()
            )
            .join(" ");
    }

    function pulseHeartbeatIfChanged(value) {
        if (!value) {
            return;
        }

        const heartbeatDate =
            new Date(value);

        if (
            Number.isNaN(
                heartbeatDate.getTime()
            )
        ) {
            return;
        }

        const normalized =
            heartbeatDate.toISOString();

        if (
            lastHeartbeatValue === null
        ) {
            lastHeartbeatValue = normalized;
            return;
        }

        if (
            normalized === lastHeartbeatValue
        ) {
            return;
        }

        lastHeartbeatValue = normalized;

        const wave =
            root.querySelector(
                "[data-heartbeat-wave]"
            );

        if (!wave) {
            return;
        }

        wave.classList.remove(
            "profile-heartbeat-flash"
        );

        void wave.offsetWidth;

        wave.classList.add(
            "profile-heartbeat-flash"
        );
    }

    function updateConnectionIndicator(status) {
        const core =
            root.querySelector(
                "[data-heartbeat-core]"
            );

        if (!core) {
            return;
        }

        core.className =
            "relative h-3 w-3 rounded-full";

        if (status === "online") {
            core.classList.add(
                "bg-green-400",
                "shadow-[0_0_14px_rgba(74,222,128,0.85)]"
            );
            return;
        }

        if (status === "degraded") {
            core.classList.add(
                "bg-amber-400",
                "shadow-[0_0_14px_rgba(251,191,36,0.8)]"
            );
            return;
        }

        core.classList.add(
            "bg-slate-600",
            "shadow-[0_0_14px_rgba(100,116,139,0.7)]"
        );
    }

    function setSyncState(success) {
        const element =
            root.querySelector(
                "[data-dashboard-sync]"
            );

        if (!element) {
            return;
        }

        element.textContent = success
            ? formatTime(new Date())
            : labels.retrying;

        element.classList.toggle(
            "text-green-400",
            success
        );

        element.classList.toggle(
            "text-amber-400",
            !success
        );
    }

    function clearAllTimers() {
        clearPollTimers();
        clearReconnectTimer();

        if (
            statisticsDebounceTimerId !== null
        ) {
            window.clearTimeout(
                statisticsDebounceTimerId
            );

            statisticsDebounceTimerId =
                null;
        }
    }

    function clearPollTimers() {
        clearLiveTimer();
        clearStatisticsTimer();
    }

    function clearLiveTimer() {
        if (liveTimerId !== null) {
            window.clearTimeout(
                liveTimerId
            );

            liveTimerId = null;
        }
    }

    function clearStatisticsTimer() {
        if (statisticsTimerId !== null) {
            window.clearTimeout(
                statisticsTimerId
            );

            statisticsTimerId = null;
        }
    }

    function clearReconnectTimer() {
        if (reconnectTimerId !== null) {
            window.clearTimeout(
                reconnectTimerId
            );

            reconnectTimerId = null;
        }
    }

    function normalizeConnectionStatus(value) {
        const normalized =
            String(value ?? "")
                .trim()
                .toLowerCase();

        return [
            "online",
            "degraded",
            "offline"
        ].includes(normalized)
            ? normalized
            : "offline";
    }

    function formatDuration(totalMinutes) {
        const minutes = Math.max(
            0,
            Math.floor(
                Number(totalMinutes ?? 0)
            )
        );

        const hours = Math.floor(
            minutes / 60
        );

        const remainingMinutes =
            minutes % 60;

        if (hours <= 0) {
            return `${remainingMinutes}${labels.minutes}`;
        }

        return `${hours}${labels.hours} ${remainingMinutes}${labels.minutes}`;
    }

    function formatLiveDuration(
        startedAtUtc,
        serverTimeUtc
    ) {
        if (!startedAtUtc) {
            return `0${labels.minutes}`;
        }

        const startedAt =
            new Date(startedAtUtc);

        const serverTime =
            new Date(
                serverTimeUtc ??
                Date.now()
            );

        if (
            Number.isNaN(
                startedAt.getTime()
            ) ||
            Number.isNaN(
                serverTime.getTime()
            )
        ) {
            return `0${labels.minutes}`;
        }

        const minutes = Math.max(
            0,
            Math.floor(
                (
                    serverTime.getTime() -
                    startedAt.getTime()
                ) /
                60000
            )
        );

        return formatDuration(minutes);
    }

    function formatDateTime(value) {
        if (!value) {
            return labels.notAvailable;
        }

        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return labels.notAvailable;
        }

        return new Intl.DateTimeFormat(
            document.documentElement.lang ||
            "en",
            {
                dateStyle: "medium",
                timeStyle: "short"
            }
        ).format(date);
    }

    function formatTime(date) {
        if (
            !(date instanceof Date) ||
            Number.isNaN(date.getTime())
        ) {
            return "--:--";
        }

        return new Intl.DateTimeFormat(
            document.documentElement.lang ||
            "en",
            {
                hour: "2-digit",
                minute: "2-digit"
            }
        ).format(date);
    }

    function formatNumber(value) {
        return new Intl.NumberFormat(
            document.documentElement.lang ||
            "en"
        ).format(Number(value ?? 0));
    }

    function setText(selector, value) {
        const element =
            root.querySelector(selector);

        if (element) {
            element.textContent = value;
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#039;");
    }
})();
