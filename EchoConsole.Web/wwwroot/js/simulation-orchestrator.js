(() => {
    "use strict";

    const rootId = "simulation-orchestrator-root";
    const openSelector = "[data-sim-open]";
    const managerKey = "__echoConsoleSimulationManager";
    const storageKey = "echo-console:simulation-orchestrator:v4";

    const statusPollIntervalMs = 10000;
    const statusUiThrottleMs = 1800;
    const logFlushIntervalMs = 300;
    const organicMinimumDelayMs = 4000;
    const organicMaximumDelayMs = 8000;

    function unlockDocumentScroll() {
        const body = document.body;

        if (!body) {
            return;
        }

        if (
            body.dataset.simulationScrollLocked
            === "true"
        ) {
            body.style.overflow =
                body.dataset.simulationPreviousOverflow
                ?? "";
        }

        delete body.dataset.simulationScrollLocked;
        delete body.dataset.simulationPreviousOverflow;
    }

    function resetVisualState() {
        unlockDocumentScroll();

        document
            .querySelectorAll(`#${rootId}`)
            .forEach(root => {
                root.classList.add(
                    "pointer-events-none");

                root.setAttribute(
                    "aria-hidden",
                    "true");

                const overlay =
                    root.querySelector(
                        "[data-sim-overlay]");

                const panel =
                    root.querySelector(
                        "[data-sim-panel]");

                if (overlay) {
                    overlay.classList.remove(
                        "opacity-100");

                    overlay.classList.add(
                        "opacity-0");
                }

                if (panel) {
                    panel.classList.add(
                        "translate-x-full");

                    panel.setAttribute(
                        "aria-hidden",
                        "true");
                }
            });

        document
            .querySelectorAll(openSelector)
            .forEach(button => {
                button.classList.remove(
                    "pointer-events-none",
                    "invisible",
                    "opacity-0");

                button.classList.add(
                    "pointer-events-auto",
                    "opacity-100");

                button.setAttribute(
                    "aria-expanded",
                    "false");
            });
    }

    function getRequiredElements(root, openButton) {
        const selectors = {
            overlay: "[data-sim-overlay]",
            panel: "[data-sim-panel]",
            close: "[data-sim-close]",
            target: "[data-sim-target]",
            applyTarget: "[data-sim-apply-target]",
            organic: "[data-sim-organic]",
            terminate: "[data-sim-terminate]",
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

        const elements = {
            open: openButton
        };

        for (const [key, selector] of Object.entries(selectors)) {
            elements[key] = root.querySelector(selector);
        }

        const missing = Object
            .entries(elements)
            .filter(([, element]) => !element)
            .map(([key]) => key);

        if (missing.length > 0) {
            throw new Error(
                `Simulation Orchestrator is missing required elements: ${missing.join(", ")}`);
        }

        return elements;
    }

    function readStoredState() {
        try {
            const serialized =
                window.localStorage.getItem(storageKey);

            if (!serialized) {
                return null;
            }

            const value = JSON.parse(serialized);

            return value
                && typeof value === "object"
                    ? value
                    : null;
        } catch {
            return null;
        }
    }

    function writeStoredState(value) {
        try {
            window.localStorage.setItem(
                storageKey,
                JSON.stringify(value));
        } catch {
        }
    }

    class SimulationOrchestrator {
        constructor(root, openButton) {
            this.root = root;
            this.openButton = openButton;
            this.elements = getRequiredElements(
                root,
                openButton);

            this.moduleInputs = Array.from(
                root.querySelectorAll(
                    "[data-sim-module]"));

            this.antiForgeryToken = root
                .querySelector(
                    'input[name="__RequestVerificationToken"]')
                ?.value;

            this.endpoints = {
                status: root.dataset.statusUrl,
                reconcile: root.dataset.reconcileUrl,
                pulse: root.dataset.pulseUrl,
                critical: root.dataset.criticalAlertUrl,
                massDrop: root.dataset.massDropUrl,
                purge: root.dataset.purgeUrl,
                wipe: root.dataset.wipeUrl
            };

            this.messages = {
                stateActive:
                    root.dataset.i18nStateActive
                    ?? "ACTIVE",
                stateIdle:
                    root.dataset.i18nStateIdle
                    ?? "IDLE",
                stateOffline:
                    root.dataset.i18nStateOffline
                    ?? "OFFLINE",
                stateTransmitting:
                    root.dataset.i18nStateTransmitting
                    ?? "TRANSMITTING",
                stateError:
                    root.dataset.i18nStateError
                    ?? "ERROR",
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
                confirmTerminate:
                    root.dataset.i18nConfirmTerminate
                    ?? "Stop the persistent simulation and disconnect all simulated sessions?",
                confirmMassDrop:
                    root.dataset.i18nConfirmMassDrop
                    ?? "Disconnect all simulated sessions?",
                confirmPurge:
                    root.dataset.i18nConfirmPurge
                    ?? "Delete all simulated telemetry data?",
                confirmWipeFirst:
                    root.dataset.i18nConfirmWipeFirst
                    ?? "Delete all telemetry data?",
                confirmWipeFinal:
                    root.dataset.i18nConfirmWipeFinal
                    ?? "This operation cannot be undone. Continue?",
                consoleCleared:
                    root.dataset.i18nConsoleCleared
                    ?? "DEBUG_CONSOLE_CLEARED"
            };

            this.state = {
                isOpen: false,
                requestInFlight: false,
                statusRequestInFlight: false,
                organicTimerId: null,
                statusTimerId: null,
                uiFlushTimerId: null,
                logFlushTimerId: null,
                pendingStatus: null,
                pendingLogs: [],
                lastUiFlushAt: 0,
                lastFocusedElement: null,
                destroyed: false
            };

            this.lifecycleController =
                new AbortController();

            this.requestController =
                new AbortController();
        }

        initialize() {
            this.forceClosedState();
            this.restorePersistentState();
            this.bindEvents();

            this.state.statusTimerId =
                window.setInterval(
                    () => {
                        if (
                            !this.state.destroyed
                            && this.root.isConnected
                        ) {
                            void this.refreshStatus();
                        }
                    },
                    statusPollIntervalMs);

            void this.refreshStatus({
                immediate: true
            });

            if (this.elements.organic.checked) {
                this.startOrganicMode({
                    writeLog: false,
                    immediate: true
                });
            }
        }

        isCurrent(root, openButton) {
            return (
                !this.state.destroyed
                && this.root === root
                && this.openButton === openButton
                && this.root.isConnected
                && this.openButton.isConnected
            );
        }

        formatMessage(template, ...values) {
            return values.reduce(
                (result, value, index) =>
                    result.replaceAll(
                        `{${index}}`,
                        String(value)),
                template);
        }

        getModules() {
            return this.moduleInputs.reduce(
                (modules, input) => {
                    modules[input.dataset.simModule] =
                        input.checked;

                    return modules;
                },
                {
                    sessions: false,
                    installations: false,
                    alerts: false,
                    events: false
                });
        }

        getTarget() {
            const rawValue = Number.parseInt(
                this.elements.target.value,
                10);

            const fallbackValue = Number.parseInt(
                this.root.dataset.defaultTarget
                    ?? "40",
                10);

            const normalizedValue =
                Number.isFinite(rawValue)
                    ? rawValue
                    : fallbackValue;

            return Math.min(
                250,
                Math.max(0, normalizedValue));
        }

        readPersistentState() {
            const stored = readStoredState();

            return {
                target:
                    Number.isFinite(
                        Number(stored?.target))
                        ? Math.min(
                            250,
                            Math.max(
                                0,
                                Number(stored.target)))
                        : this.getTarget(),
                organicEnabled:
                    stored?.organicEnabled === true,
                modules: {
                    sessions:
                        stored?.modules?.sessions
                        !== false,
                    installations:
                        stored?.modules?.installations
                        !== false,
                    alerts:
                        stored?.modules?.alerts
                        !== false,
                    events:
                        stored?.modules?.events
                        !== false
                }
            };
        }

        restorePersistentState() {
            const persisted =
                this.readPersistentState();

            this.elements.target.value =
                String(persisted.target);

            this.elements.organic.checked =
                persisted.organicEnabled;

            this.moduleInputs.forEach(input => {
                const key =
                    input.dataset.simModule;

                input.checked =
                    persisted.modules[key]
                    !== false;
            });
        }

        persistState(overrides = {}) {
            const current = {
                target: this.getTarget(),
                organicEnabled:
                    this.elements.organic.checked,
                modules: this.getModules()
            };

            writeStoredState({
                ...current,
                ...overrides,
                modules: {
                    ...current.modules,
                    ...(overrides.modules ?? {})
                }
            });
        }

        lockDocumentScroll() {
            const body = document.body;

            if (!body) {
                return;
            }

            if (
                body.dataset.simulationScrollLocked
                !== "true"
            ) {
                body.dataset.simulationPreviousOverflow =
                    body.style.overflow;

                body.dataset.simulationScrollLocked =
                    "true";
            }

            body.style.overflow = "hidden";
        }

        forceClosedState() {
            this.state.isOpen = false;

            this.root.classList.add(
                "pointer-events-none");

            this.root.setAttribute(
                "aria-hidden",
                "true");

            this.elements.panel.classList.add(
                "translate-x-full");

            this.elements.panel.setAttribute(
                "aria-hidden",
                "true");

            this.elements.overlay.classList.remove(
                "opacity-100");

            this.elements.overlay.classList.add(
                "opacity-0");

            this.openButton.classList.remove(
                "pointer-events-none",
                "invisible",
                "opacity-0");

            this.openButton.classList.add(
                "pointer-events-auto",
                "opacity-100");

            this.openButton.setAttribute(
                "aria-expanded",
                "false");

            unlockDocumentScroll();
        }

        setOpen(isOpen, options = {}) {
            if (
                this.state.destroyed
                || !this.root.isConnected
                || !this.openButton.isConnected
            ) {
                this.destroy();
                resetVisualState();
                return;
            }

            this.state.isOpen = isOpen;

            this.root.setAttribute(
                "aria-hidden",
                isOpen ? "false" : "true");

            this.elements.panel.setAttribute(
                "aria-hidden",
                isOpen ? "false" : "true");

            this.openButton.setAttribute(
                "aria-expanded",
                isOpen ? "true" : "false");

            if (isOpen) {
                this.state.lastFocusedElement =
                    document.activeElement;

                this.lockDocumentScroll();

                this.root.classList.remove(
                    "pointer-events-none");

                this.elements.panel.classList.remove(
                    "translate-x-full");

                this.elements.overlay.classList.remove(
                    "opacity-0");

                this.elements.overlay.classList.add(
                    "opacity-100");

                this.openButton.classList.remove(
                    "pointer-events-auto",
                    "opacity-100");

                this.openButton.classList.add(
                    "pointer-events-none",
                    "invisible",
                    "opacity-0");

                this.elements.close.focus();

                void this.refreshStatus({
                    immediate: true
                });

                return;
            }

            this.forceClosedState();

            if (
                options.restoreFocus !== false
                && this.state.lastFocusedElement
                    instanceof HTMLElement
                && this.state.lastFocusedElement.isConnected
            ) {
                this.state.lastFocusedElement.focus();
            }
        }

        appendLog(message, tone = "default") {
            if (
                this.state.destroyed
                || !this.elements.log.isConnected
            ) {
                return;
            }

            this.state.pendingLogs.push({
                message,
                tone,
                timestamp:
                    new Date().toLocaleTimeString(
                        [],
                        {
                            hour12: false
                        })
            });

            if (this.state.logFlushTimerId !== null) {
                return;
            }

            this.state.logFlushTimerId =
                window.setTimeout(
                    () => this.flushLogs(),
                    logFlushIntervalMs);
        }

        flushLogs() {
            window.clearTimeout(
                this.state.logFlushTimerId);

            this.state.logFlushTimerId = null;

            if (
                this.state.destroyed
                || !this.elements.log.isConnected
                || this.state.pendingLogs.length === 0
            ) {
                this.state.pendingLogs = [];
                return;
            }

            const fragment =
                document.createDocumentFragment();

            for (const entry of this.state.pendingLogs) {
                const line =
                    document.createElement("div");

                line.textContent =
                    `[${entry.timestamp}] ${entry.message}`;

                if (entry.tone === "error") {
                    line.className =
                        "text-rose-400";
                } else if (entry.tone === "warning") {
                    line.className =
                        "text-amber-400";
                } else if (entry.tone === "success") {
                    line.className =
                        "text-green-400";
                } else if (entry.tone === "info") {
                    line.className =
                        "text-cyan-400";
                }

                fragment.appendChild(line);
            }

            this.state.pendingLogs = [];
            this.elements.log.appendChild(fragment);

            while (
                this.elements.log.childElementCount > 80
            ) {
                this.elements.log
                    .firstElementChild
                    ?.remove();
            }

            this.elements.log.scrollTop =
                this.elements.log.scrollHeight;
        }

        setChannelState(label, tone = "idle") {
            if (
                this.state.destroyed
                || !this.elements.channelState.isConnected
            ) {
                return;
            }

            this.elements.channelState.textContent =
                `[ ${label} ]`;

            this.elements.channelState.className =
                "rounded border bg-black px-2 py-1 text-[9px] uppercase tracking-[0.16em]";

            if (tone === "active") {
                this.elements.channelState.classList.add(
                    "border-green-500/30",
                    "text-green-400");
            } else if (tone === "warning") {
                this.elements.channelState.classList.add(
                    "border-amber-500/30",
                    "text-amber-400");
            } else if (tone === "error") {
                this.elements.channelState.classList.add(
                    "border-rose-500/30",
                    "text-rose-400");
            } else {
                this.elements.channelState.classList.add(
                    "border-slate-700",
                    "text-slate-500");
            }
        }

        async parseResponse(response) {
            if (response.status === 204) {
                return {};
            }

            const contentType =
                response.headers.get(
                    "content-type")
                ?? "";

            if (
                contentType.includes(
                    "application/json")
            ) {
                return await response.json();
            }

            const text =
                await response.text();

            return text
                ? { message: text }
                : {};
        }

        async request(url, options = {}) {
            if (!url) {
                throw new Error(
                    this.messages.endpointNotConfigured);
            }

            const method =
                options.method ?? "GET";

            const headers = {
                Accept: "application/json"
            };

            if (method !== "GET") {
                headers["Content-Type"] =
                    "application/json";

                if (this.antiForgeryToken) {
                    headers.RequestVerificationToken =
                        this.antiForgeryToken;
                }
            }

            const response = await fetch(
                url,
                {
                    method,
                    credentials: "same-origin",
                    headers,
                    body:
                        options.body === undefined
                            ? undefined
                            : JSON.stringify(
                                options.body),
                    signal:
                        this.requestController.signal
                });

            const payload =
                await this.parseResponse(response);

            if (!response.ok) {
                const message =
                    payload.message
                    ?? payload.title
                    ?? this.formatMessage(
                        this.messages
                            .requestFailedStatus,
                        response.status);

                const error =
                    new Error(message);

                error.status =
                    response.status;

                throw error;
            }

            return payload;
        }

        queueStatus(status, options = {}) {
            if (
                this.state.destroyed
                || !status
            ) {
                return;
            }

            this.state.pendingStatus = status;

            if (options.immediate === true) {
                this.flushStatus();
                return;
            }

            if (this.state.uiFlushTimerId !== null) {
                return;
            }

            const elapsed =
                performance.now()
                - this.state.lastUiFlushAt;

            const delay =
                Math.max(
                    0,
                    statusUiThrottleMs - elapsed);

            this.state.uiFlushTimerId =
                window.setTimeout(
                    () => this.flushStatus(),
                    delay);
        }

        flushStatus() {
            window.clearTimeout(
                this.state.uiFlushTimerId);

            this.state.uiFlushTimerId = null;

            const status =
                this.state.pendingStatus;

            this.state.pendingStatus = null;

            if (
                this.state.destroyed
                || !status
                || !this.root.isConnected
            ) {
                return;
            }

            window.requestAnimationFrame(() => {
                if (
                    this.state.destroyed
                    || !this.root.isConnected
                ) {
                    return;
                }

                const activeReal =
                    Number(
                        status.activeRealSessions
                        ?? status.realSessions
                        ?? 0);

                const activeSimulated =
                    Number(
                        status.activeSimulatedSessions
                        ?? status.simulatedSessions
                        ?? 0);

                this.elements.realCount.textContent =
                    String(activeReal);

                this.elements.simulatedCount.textContent =
                    String(activeSimulated);

                this.elements.lastSync.textContent =
                    this.formatMessage(
                        this.messages.lastSync,
                        new Date().toLocaleTimeString(
                            [],
                            {
                                hour12: false
                            }));

                this.setChannelState(
                    activeSimulated > 0
                        ? this.messages.stateActive
                        : this.messages.stateIdle,
                    activeSimulated > 0
                        ? "active"
                        : "idle");

                this.state.lastUiFlushAt =
                    performance.now();
            });
        }

        async refreshStatus(options = {}) {
            if (
                this.state.destroyed
                || !this.root.isConnected
                || this.state.statusRequestInFlight
            ) {
                return;
            }

            this.state.statusRequestInFlight = true;

            try {
                const status =
                    await this.request(
                        this.endpoints.status);

                this.queueStatus(
                    status,
                    {
                        immediate:
                            options.immediate === true
                    });
            } catch (error) {
                if (
                    error.name === "AbortError"
                    || this.state.destroyed
                ) {
                    return;
                }

                this.setChannelState(
                    this.messages.stateOffline,
                    "error");

                if (this.state.isOpen) {
                    this.appendLog(
                        `${this.messages.statusError}: ${error.message}`,
                        "error");
                }
            } finally {
                this.state.statusRequestInFlight = false;
            }
        }

        async runCommand(
            label,
            endpoint,
            body,
            options = {}) {
            if (this.state.requestInFlight) {
                this.appendLog(
                    this.messages.channelBusy,
                    "warning");

                return null;
            }

            this.state.requestInFlight = true;

            this.setChannelState(
                this.messages.stateTransmitting,
                "warning");

            this.appendLog(
                `${label}: ${this.messages.requestSent}`,
                "info");

            try {
                const result =
                    await this.request(
                        endpoint,
                        {
                            method:
                                options.method
                                ?? "POST",
                            body
                        });

                this.appendLog(
                    result.message
                    ?? `${label}: ${this.messages.complete}`,
                    "success");

                if (result.status) {
                    this.queueStatus(result.status);
                } else {
                    void this.refreshStatus();
                }

                return result;
            } catch (error) {
                if (
                    error.name === "AbortError"
                    || this.state.destroyed
                ) {
                    return null;
                }

                this.appendLog(
                    `${label}_ERROR: ${error.message}`,
                    "error");

                this.setChannelState(
                    this.messages.stateError,
                    "error");

                if (
                    error.status === 401
                    || error.status === 403
                ) {
                    this.appendLog(
                        this.messages.accessDenied,
                        "error");
                }

                return null;
            } finally {
                this.state.requestInFlight = false;
            }
        }

        async reconcileTarget() {
            const targetActiveSessions =
                this.getTarget();

            this.elements.target.value =
                String(targetActiveSessions);

            this.persistState({
                target: targetActiveSessions
            });

            await this.runCommand(
                "RECONCILE_TARGET",
                this.endpoints.reconcile,
                {
                    targetActiveSessions,
                    simulateEvents:
                        this.getModules().events,
                    modules: this.getModules()
                });
        }

        getOrganicDelay() {
            return (
                organicMinimumDelayMs
                + Math.floor(
                    Math.random()
                    * (
                        organicMaximumDelayMs
                        - organicMinimumDelayMs
                        + 1
                    ))
            );
        }

        scheduleOrganicPulse(options = {}) {
            window.clearTimeout(
                this.state.organicTimerId);

            if (
                this.state.destroyed
                || !this.root.isConnected
                || !this.elements.organic.checked
            ) {
                this.state.organicTimerId = null;
                return;
            }

            const delay =
                options.immediate === true
                    ? 750
                    : this.getOrganicDelay();

            this.state.organicTimerId =
                window.setTimeout(
                    async () => {
                        if (
                            this.state.destroyed
                            || !this.root.isConnected
                            || !this.elements.organic.checked
                        ) {
                            return;
                        }

                        if (
                            !this.state.requestInFlight
                        ) {
                            await this.runCommand(
                                "ORGANIC_PULSE",
                                this.endpoints.pulse,
                                {
                                    targetActiveSessions:
                                        this.getTarget(),
                                    simulateEvents:
                                        this.getModules().events,
                                    modules:
                                        this.getModules()
                                });
                        }

                        this.scheduleOrganicPulse();
                    },
                    delay);
        }

        startOrganicMode(options = {}) {
            this.elements.organic.checked = true;

            this.persistState({
                organicEnabled: true
            });

            if (options.writeLog !== false) {
                this.appendLog(
                    this.messages.organicEnabled,
                    "warning");
            }

            this.scheduleOrganicPulse({
                immediate:
                    options.immediate === true
            });
        }

        stopOrganicMode(options = {}) {
            window.clearTimeout(
                this.state.organicTimerId);

            this.state.organicTimerId = null;
            this.elements.organic.checked = false;

            if (options.persist !== false) {
                this.persistState({
                    organicEnabled: false
                });
            }

            if (options.writeLog !== false) {
                this.appendLog(
                    this.messages.organicDisabled,
                    "info");
            }
        }

        async terminateSimulation() {
            if (
                !window.confirm(
                    this.messages.confirmTerminate)
            ) {
                return;
            }

            this.stopOrganicMode();

            await this.runCommand(
                "TERMINATE_SIMULATION",
                this.endpoints.reconcile,
                {
                    targetActiveSessions: 0,
                    simulateEvents:
                        this.getModules().events,
                    modules: {
                        ...this.getModules(),
                        sessions: true
                    }
                });
        }

        bindEvents() {
            const signal =
                this.lifecycleController.signal;

            this.openButton.addEventListener(
                "click",
                () => this.setOpen(true),
                { signal });

            this.elements.close.addEventListener(
                "click",
                () => this.setOpen(false),
                { signal });

            this.elements.overlay.addEventListener(
                "click",
                () => this.setOpen(false),
                { signal });

            this.elements.applyTarget.addEventListener(
                "click",
                () => {
                    void this.reconcileTarget();
                },
                { signal });

            this.elements.target.addEventListener(
                "change",
                () => this.persistState(),
                { signal });

            this.elements.target.addEventListener(
                "keydown",
                event => {
                    if (event.key === "Enter") {
                        event.preventDefault();
                        void this.reconcileTarget();
                    }
                },
                { signal });

            this.elements.organic.addEventListener(
                "change",
                () => {
                    if (
                        this.elements.organic.checked
                    ) {
                        this.startOrganicMode();
                    } else {
                        this.stopOrganicMode();
                    }
                },
                { signal });

            this.elements.terminate.addEventListener(
                "click",
                () => {
                    void this.terminateSimulation();
                },
                { signal });

            this.moduleInputs.forEach(input => {
                input.addEventListener(
                    "change",
                    () => this.persistState(),
                    { signal });
            });

            this.elements.critical.addEventListener(
                "click",
                () => {
                    void this.runCommand(
                        "CRITICAL_ALARM",
                        this.endpoints.critical,
                        {
                            simulateEvents:
                                this.getModules().events,
                            modules:
                                this.getModules()
                        });
                },
                { signal });

            this.elements.massDrop.addEventListener(
                "click",
                () => {
                    if (
                        !window.confirm(
                            this.messages
                                .confirmMassDrop)
                    ) {
                        return;
                    }

                    void this.runCommand(
                        "MASS_DROP",
                        this.endpoints.massDrop,
                        {
                            simulateEvents:
                                this.getModules().events,
                            modules:
                                this.getModules()
                        });
                },
                { signal });

            this.elements.purge.addEventListener(
                "click",
                () => {
                    if (
                        !window.confirm(
                            this.messages
                                .confirmPurge)
                    ) {
                        return;
                    }

                    this.stopOrganicMode({
                        writeLog: false
                    });

                    void this.runCommand(
                        "PURGE_SIMULATED_DATA",
                        this.endpoints.purge,
                        {
                            confirmation:
                                "PURGE_PC_PLAYER_DATA"
                        });
                },
                { signal });

            this.elements.wipe.addEventListener(
                "click",
                () => {
                    const firstConfirmation =
                        window.confirm(
                            this.messages
                                .confirmWipeFirst);

                    if (!firstConfirmation) {
                        return;
                    }

                    const finalConfirmation =
                        window.confirm(
                            this.messages
                                .confirmWipeFinal);

                    if (!finalConfirmation) {
                        return;
                    }

                    this.stopOrganicMode({
                        writeLog: false
                    });

                    void this.runCommand(
                        "WIPE_ALL_TELEMETRY",
                        this.endpoints.wipe,
                        {
                            confirmation:
                                "WIPE_ALL_TELEMETRY"
                        });
                },
                { signal });

            this.elements.clearLog.addEventListener(
                "click",
                () => {
                    this.state.pendingLogs = [];

                    this.elements.log
                        .replaceChildren();

                    this.appendLog(
                        this.messages.consoleCleared,
                        "info");
                },
                { signal });

            document.addEventListener(
                "keydown",
                event => {
                    if (
                        event.key === "Escape"
                        && this.state.isOpen
                    ) {
                        this.setOpen(false);
                    }
                },
                { signal });

            document.addEventListener(
                "visibilitychange",
                () => {
                    if (
                        document.hidden
                        || this.state.destroyed
                    ) {
                        return;
                    }

                    void this.refreshStatus({
                        immediate: true
                    });

                    if (this.elements.organic.checked) {
                        this.scheduleOrganicPulse({
                            immediate: true
                        });
                    }
                },
                { signal });
        }

        destroy(options = {}) {
            if (this.state.destroyed) {
                return;
            }

            this.persistState();

            this.state.destroyed = true;

            window.clearTimeout(
                this.state.organicTimerId);

            window.clearInterval(
                this.state.statusTimerId);

            window.clearTimeout(
                this.state.uiFlushTimerId);

            window.clearTimeout(
                this.state.logFlushTimerId);

            this.lifecycleController.abort();
            this.requestController.abort();

            this.state.requestInFlight = false;
            this.state.statusRequestInFlight = false;
            this.state.pendingStatus = null;
            this.state.pendingLogs = [];

            this.forceClosedState();

            if (options.restoreFocus === false) {
                this.state.lastFocusedElement = null;
            }
        }
    }

    class OrchestratorManager {
        constructor() {
            this.controller = null;
            this.observer = null;
            this.reinitializeTimerId = null;
            this.lifecycleController =
                new AbortController();

            this.suspended = false;
        }

        start() {
            this.reinitialize();

            const body = document.body;

            if (body) {
                this.observer =
                    new MutationObserver(() => {
                        if (this.suspended) {
                            return;
                        }

                        const currentRoot =
                            document.getElementById(
                                rootId);

                        const currentOpenButton =
                            document.querySelector(
                                openSelector);

                        if (
                            !this.controller
                            || !this.controller.isCurrent(
                                currentRoot,
                                currentOpenButton)
                        ) {
                            this.scheduleReinitialize();
                        }
                    });

                this.observer.observe(
                    body,
                    {
                        childList: true,
                        subtree: true
                    });
            }

            const signal =
                this.lifecycleController.signal;

            window.addEventListener(
                "pageshow",
                event => {
                    this.suspended = false;

                    if (event.persisted) {
                        resetVisualState();
                    }

                    this.scheduleReinitialize();
                },
                { signal });

            window.addEventListener(
                "pagehide",
                () => {
                    this.suspended = true;

                    window.clearTimeout(
                        this.reinitializeTimerId);

                    this.controller?.destroy({
                        restoreFocus: false
                    });

                    this.controller = null;
                    resetVisualState();
                },
                { signal });

            window.addEventListener(
                "beforeunload",
                () => {
                    this.suspended = true;

                    window.clearTimeout(
                        this.reinitializeTimerId);

                    this.controller?.destroy({
                        restoreFocus: false
                    });

                    resetVisualState();
                },
                { signal });
        }

        scheduleReinitialize() {
            if (this.suspended) {
                return;
            }

            window.clearTimeout(
                this.reinitializeTimerId);

            this.reinitializeTimerId =
                window.setTimeout(
                    () => this.reinitialize(),
                    80);
        }

        reinitialize() {
            if (this.suspended) {
                return;
            }

            window.clearTimeout(
                this.reinitializeTimerId);

            const root =
                document.getElementById(rootId);

            const openButton =
                document.querySelector(
                    openSelector);

            if (
                this.controller
                && this.controller.isCurrent(
                    root,
                    openButton)
            ) {
                return;
            }

            this.controller?.destroy({
                restoreFocus: false
            });

            this.controller = null;
            resetVisualState();

            if (!root || !openButton) {
                return;
            }

            try {
                const controller =
                    new SimulationOrchestrator(
                        root,
                        openButton);

                controller.initialize();
                this.controller = controller;
            } catch (error) {
                resetVisualState();

                console.error(
                    "Simulation Orchestrator initialization failed.",
                    error);
            }
        }

        destroy() {
            window.clearTimeout(
                this.reinitializeTimerId);

            this.controller?.destroy({
                restoreFocus: false
            });

            this.controller = null;

            this.observer?.disconnect();
            this.observer = null;

            this.lifecycleController.abort();
            resetVisualState();
        }
    }

    function bootstrap() {
        try {
            window[managerKey]?.destroy?.();

            const manager =
                new OrchestratorManager();

            window[managerKey] = manager;
            manager.start();
        } catch (error) {
            resetVisualState();

            console.error(
                "Simulation Orchestrator bootstrap failed.",
                error);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            bootstrap,
            {
                once: true
            });
    } else {
        bootstrap();
    }
})();
