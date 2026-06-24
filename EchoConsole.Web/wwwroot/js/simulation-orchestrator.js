(() => {
    "use strict";

    const root = document.getElementById("simulation-orchestrator-root");

    if (!root) {
        return;
    }

    const selectors = {
        overlay: "[data-sim-overlay]",
        panel: "[data-sim-panel]",
        open: "[data-sim-open]",
        close: "[data-sim-close]",
        target: "[data-sim-target]",
        applyTarget: "[data-sim-apply-target]",
        organic: "[data-sim-organic]",
        critical: "[data-sim-critical]",
        massDrop: "[data-sim-mass-drop]",
        purge: "[data-sim-purge]",
        wipe: "[data-sim-wipe]",
        clearLog: "[data-sim-clear-log]",
        log: "[data-sim-log]",
        channelState: "[data-sim-channel-state]",
        realCount: "[data-sim-real-count]",
        simulatedCount: "[data-sim-simulated-count]",
        lastSync: "[data-sim-last-sync]"
    };

    const elements = Object.fromEntries(
        Object.entries(selectors).map(([key, selector]) => [
            key,
            root.querySelector(selector)
        ]));

    const moduleInputs = Array.from(
        root.querySelectorAll("[data-sim-module]"));

    const antiForgeryToken = root
        .querySelector('input[name="__RequestVerificationToken"]')
        ?.value;

    const endpoints = {
        status: root.dataset.statusUrl,
        reconcile: root.dataset.reconcileUrl,
        pulse: root.dataset.pulseUrl,
        critical: root.dataset.criticalAlertUrl,
        massDrop: root.dataset.massDropUrl,
        purge: root.dataset.purgeUrl,
        wipe: root.dataset.wipeUrl
    };

    const messages = {
        stateActive: root.dataset.i18nStateActive ?? "ACTIVE",
        stateIdle: root.dataset.i18nStateIdle ?? "IDLE",
        stateOffline: root.dataset.i18nStateOffline ?? "OFFLINE",
        stateTransmitting: root.dataset.i18nStateTransmitting ?? "TRANSMITTING",
        stateError: root.dataset.i18nStateError ?? "ERROR",
        endpointNotConfigured:
            root.dataset.i18nEndpointNotConfigured
            ?? "Simulation endpoint is not configured.",
        requestFailedStatus:
            root.dataset.i18nRequestFailedStatus
            ?? "Request failed with status {0}.",
        lastSync:
            root.dataset.i18nLastSync
            ?? "LAST_SYNC: {0}",
        statusError:
            root.dataset.i18nStatusError
            ?? "STATUS_ERROR",
        channelBusy:
            root.dataset.i18nChannelBusy
            ?? "CHANNEL_BUSY: previous command still running.",
        requestSent:
            root.dataset.i18nRequestSent
            ?? "REQUEST_SENT",
        complete:
            root.dataset.i18nComplete
            ?? "COMPLETE",
        accessDenied:
            root.dataset.i18nAccessDenied
            ?? "ACCESS_DENIED: admin session required.",
        organicEnabled:
            root.dataset.i18nOrganicEnabled
            ?? "ORGANIC_MODE: ENABLED",
        organicDisabled:
            root.dataset.i18nOrganicDisabled
            ?? "ORGANIC_MODE: DISABLED",
        confirmMassDrop:
            root.dataset.i18nConfirmMassDrop
            ?? messages.confirmMassDrop,
        confirmPurge:
            root.dataset.i18nConfirmPurge
            ?? messages.confirmPurge,
        confirmWipeFirst:
            root.dataset.i18nConfirmWipeFirst
            ?? messages.confirmWipeFirst,
        confirmWipeFinal:
            root.dataset.i18nConfirmWipeFinal
            ?? messages.confirmWipeFinal,
        consoleCleared:
            root.dataset.i18nConsoleCleared
            ?? "DEBUG_CONSOLE_CLEARED"
    };

    function formatMessage(template, ...values) {
        return values.reduce(
            (result, value, index) =>
                result.replaceAll(`{${index}}`, String(value)),
            template);
    }

    const state = {
        isOpen: false,
        requestInFlight: false,
        organicTimerId: null,
        statusTimerId: null,
        lastFocusedElement: null,
        previousBodyOverflow: ""
    };

    function getModules() {
        return moduleInputs.reduce(
            (modules, input) => {
                modules[input.dataset.simModule] = input.checked;
                return modules;
            },
            {
                sessions: false,
                installations: false,
                alerts: false
            });
    }

    function getTarget() {
        const rawValue = Number.parseInt(elements.target.value, 10);
        const normalizedValue = Number.isFinite(rawValue)
            ? rawValue
            : Number.parseInt(root.dataset.defaultTarget ?? "40", 10);

        return Math.min(250, Math.max(0, normalizedValue));
    }

    function appendLog(message, tone = "default") {
        const line = document.createElement("div");
        const timestamp = new Date().toLocaleTimeString([], {
            hour12: false
        });

        line.textContent = `[${timestamp}] ${message}`;

        if (tone === "error") {
            line.className = "text-rose-400";
        } else if (tone === "warning") {
            line.className = "text-amber-400";
        } else if (tone === "success") {
            line.className = "text-green-400";
        } else if (tone === "info") {
            line.className = "text-cyan-400";
        }

        elements.log.appendChild(line);
        elements.log.scrollTop = elements.log.scrollHeight;

        while (elements.log.childElementCount > 80) {
            elements.log.firstElementChild?.remove();
        }
    }

    function setChannelState(label, tone = "idle") {
        elements.channelState.textContent = `[ ${label} ]`;
        elements.channelState.className =
            "rounded border bg-black px-2 py-1 text-[9px] uppercase tracking-[0.16em]";

        if (tone === "active") {
            elements.channelState.classList.add(
                "border-green-500/30",
                "text-green-400");
        } else if (tone === "warning") {
            elements.channelState.classList.add(
                "border-amber-500/30",
                "text-amber-400");
        } else if (tone === "error") {
            elements.channelState.classList.add(
                "border-rose-500/30",
                "text-rose-400");
        } else {
            elements.channelState.classList.add(
                "border-slate-700",
                "text-slate-500");
        }
    }

    function setOpen(isOpen) {
        state.isOpen = isOpen;
        elements.panel.setAttribute("aria-hidden", isOpen ? "false" : "true");
        elements.open.setAttribute("aria-expanded", isOpen ? "true" : "false");

        if (isOpen) {
            state.lastFocusedElement = document.activeElement;
            state.previousBodyOverflow = document.body.style.overflow;
            document.body.style.overflow = "hidden";

            root.classList.remove("pointer-events-none");
            elements.panel.classList.remove("translate-x-full");
            elements.overlay.classList.add("opacity-100");
            elements.close.focus();
            void refreshStatus();
        } else {
            root.classList.add("pointer-events-none");
            elements.panel.classList.add("translate-x-full");
            elements.overlay.classList.remove("opacity-100");
            document.body.style.overflow = state.previousBodyOverflow;

            if (state.lastFocusedElement instanceof HTMLElement) {
                state.lastFocusedElement.focus();
            }
        }
    }

    async function parseResponse(response) {
        if (response.status === 204) {
            return {};
        }

        const contentType = response.headers.get("content-type") ?? "";

        if (contentType.includes("application/json")) {
            return await response.json();
        }

        const text = await response.text();
        return text ? { message: text } : {};
    }

    async function request(url, options = {}) {
        if (!url) {
            throw new Error(messages.endpointNotConfigured);
        }

        const method = options.method ?? "GET";
        const headers = {
            Accept: "application/json"
        };

        if (method !== "GET") {
            headers["Content-Type"] = "application/json";

            if (antiForgeryToken) {
                headers.RequestVerificationToken = antiForgeryToken;
            }
        }

        const response = await fetch(url, {
            method,
            credentials: "same-origin",
            headers,
            body: options.body === undefined
                ? undefined
                : JSON.stringify(options.body)
        });

        const payload = await parseResponse(response);

        if (!response.ok) {
            const message = payload.message
                ?? payload.title
                ?? formatMessage(messages.requestFailedStatus, response.status);

            const error = new Error(message);
            error.status = response.status;
            throw error;
        }

        return payload;
    }

    function applyStatus(status) {
        elements.realCount.textContent = String(
            status.activeRealSessions ?? status.realSessions ?? 0);

        elements.simulatedCount.textContent = String(
            status.activeSimulatedSessions ?? status.simulatedSessions ?? 0);

        elements.lastSync.textContent =
            formatMessage(
                messages.lastSync,
                new Date().toLocaleTimeString([], { hour12: false }));

        const activeSimulated = Number(
            status.activeSimulatedSessions ?? status.simulatedSessions ?? 0);

        setChannelState(
            activeSimulated > 0
                ? messages.stateActive
                : messages.stateIdle,
            activeSimulated > 0 ? "active" : "idle");
    }

    async function refreshStatus() {
        try {
            const status = await request(endpoints.status);
            applyStatus(status);
        } catch (error) {
            setChannelState(messages.stateOffline, "error");

            if (state.isOpen) {
                appendLog(`${messages.statusError}: ${error.message}`, "error");
            }
        }
    }

    async function runCommand(label, endpoint, body, options = {}) {
        if (state.requestInFlight) {
            appendLog(messages.channelBusy, "warning");
            return null;
        }

        state.requestInFlight = true;
        setChannelState(messages.stateTransmitting, "warning");
        appendLog(`${label}: ${messages.requestSent}`, "info");

        try {
            const result = await request(endpoint, {
                method: options.method ?? "POST",
                body
            });

            appendLog(
                result.message ?? `${label}: ${messages.complete}`,
                "success");

            if (result.status) {
                applyStatus(result.status);
            } else {
                await refreshStatus();
            }

            return result;
        } catch (error) {
            appendLog(`${label}_ERROR: ${error.message}`, "error");
            setChannelState(messages.stateError, "error");

            if (error.status === 401 || error.status === 403) {
                stopOrganicMode();
                appendLog(messages.accessDenied, "error");
            }

            return null;
        } finally {
            state.requestInFlight = false;
        }
    }

    async function reconcileTarget() {
        const targetActiveSessions = getTarget();
        elements.target.value = String(targetActiveSessions);

        await runCommand(
            "RECONCILE_TARGET",
            endpoints.reconcile,
            {
                targetActiveSessions,
                modules: getModules()
            });
    }

    function getOrganicDelay() {
        return 4000 + Math.floor(Math.random() * 4001);
    }

    function scheduleOrganicPulse() {
        window.clearTimeout(state.organicTimerId);

        if (!elements.organic.checked) {
            state.organicTimerId = null;
            return;
        }

        state.organicTimerId = window.setTimeout(
            async () => {
                if (!state.requestInFlight) {
                    await runCommand(
                        "ORGANIC_PULSE",
                        endpoints.pulse,
                        {
                            targetActiveSessions: getTarget(),
                            modules: getModules()
                        });
                }

                scheduleOrganicPulse();
            },
            getOrganicDelay());
    }

    function startOrganicMode() {
        appendLog(messages.organicEnabled, "warning");
        scheduleOrganicPulse();
    }

    function stopOrganicMode() {
        window.clearTimeout(state.organicTimerId);
        state.organicTimerId = null;
        elements.organic.checked = false;
        appendLog(messages.organicDisabled, "info");
    }

    elements.open.addEventListener("click", () => setOpen(true));
    elements.close.addEventListener("click", () => setOpen(false));
    elements.overlay.addEventListener("click", () => setOpen(false));

    elements.applyTarget.addEventListener("click", reconcileTarget);

    elements.target.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            void reconcileTarget();
        }
    });

    elements.organic.addEventListener("change", () => {
        if (elements.organic.checked) {
            startOrganicMode();
        } else {
            stopOrganicMode();
        }
    });

    elements.critical.addEventListener("click", async () => {
        await runCommand(
            "CRITICAL_ALARM",
            endpoints.critical,
            {
                modules: getModules()
            });
    });

    elements.massDrop.addEventListener("click", async () => {
        const confirmed = window.confirm(
            messages.confirmMassDrop);

        if (!confirmed) {
            return;
        }

        await runCommand(
            "MASS_DROP",
            endpoints.massDrop,
            {
                modules: getModules()
            });
    });

    elements.purge.addEventListener("click", async () => {
        const confirmed = window.confirm(
            messages.confirmPurge);

        if (!confirmed) {
            return;
        }

        stopOrganicMode();

        await runCommand(
            "PURGE_SIMULATED_DATA",
            endpoints.purge,
            {
                confirmation: "PURGE_PC_PLAYER_DATA"
            });
    });

    elements.wipe.addEventListener("click", async () => {
        const firstConfirmation = window.confirm(
            messages.confirmWipeFirst);

        if (!firstConfirmation) {
            return;
        }

        const finalConfirmation = window.confirm(
            messages.confirmWipeFinal);

        if (!finalConfirmation) {
            return;
        }

        stopOrganicMode();

        await runCommand(
            "WIPE_ALL_TELEMETRY",
            endpoints.wipe,
            {
                confirmation: "WIPE_ALL_TELEMETRY"
            });
    });

    elements.clearLog.addEventListener("click", () => {
        elements.log.replaceChildren();
        appendLog(messages.consoleCleared, "info");
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && state.isOpen) {
            setOpen(false);
        }
    });

    window.addEventListener("pagehide", () => {
        window.clearTimeout(state.organicTimerId);
        window.clearInterval(state.statusTimerId);
        document.body.style.overflow = state.previousBodyOverflow;
    });

    state.statusTimerId = window.setInterval(
        refreshStatus,
        5000);

    void refreshStatus();
})();
