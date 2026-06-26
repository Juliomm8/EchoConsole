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
        statistics: root.dataset.statisticsUrl
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
            root.dataset.labelRetrying ?? "RETRYING"
    };

    const liveIntervalMilliseconds = 5000;
    const statisticsIntervalMilliseconds = 30000;
    const maximumBackoffMilliseconds = 30000;

    let liveTimerId = null;
    let statisticsTimerId = null;
    let liveBackoffMilliseconds =
        liveIntervalMilliseconds;

    initialize();

    function initialize() {
        void refreshLive();
        void refreshStatistics();

        document.addEventListener(
            "visibilitychange",
            () => {
                if (document.hidden) {
                    clearTimers();
                    return;
                }

                liveBackoffMilliseconds =
                    liveIntervalMilliseconds;

                void refreshLive();
                void refreshStatistics();
            }
        );

        window.addEventListener(
            "beforeunload",
            clearTimers
        );
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

            renderLive(data);

            liveBackoffMilliseconds =
                liveIntervalMilliseconds;

            setSyncState(true);
        } catch {
            setSyncState(false);

            liveBackoffMilliseconds = Math.min(
                liveBackoffMilliseconds * 2,
                maximumBackoffMilliseconds
            );
        } finally {
            liveTimerId = window.setTimeout(
                () => {
                    void refreshLive();
                },
                liveBackoffMilliseconds
            );
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
            statisticsTimerId = window.setTimeout(
                () => {
                    void refreshStatistics();
                },
                statisticsIntervalMilliseconds
            );
        }
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
    }

    function updateConnectionIndicator(status) {
        const indicator =
            root.querySelector(
                "[data-connection-indicator]"
            );

        if (!indicator) {
            return;
        }

        indicator.className =
            "h-3 w-3 rounded-full";

        if (status === "online") {
            indicator.classList.add(
                "bg-green-400",
                "shadow-[0_0_14px_rgba(74,222,128,0.85)]"
            );
            return;
        }

        if (status === "degraded") {
            indicator.classList.add(
                "bg-amber-400",
                "shadow-[0_0_14px_rgba(251,191,36,0.8)]"
            );
            return;
        }

        indicator.classList.add(
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

    function clearTimers() {
        clearLiveTimer();
        clearStatisticsTimer();
    }

    function clearLiveTimer() {
        if (liveTimerId !== null) {
            window.clearTimeout(liveTimerId);
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

    function normalizeConnectionStatus(value) {
        const normalized =
            String(value ?? "")
                .trim()
                .toLowerCase();

        return ["online", "degraded", "offline"]
            .includes(normalized)
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
            Number.isNaN(startedAt.getTime()) ||
            Number.isNaN(serverTime.getTime())
        ) {
            return `0${labels.minutes}`;
        }

        const minutes = Math.max(
            0,
            Math.floor(
                (serverTime.getTime() -
                 startedAt.getTime()) /
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
        return new Intl.DateTimeFormat(
            document.documentElement.lang ||
            "en",
            {
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit"
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
})();
