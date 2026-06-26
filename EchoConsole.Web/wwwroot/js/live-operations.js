window.echoConsoleLiveOperations = (() => {
    const fleetById = new Map();
    const activeCardElements = new Map();
    const inactiveRowElements = new Map();

    let connection = null;
    let options = null;
    let elements = null;
    let refreshTimer = null;
    let pollingTimer = null;
    let serverClockTimer = null;
    let refreshInProgress = false;
    let serverTimeUtc = null;
    let localSyncTime = null;
    let relativeTimeFormatter = null;
    let numberFormatter = null;
    let selectedTab = "active";
    let searchTerm = "";
    let inactiveVisibleLimit = 100;
    let initialized = false;

    function init(config) {
        if (initialized) {
            return;
        }

        initialized = true;

        options = {
            hubUrl: config.hubUrl,
            snapshotUrl: config.snapshotUrl,
            refreshIntervalMilliseconds:
                Number(config.refreshIntervalMilliseconds) || 15000,
            inactivePageSize:
                Math.max(25, Number(config.inactivePageSize) || 100),
            culture: config.culture || "en",
            labels: config.labels || {},
            initialFleet: Array.isArray(config.initialFleet)
                ? config.initialFleet
                : []
        };

        inactiveVisibleLimit = options.inactivePageSize;

        relativeTimeFormatter = new Intl.RelativeTimeFormat(
            options.culture,
            {
                numeric: "always",
                style: "narrow"
            });

        numberFormatter = new Intl.NumberFormat(
            options.culture);

        elements = resolveElements();
        bindInterfaceEvents();
        replaceFleet(options.initialFleet);
        renderFleet();

        if (!window.signalR) {
            setConnectionState("Unavailable");
            startPolling();
            startServerClock();
            void refreshSnapshot();
            return;
        }

        buildConnection();
        void startConnection();
        startPolling();
        startServerClock();
        void refreshSnapshot();
    }

    function resolveElements() {
        return {
            search: document.getElementById("nodeSearch"),
            activeTab: document.getElementById("activeNodesTab"),
            inactiveTab: document.getElementById("inactiveFleetTab"),
            activePanel: document.getElementById("activeNodesPanel"),
            inactivePanel: document.getElementById("inactiveFleetPanel"),
            activeGrid: document.getElementById("live-ops-active-grid"),
            activeEmpty: document.getElementById("live-ops-active-empty-state"),
            inactiveBody: document.getElementById("live-ops-inactive-table-body"),
            inactiveEmpty: document.getElementById("live-ops-inactive-empty-state"),
            inactivePagination: document.getElementById("live-ops-inactive-pagination"),
            loadMoreInactive: document.getElementById("loadMoreInactiveNodes"),
            activeTabCount: document.getElementById("activeNodesTabCount"),
            inactiveTabCount: document.getElementById("inactiveFleetTabCount")
        };
    }

    function bindInterfaceEvents() {
        elements.search?.addEventListener("input", event => {
            searchTerm = normalizeSearchValue(event.target.value);
            inactiveVisibleLimit = options.inactivePageSize;
            renderFleet();
        });

        elements.activeTab?.addEventListener("click", () => {
            setSelectedTab("active");
        });

        elements.inactiveTab?.addEventListener("click", () => {
            setSelectedTab("inactive");
        });

        elements.loadMoreInactive?.addEventListener("click", () => {
            inactiveVisibleLimit += options.inactivePageSize;
            renderFleet();
        });
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
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("LiveOperationsRefresh", () => {
            scheduleRefresh(250);
        });

        connection.on("ReceiveTelemetryUpdate", message => {
            handleTelemetryEnvelope(message);
        });

        for (const eventName of [
            "sessionStarted",
            "sessionHeartbeat",
            "sessionEnded",
            "sessionExpired"
        ]) {
            connection.on(eventName, payload => {
                applyOperationalEvent(eventName, payload);
            });
        }

        connection.onreconnecting(() => {
            setConnectionState("Reconnecting");
        });

        connection.onreconnected(() => {
            setConnectionState("Connected");
            scheduleRefresh(0);
        });

        connection.onclose(() => {
            setConnectionState("Disconnected");

            window.setTimeout(
                () => void startConnection(),
                5000);
        });
    }

    async function startConnection() {
        if (!connection ||
            connection.state !== signalR.HubConnectionState.Disconnected) {
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

            window.setTimeout(
                () => void startConnection(),
                5000);
        }
    }

    function startPolling() {
        if (pollingTimer) {
            window.clearInterval(pollingTimer);
        }

        pollingTimer = window.setInterval(
            () => void refreshSnapshot(),
            options.refreshIntervalMilliseconds);
    }

    function startServerClock() {
        if (serverClockTimer) {
            window.clearInterval(serverClockTimer);
        }

        serverClockTimer = window.setInterval(() => {
            if (!serverTimeUtc || !localSyncTime) {
                return;
            }

            const elapsed = Date.now() - localSyncTime.getTime();
            const currentServerTime = new Date(
                serverTimeUtc.getTime() + elapsed);

            setText(
                "live-ops-server-time",
                formatUtcDateTime(currentServerTime));
        }, 1000);
    }

    function scheduleRefresh(delayMilliseconds) {
        if (refreshTimer) {
            window.clearTimeout(refreshTimer);
        }

        refreshTimer = window.setTimeout(
            () => void refreshSnapshot(),
            Math.max(0, Number(delayMilliseconds) || 0));
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
                    cache: "no-store",
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
            readValue(snapshot, "activeInstallations", "ActiveInstallations"));

        animateCounter(
            "live-ops-degraded-installations",
            readValue(snapshot, "degradedInstallations", "DegradedInstallations"));

        animateCounter(
            "live-ops-inactive-installations",
            readValue(snapshot, "inactiveInstallations", "InactiveInstallations"));

        animateCounter(
            "live-ops-active-sessions",
            readValue(snapshot, "activeSessions", "ActiveSessions"));

        animateCounter(
            "live-ops-events-5",
            readValue(snapshot, "eventsLast5Minutes", "EventsLast5Minutes"));

        animateCounter(
            "live-ops-events-15",
            readValue(snapshot, "eventsLast15Minutes", "EventsLast15Minutes"));

        animateCounter(
            "live-ops-unresolved-alerts",
            readValue(snapshot, "unresolvedAlerts", "UnresolvedAlerts"));

        const alertRate = Number(
            readValue(snapshot, "alertRatePerMinute", "AlertRatePerMinute") || 0);

        setText(
            "live-ops-alert-rate",
            alertRate.toLocaleString(
                options.culture,
                {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2
                }));

        setText(
            "live-ops-total-installations",
            formatNumber(
                readValue(snapshot, "totalInstallations", "TotalInstallations")));

        applySpikeState(snapshot);

        const activeNodes = readValue(
            snapshot,
            "activeNodes",
            "ActiveNodes");

        const inactiveFleet = readValue(
            snapshot,
            "inactiveFleet",
            "InactiveFleet");

        const installations = readValue(
            snapshot,
            "installations",
            "Installations");

        if (Array.isArray(activeNodes) || Array.isArray(inactiveFleet)) {
            replaceFleetSegments(
                Array.isArray(activeNodes)
                    ? activeNodes
                    : [],
                Array.isArray(inactiveFleet)
                    ? inactiveFleet
                    : []);
        } else {
            replaceFleet(
                Array.isArray(installations)
                    ? installations
                    : []);
        }

        renderFleet();

        const snapshotTime = readValue(
            snapshot,
            "serverTimeUtc",
            "ServerTimeUtc");

        syncServerTime(snapshotTime);

        const parsedSnapshotTime = new Date(snapshotTime);

        if (!Number.isNaN(parsedSnapshotTime.getTime())) {
            setText(
                "live-ops-last-refresh",
                formatUtcTime(parsedSnapshotTime));
        }
    }

    function applySpikeState(snapshot) {
        const state = String(
            readValue(snapshot, "eventSpikeState", "EventSpikeState") || "Quiet");

        const card = document.getElementById("live-ops-spike-card");
        const value = document.getElementById("live-ops-event-spike");
        const detail = document.getElementById("live-ops-event-spike-detail");

        if (value) {
            value.textContent = localizeSpikeState(state);
        }

        const current = Number(
            readValue(snapshot, "eventsLast5Minutes", "EventsLast5Minutes") || 0);

        const previous = Number(
            readValue(snapshot, "previousFiveMinuteEvents", "PreviousFiveMinuteEvents") || 0);

        const multiplier = Number(
            readValue(snapshot, "eventSpikeMultiplier", "EventSpikeMultiplier") || 0);

        if (detail) {
            detail.textContent =
                `${formatNumber(current)} ${getLabel("current", "current")} · ` +
                `${formatNumber(previous)} ${getLabel("previous", "previous")} · ` +
                `x${multiplier.toFixed(2)}`;
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

    function replaceFleet(rawInstallations) {
        const nextFleet = new Map();

        for (const rawInstallation of rawInstallations) {
            const installation = normalizeInstallation(rawInstallation);

            if (!installation.installationId) {
                continue;
            }

            nextFleet.set(
                installation.installationId,
                installation);
        }

        applyFleetMap(nextFleet);
    }

    function replaceFleetSegments(
        rawActiveNodes,
        rawInactiveFleet) {
        const nextFleet = new Map();

        for (const rawInstallation of rawActiveNodes) {
            const installation = normalizeInstallation(rawInstallation);

            if (!installation.installationId) {
                continue;
            }

            installation.operationalState = "Active";

            nextFleet.set(
                installation.installationId,
                installation);
        }

        for (const rawInstallation of rawInactiveFleet) {
            const installation = normalizeInstallation(rawInstallation);

            if (!installation.installationId) {
                continue;
            }

            if (installation.operationalState === "Active") {
                installation.operationalState = "Inactive";
            }

            nextFleet.set(
                installation.installationId,
                installation);
        }

        applyFleetMap(nextFleet);
    }

    function applyFleetMap(nextFleet) {
        fleetById.clear();

        for (const [installationId, installation] of nextFleet) {
            fleetById.set(installationId, installation);
        }

        pruneElementCaches();
    }

    function normalizeInstallation(rawInstallation) {
        const installationId = String(
            readValue(rawInstallation, "installationId", "InstallationId") || "")
            .toLowerCase();

        return {
            installationId,
            ownerUserId: readValue(
                rawInstallation,
                "ownerUserId",
                "OwnerUserId"),
            deviceName: normalizeDisplayValue(
                readValue(rawInstallation, "deviceName", "DeviceName")),
            platform: normalizeDisplayValue(
                readValue(rawInstallation, "platform", "Platform")),
            buildVersion: normalizeDisplayValue(
                readValue(rawInstallation, "buildVersion", "BuildVersion")),
            operationalState: normalizeState(
                readValue(rawInstallation, "operationalState", "OperationalState")),
            currentScene: normalizeDisplayValue(
                readValue(rawInstallation, "currentScene", "CurrentScene")),
            currentGameState: normalizeDisplayValue(
                readValue(rawInstallation, "currentGameState", "CurrentGameState")),
            lastUpdateUtc: normalizeDateValue(
                readValue(rawInstallation, "lastUpdateUtc", "LastUpdateUtc")),
            lastHeartbeatUtc: normalizeNullableDateValue(
                readValue(rawInstallation, "lastHeartbeatUtc", "LastHeartbeatUtc"))
        };
    }

    function renderFleet() {
        const fleet = Array.from(fleetById.values());
        const allActive = fleet.filter(
            installation => installation.operationalState === "Active");

        const allInactive = fleet.filter(
            installation => installation.operationalState !== "Active");

        const filteredActive = allActive
            .filter(matchesSearch)
            .sort(compareActiveInstallations);

        const filteredInactive = allInactive
            .filter(matchesSearch)
            .sort(compareInactiveInstallations);

        setElementText(
            elements.activeTabCount,
            formatNumber(allActive.length));

        setElementText(
            elements.inactiveTabCount,
            formatNumber(allInactive.length));

        if (selectedTab === "active") {
            renderActiveNodes(filteredActive);
            clearInactiveRows();
            setText(
                "live-ops-visible-installations",
                formatNumber(filteredActive.length));
        } else {
            renderInactiveFleet(filteredInactive);
            setText(
                "live-ops-visible-installations",
                formatNumber(filteredInactive.length));
        }

        updateTabPresentation();
    }

    function renderActiveNodes(installations) {
        if (!elements.activeGrid || !elements.activeEmpty) {
            return;
        }

        const fragment = document.createDocumentFragment();

        for (const installation of installations) {
            const card = ensureActiveCard(installation.installationId);
            updateActiveCard(card, installation);
            fragment.appendChild(card);
        }

        elements.activeGrid.replaceChildren(fragment);
        elements.activeEmpty.classList.toggle(
            "hidden",
            installations.length > 0);
    }

    function renderInactiveFleet(installations) {
        if (!elements.inactiveBody || !elements.inactiveEmpty) {
            return;
        }

        const visibleInstallations = installations.slice(
            0,
            inactiveVisibleLimit);

        const fragment = document.createDocumentFragment();

        for (const installation of visibleInstallations) {
            const row = ensureInactiveRow(installation.installationId);
            updateInactiveRow(row, installation);
            fragment.appendChild(row);
        }

        elements.inactiveBody.replaceChildren(fragment);
        elements.inactiveEmpty.classList.toggle(
            "hidden",
            installations.length > 0);

        const hasMore = visibleInstallations.length < installations.length;

        elements.inactivePagination?.classList.toggle(
            "hidden",
            !hasMore);

        if (elements.loadMoreInactive) {
            const remaining = Math.max(
                0,
                installations.length - visibleInstallations.length);

            elements.loadMoreInactive.textContent =
                `[ LOAD MORE TERMINALS · ${formatNumber(remaining)} REMAINING ]`;
        }
    }

    function clearInactiveRows() {
        elements.inactiveBody?.replaceChildren();
        elements.inactivePagination?.classList.add("hidden");
    }

    function ensureActiveCard(installationId) {
        const existing = activeCardElements.get(installationId);

        if (existing) {
            return existing;
        }

        const article = document.createElement("article");
        article.dataset.installationId = installationId;
        article.className =
            "rounded-2xl border border-emerald-400/35 bg-emerald-500/10 p-5 shadow-[0_0_24px_rgba(52,211,153,0.06)] transition duration-300 hover:-translate-y-1 hover:border-emerald-300/60";

        article.innerHTML = `
            <div class="flex items-start justify-between gap-4">
                <div class="min-w-0">
                    <div data-field="device-name" class="truncate text-base font-semibold text-white"></div>
                    <div data-field="platform-build" class="mt-1 text-xs text-slate-500"></div>
                </div>
                <span class="inline-flex shrink-0 items-center gap-2 rounded-full border border-emerald-300/20 bg-emerald-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-emerald-200">
                    <span class="h-2 w-2 rounded-full bg-emerald-300 shadow-[0_0_10px_rgba(110,231,183,0.9)] animate-pulse"></span>
                    <span data-field="state"></span>
                </span>
            </div>
            <div class="mt-5 grid grid-cols-2 gap-3">
                <div class="rounded-xl border border-emerald-400/10 bg-slate-950/55 p-3">
                    <div data-label="scene" class="text-[10px] uppercase tracking-[0.18em] text-slate-600"></div>
                    <div data-field="scene" class="mt-1 truncate text-sm text-emerald-50"></div>
                </div>
                <div class="rounded-xl border border-emerald-400/10 bg-slate-950/55 p-3">
                    <div data-label="game-state" class="text-[10px] uppercase tracking-[0.18em] text-slate-600"></div>
                    <div data-field="game-state" class="mt-1 truncate text-sm text-emerald-50"></div>
                </div>
            </div>
            <div class="mt-4 flex items-center justify-between gap-4 text-xs text-slate-600">
                <span data-field="last-update"></span>
                <span data-field="last-heartbeat"></span>
            </div>
            <div data-field="installation-id" class="mt-3 truncate font-mono text-[10px] text-slate-700"></div>
        `;

        activeCardElements.set(installationId, article);
        return article;
    }

    function updateActiveCard(card, installation) {
        setFieldText(card, "device-name", installation.deviceName);
        setFieldText(
            card,
            "platform-build",
            `${installation.platform} · ${installation.buildVersion}`);
        setFieldText(
            card,
            "state",
            localizeOperationalState("Active"));
        setLabelText(card, "scene", getLabel("scene", "Scene"));
        setFieldText(card, "scene", installation.currentScene);
        setLabelText(card, "game-state", getLabel("gameState", "Game State"));
        setFieldText(card, "game-state", installation.currentGameState);
        setFieldText(
            card,
            "last-update",
            `${getLabel("update", "Update")}: ${formatRelativeAge(installation.lastUpdateUtc)}`);
        setFieldText(
            card,
            "last-heartbeat",
            installation.lastHeartbeatUtc
                ? formatRelativeAge(installation.lastHeartbeatUtc)
                : getLabel("noHeartbeat", "No heartbeat"));
        setFieldText(card, "installation-id", installation.installationId);
        card.title = installation.installationId;
    }

    function ensureInactiveRow(installationId) {
        const existing = inactiveRowElements.get(installationId);

        if (existing) {
            return existing;
        }

        const row = document.createElement("tr");
        row.dataset.installationId = installationId;
        row.className = "transition hover:bg-slate-900/70";

        row.innerHTML = `
            <td class="px-4 py-3 align-middle">
                <div data-field="device-name" class="truncate font-medium text-slate-300"></div>
                <div data-field="installation-id" class="mt-1 truncate font-mono text-[9px] text-slate-700"></div>
            </td>
            <td class="px-4 py-3 align-middle">
                <span data-field="state-badge" class="inline-flex items-center gap-2 rounded-full border px-2.5 py-1 font-mono text-[9px] uppercase tracking-[0.12em]">
                    <span data-field="state-dot" class="h-1.5 w-1.5 rounded-full"></span>
                    <span data-field="state"></span>
                </span>
            </td>
            <td class="px-4 py-3 align-middle">
                <div data-field="scene" class="truncate text-slate-400"></div>
                <div data-field="game-state" class="mt-1 truncate text-[10px] text-slate-700"></div>
            </td>
            <td class="px-4 py-3 align-middle">
                <div data-field="build" class="truncate font-mono text-[10px] text-slate-500"></div>
                <div data-field="platform" class="mt-1 truncate text-[10px] text-slate-700"></div>
            </td>
            <td class="px-4 py-3 align-middle">
                <div data-field="last-heartbeat" class="text-slate-500"></div>
                <div data-field="last-update" class="mt-1 text-[10px] text-slate-700"></div>
            </td>
        `;

        inactiveRowElements.set(installationId, row);
        return row;
    }

    function updateInactiveRow(row, installation) {
        const isDegraded = installation.operationalState === "Degraded";
        const badge = row.querySelector('[data-field="state-badge"]');
        const dot = row.querySelector('[data-field="state-dot"]');

        if (badge) {
            badge.className = isDegraded
                ? "inline-flex items-center gap-2 rounded-full border border-amber-400/20 bg-amber-500/10 px-2.5 py-1 font-mono text-[9px] uppercase tracking-[0.12em] text-amber-300"
                : "inline-flex items-center gap-2 rounded-full border border-slate-700 bg-slate-900 px-2.5 py-1 font-mono text-[9px] uppercase tracking-[0.12em] text-slate-500";
        }

        if (dot) {
            dot.className = isDegraded
                ? "h-1.5 w-1.5 rounded-full bg-amber-400"
                : "h-1.5 w-1.5 rounded-full bg-slate-600";
        }

        setFieldText(row, "device-name", installation.deviceName);
        setFieldText(row, "installation-id", installation.installationId);
        setFieldText(
            row,
            "state",
            localizeOperationalState(installation.operationalState));
        setFieldText(row, "scene", installation.currentScene);
        setFieldText(row, "game-state", installation.currentGameState);
        setFieldText(row, "build", installation.buildVersion);
        setFieldText(row, "platform", installation.platform);
        setFieldText(
            row,
            "last-heartbeat",
            installation.lastHeartbeatUtc
                ? formatRelativeAge(installation.lastHeartbeatUtc)
                : getLabel("noHeartbeat", "No heartbeat"));
        setFieldText(
            row,
            "last-update",
            `${getLabel("update", "Update")}: ${formatRelativeAge(installation.lastUpdateUtc)}`);
        row.title = installation.installationId;
    }

    function setSelectedTab(tabName) {
        selectedTab = tabName === "inactive"
            ? "inactive"
            : "active";

        inactiveVisibleLimit = options.inactivePageSize;
        renderFleet();
    }

    function updateTabPresentation() {
        const activeSelected = selectedTab === "active";

        elements.activePanel?.classList.toggle(
            "hidden",
            !activeSelected);

        elements.inactivePanel?.classList.toggle(
            "hidden",
            activeSelected);

        elements.activeTab?.setAttribute(
            "aria-selected",
            String(activeSelected));

        elements.inactiveTab?.setAttribute(
            "aria-selected",
            String(!activeSelected));

        if (elements.activeTab) {
            elements.activeTab.className = activeSelected
                ? "shrink-0 rounded-xl border border-emerald-400/50 bg-emerald-500/15 px-4 py-3 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-emerald-200 shadow-[0_0_18px_rgba(52,211,153,0.08)]"
                : "shrink-0 rounded-xl border border-slate-700 bg-slate-900/70 px-4 py-3 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500";
        }

        if (elements.inactiveTab) {
            elements.inactiveTab.className = !activeSelected
                ? "shrink-0 rounded-xl border border-cyan-400/40 bg-cyan-500/10 px-4 py-3 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-cyan-200 shadow-[0_0_18px_rgba(34,211,238,0.06)]"
                : "shrink-0 rounded-xl border border-slate-700 bg-slate-900/70 px-4 py-3 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500";
        }
    }

    function handleTelemetryEnvelope(message) {
        const outerPayload = readValue(message, "payload", "Payload") ?? message;
        const eventType = readValue(outerPayload, "eventType", "EventType");
        const eventPayload = readValue(outerPayload, "payload", "Payload");

        if (typeof eventType !== "string") {
            return;
        }

        applyOperationalEvent(
            eventType,
            eventPayload ?? outerPayload);
    }

    function applyOperationalEvent(eventName, rawPayload) {
        const normalizedEventName = String(eventName || "").toLowerCase();

        if (!["sessionstarted", "sessionheartbeat", "sessionended", "sessionexpired"]
            .includes(normalizedEventName)) {
            return;
        }

        const payload = unwrapPayload(rawPayload);
        const installationId = String(
            readValue(payload, "installationId", "InstallationId") || "")
            .toLowerCase();

        if (!installationId) {
            scheduleRefresh(100);
            return;
        }

        const existing = fleetById.get(installationId) ?? {
            installationId,
            ownerUserId: null,
            deviceName: `NODE-${installationId.substring(0, 8).toUpperCase()}`,
            platform: "-",
            buildVersion: "-",
            operationalState: "Inactive",
            currentScene: "-",
            currentGameState: "-",
            lastUpdateUtc: new Date().toISOString(),
            lastHeartbeatUtc: null
        };

        const eventTime = normalizeDateValue(
            readValue(payload, "serverTimeUtc", "ServerTimeUtc") ??
            readValue(payload, "startedAtUtc", "StartedAtUtc") ??
            readValue(payload, "endedAtUtc", "EndedAtUtc") ??
            readValue(payload, "lastHeartbeatUtc", "LastHeartbeatUtc") ??
            new Date().toISOString());

        if (normalizedEventName === "sessionstarted" ||
            normalizedEventName === "sessionheartbeat") {
            existing.operationalState = "Active";
            existing.ownerUserId =
                readValue(payload, "ownerUserId", "OwnerUserId") ??
                existing.ownerUserId;
            existing.deviceName = normalizeDisplayValue(
                readValue(payload, "deviceName", "DeviceName") ??
                existing.deviceName);
            existing.buildVersion = normalizeDisplayValue(
                readValue(payload, "buildVersion", "BuildVersion") ??
                existing.buildVersion);
            existing.currentScene = normalizeDisplayValue(
                readValue(payload, "currentScene", "CurrentScene") ??
                readValue(payload, "scene", "Scene") ??
                existing.currentScene);
            existing.currentGameState = normalizeDisplayValue(
                readValue(payload, "currentGameState", "CurrentGameState") ??
                readValue(payload, "gameState", "GameState") ??
                existing.currentGameState);
            existing.lastHeartbeatUtc = eventTime;
            existing.lastUpdateUtc = eventTime;
        } else {
            existing.operationalState = "Degraded";
            existing.lastUpdateUtc = eventTime;

            const heartbeatValue = readValue(
                payload,
                "lastHeartbeatUtc",
                "LastHeartbeatUtc");

            if (heartbeatValue) {
                existing.lastHeartbeatUtc = normalizeDateValue(heartbeatValue);
            }
        }

        fleetById.set(installationId, existing);
        pruneElementCaches();
        renderFleet();
        scheduleRefresh(150);
    }

    function unwrapPayload(rawPayload) {
        let current = rawPayload;

        for (let depth = 0; depth < 3; depth += 1) {
            if (!current || typeof current !== "object") {
                break;
            }

            const nestedPayload = readValue(current, "payload", "Payload");
            const hasInstallationId =
                readValue(current, "installationId", "InstallationId") !== undefined;

            if (hasInstallationId || nestedPayload === undefined) {
                break;
            }

            current = nestedPayload;
        }

        return current && typeof current === "object"
            ? current
            : {};
    }

    function pruneElementCaches() {
        for (const installationId of activeCardElements.keys()) {
            const installation = fleetById.get(installationId);

            if (!installation || installation.operationalState !== "Active") {
                activeCardElements.get(installationId)?.remove();
                activeCardElements.delete(installationId);
            }
        }

        for (const installationId of inactiveRowElements.keys()) {
            const installation = fleetById.get(installationId);

            if (!installation || installation.operationalState === "Active") {
                inactiveRowElements.get(installationId)?.remove();
                inactiveRowElements.delete(installationId);
            }
        }
    }

    function matchesSearch(installation) {
        if (!searchTerm) {
            return true;
        }

        const searchableValue = normalizeSearchValue(
            `${installation.deviceName} ${installation.currentScene}`);

        return searchableValue.includes(searchTerm);
    }

    function compareActiveInstallations(left, right) {
        const heartbeatDifference =
            dateToTimestamp(right.lastHeartbeatUtc) -
            dateToTimestamp(left.lastHeartbeatUtc);

        if (heartbeatDifference !== 0) {
            return heartbeatDifference;
        }

        return left.deviceName.localeCompare(
            right.deviceName,
            options.culture,
            { sensitivity: "base" });
    }

    function compareInactiveInstallations(left, right) {
        const leftPriority = left.operationalState === "Degraded" ? 0 : 1;
        const rightPriority = right.operationalState === "Degraded" ? 0 : 1;

        if (leftPriority !== rightPriority) {
            return leftPriority - rightPriority;
        }

        const updateDifference =
            dateToTimestamp(right.lastUpdateUtc) -
            dateToTimestamp(left.lastUpdateUtc);

        if (updateDifference !== 0) {
            return updateDifference;
        }

        return left.deviceName.localeCompare(
            right.deviceName,
            options.culture,
            { sensitivity: "base" });
    }

    function setConnectionState(state) {
        const badge = document.getElementById("live-ops-connection-badge");
        const dot = document.getElementById("live-ops-connection-dot");
        const text = document.getElementById("live-ops-connection-text");

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

        if (state === "Reconnecting" || state === "Connecting") {
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

        const startValue = parseCounterValue(element.textContent);
        const target = Number(targetValue || 0);
        const startedAt = performance.now();
        const duration = 450;

        function update(now) {
            const progress = Math.min(
                1,
                (now - startedAt) / duration);

            const eased = 1 - Math.pow(1 - progress, 3);
            const value = Math.round(
                startValue + (target - startValue) * eased);

            element.textContent = formatNumber(value);

            if (progress < 1) {
                window.requestAnimationFrame(update);
            }
        }

        window.requestAnimationFrame(update);
    }

    function parseCounterValue(value) {
        const digits = String(value || "").replace(/[^0-9-]/g, "");
        return Number(digits) || 0;
    }

    function normalizeState(value) {
        const normalizedValue = String(value || "").toLowerCase();

        if (normalizedValue === "active") {
            return "Active";
        }

        if (normalizedValue === "degraded") {
            return "Degraded";
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
            Math.floor((Date.now() - date.getTime()) / 1000));

        if (seconds < 60) {
            return relativeTimeFormatter.format(-seconds, "second");
        }

        const minutes = Math.floor(seconds / 60);

        if (minutes < 60) {
            return relativeTimeFormatter.format(-minutes, "minute");
        }

        const hours = Math.floor(minutes / 60);

        if (hours < 24) {
            return relativeTimeFormatter.format(-hours, "hour");
        }

        const days = Math.floor(hours / 24);
        return relativeTimeFormatter.format(-days, "day");
    }

    function formatUtcDateTime(value) {
        return value
            .toISOString()
            .replace("T", " ")
            .substring(0, 19);
    }

    function formatUtcTime(value) {
        return `${value.toISOString().substring(11, 19)} UTC`;
    }

    function formatNumber(value) {
        return numberFormatter.format(Number(value || 0));
    }

    function normalizeDisplayValue(value) {
        const normalized = String(value ?? "").trim();
        return normalized.length > 0 ? normalized : "-";
    }

    function normalizeDateValue(value) {
        const parsed = new Date(value);

        return Number.isNaN(parsed.getTime())
            ? new Date().toISOString()
            : parsed.toISOString();
    }

    function normalizeNullableDateValue(value) {
        if (value === null || value === undefined || value === "") {
            return null;
        }

        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
    }

    function normalizeSearchValue(value) {
        return String(value || "")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .trim()
            .toLowerCase();
    }

    function dateToTimestamp(value) {
        const timestamp = new Date(value).getTime();
        return Number.isNaN(timestamp) ? 0 : timestamp;
    }

    function getLabel(key, fallback) {
        const value = options.labels?.[key];

        return typeof value === "string" && value.length > 0
            ? value
            : fallback;
    }

    function readValue(source, camelCaseName, pascalCaseName) {
        if (!source || typeof source !== "object") {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(source, camelCaseName)) {
            return source[camelCaseName];
        }

        if (Object.prototype.hasOwnProperty.call(source, pascalCaseName)) {
            return source[pascalCaseName];
        }

        return undefined;
    }

    function setText(id, value) {
        const element = document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
    }

    function setElementText(element, value) {
        if (element) {
            element.textContent = value;
        }
    }

    function setFieldText(root, fieldName, value) {
        const field = root.querySelector(
            `[data-field="${fieldName}"]`);

        if (field) {
            field.textContent = value;
        }
    }

    function setLabelText(root, labelName, value) {
        const label = root.querySelector(
            `[data-label="${labelName}"]`);

        if (label) {
            label.textContent = value;
        }
    }

    return {
        init
    };
})();
