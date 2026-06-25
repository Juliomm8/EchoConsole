window.echoConsoleSessionEvents = (() => {
    "use strict";

    let options = null;
    let connection = null;
    let reconnectTimer = null;
    let flushTimer = null;
    let pendingEvents = new Map();
    const purgeStates = new Map();

    const flushDelayMs = 120;

    function init(config) {
        options = {
            ...config,
            page: Number(config.page) || 1,
            maxRows: Number(config.maxRows) || 50,
            eventType: config.eventType || "",
            buildVersion: config.buildVersion || "",
            labels: config.labels || {}
        };

        bindActions();
        enforceRowLimit();
        buildConnection();
        void startConnection();
    }

    function bindActions() {
        document.addEventListener("click", event => {
            const jsonButton = event.target.closest("[data-view-json]");
            if (jsonButton) {
                toggleJson(jsonButton);
                return;
            }

            const timelineButton = event.target.closest("[data-open-timeline]");
            if (timelineButton) {
                void openTimeline(timelineButton.dataset.sessionId);
                return;
            }

            const purgeButton = event.target.closest("[data-purge-session]");
            if (purgeButton) {
                startPurgeCountdown(purgeButton.dataset.sessionId);
                return;
            }

            const cancelButton = event.target.closest("[data-cancel-purge]");
            if (cancelButton) {
                cancelPurge(cancelButton.dataset.sessionId);
                return;
            }

            if (event.target.closest("[data-timeline-close]")) {
                closeTimeline();
            }
        });

        const dialog = document.getElementById(options.timelineDialogId);
        dialog?.addEventListener("click", event => {
            if (event.target === dialog) {
                closeTimeline();
            }
        });
    }

    function buildConnection() {
        if (!window.signalR || !options.hubUrl) {
            setStreamStatus(false, "SIGNALR_UNAVAILABLE");
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl, { withCredentials: true })
            .withAutomaticReconnect([0, 1500, 3000, 6000, 12000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("ReceiveTelemetryUpdate", envelope => {
            const operational = findOperationalEnvelope(envelope);
            if (!operational) {
                return;
            }

            const normalized = normalizeRealtimeEvent(
                operational.eventType,
                operational.payload,
                envelope?.serverTimeUtc);

            if (!normalized || !matchesCurrentFilters(normalized)) {
                return;
            }

            queueRealtimeEvent(normalized);
        });

        connection.onreconnecting(() => {
            setStreamStatus(false, "RECONNECTING");
        });

        connection.onreconnected(() => {
            setStreamStatus(true, options.labels.streamOnline || "STREAM ONLINE");
        });

        connection.onclose(() => {
            setStreamStatus(false, "DISCONNECTED");
            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(
                () => void startConnection(),
                5000);
        });
    }

    async function startConnection() {
        if (
            !connection ||
            connection.state !== signalR.HubConnectionState.Disconnected
        ) {
            return;
        }

        try {
            await connection.start();
            setStreamStatus(true, options.labels.streamOnline || "STREAM ONLINE");
        } catch (error) {
            console.error("Session events SignalR connection failed.", error);
            setStreamStatus(false, "RETRY_PENDING");
            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(
                () => void startConnection(),
                5000);
        }
    }

    function findOperationalEnvelope(value) {
        if (!value || typeof value !== "object") {
            return null;
        }

        if (
            typeof value.eventType === "string" &&
            value.payload &&
            typeof value.payload === "object"
        ) {
            return value;
        }

        return findOperationalEnvelope(value.payload);
    }

    function normalizeRealtimeEvent(sourceEventName, payload, serverTimeUtc) {
        if (!payload || typeof payload !== "object") {
            return null;
        }

        if (sourceEventName === "sessionStarted") {
            const createdAtUtc = payload.startedAtUtc || serverTimeUtc;
            return {
                key: `live-start-${payload.sessionId}-${createdAtUtc}`,
                id: `LIVE-${shortId(payload.sessionId)}`,
                sessionId: payload.sessionId,
                installationId: payload.installationId,
                ownerUserId: payload.ownerUserId,
                deviceName: payload.deviceName || "-",
                buildVersion: payload.buildVersion || "-",
                eventType: "SessionStarted",
                scene: payload.currentScene || "-",
                gameState: payload.currentGameState || "-",
                phase: payload.currentPhase || "-",
                payloadJson: prettyJson(payload),
                clientTimeUtc: null,
                createdAtUtc
            };
        }

        if (sourceEventName === "sessionHeartbeat") {
            const createdAtUtc = payload.lastHeartbeatUtc || serverTimeUtc;
            return {
                key: `live-heartbeat-${payload.sessionId}-${createdAtUtc}`,
                id: `HB-${shortId(payload.sessionId)}`,
                sessionId: payload.sessionId,
                installationId: payload.installationId,
                ownerUserId: payload.ownerUserId,
                deviceName: payload.deviceName || "-",
                buildVersion: payload.buildVersion || "-",
                eventType: "TelemetryHeartbeat",
                scene: payload.currentScene || "-",
                gameState: payload.currentGameState || "-",
                phase: payload.currentPhase || "-",
                coalesceKey: `heartbeat-${payload.sessionId}`,
                payloadJson: prettyJson(payload),
                clientTimeUtc: null,
                createdAtUtc
            };
        }

        if (sourceEventName === "sessionEventRecorded") {
            return {
                key: `db-${payload.eventId}`,
                id: payload.eventId,
                sessionId: payload.sessionId,
                installationId: payload.installationId,
                ownerUserId: payload.ownerUserId,
                deviceName: payload.deviceName || "-",
                buildVersion: payload.buildVersion || "-",
                eventType: payload.eventType || "SessionEvent",
                scene: payload.scene || "-",
                gameState: payload.gameState || "-",
                phase: payload.phase || "-",
                payloadJson: formatPayload(payload.payloadJson),
                clientTimeUtc: payload.clientTimeUtc,
                createdAtUtc: payload.createdAtUtc || serverTimeUtc
            };
        }

        return null;
    }

    function matchesCurrentFilters(item) {
        if (options.page !== 1) {
            return false;
        }

        if (
            options.eventType &&
            item.eventType.toLowerCase() !== options.eventType.toLowerCase()
        ) {
            return false;
        }

        if (
            options.buildVersion &&
            item.buildVersion.toLowerCase() !== options.buildVersion.toLowerCase()
        ) {
            return false;
        }

        const timestamp = new Date(item.createdAtUtc);
        if (Number.isNaN(timestamp.getTime())) {
            return false;
        }

        if (options.fromUtc) {
            const lowerBound = new Date(`${options.fromUtc}T00:00:00Z`);
            if (timestamp < lowerBound) {
                return false;
            }
        }

        if (options.toUtc) {
            const upperBound = new Date(`${options.toUtc}T00:00:00Z`);
            upperBound.setUTCDate(upperBound.getUTCDate() + 1);
            if (timestamp >= upperBound) {
                return false;
            }
        }

        return true;
    }

    function queueRealtimeEvent(item) {
        if (
            document.querySelector(
                `[data-session-event-row][data-event-key="${item.key}"]`)
        ) {
            return;
        }

        pendingEvents.set(item.coalesceKey || item.key, item);

        if (flushTimer) {
            return;
        }

        flushTimer = window.setTimeout(flushRealtimeEvents, flushDelayMs);
    }

    function flushRealtimeEvents() {
        flushTimer = null;

        const body = document.getElementById(options.tableBodyId);
        if (!body || pendingEvents.size === 0) {
            return;
        }

        const items = Array.from(pendingEvents.values())
            .sort((left, right) =>
                new Date(right.createdAtUtc) - new Date(left.createdAtUtc))
            .slice(0, options.maxRows);

        pendingEvents = new Map();
        body.querySelector("[data-session-events-empty]")?.remove();

        const fragment = document.createDocumentFragment();

        for (const item of items) {
            const pair = createRowPair(item);
            fragment.append(pair.mainRow);
            if (pair.jsonRow) {
                fragment.append(pair.jsonRow);
            }
        }

        body.insertBefore(fragment, body.firstChild);

        for (const item of items) {
            const row = body.querySelector(
                `[data-session-event-row][data-event-key="${item.key}"]`);
            row?.classList.add("session-event-crt-flash");
            window.setTimeout(
                () => row?.classList.remove("session-event-crt-flash"),
                950);
        }

        enforceRowLimit();
        incrementTotalCount(items.length);
    }

    function createRowPair(item) {
        const row = document.createElement("tr");
        row.dataset.sessionEventRow = "true";
        row.dataset.eventKey = item.key;
        row.dataset.sessionId = item.sessionId || "";
        row.className = "border-b border-slate-900/80 align-top text-xs text-slate-300 transition-all duration-300 hover:bg-slate-900/55";

        row.append(
            createTimeCell(item),
            createEventCell(item),
            createSceneCell(item),
            createTextCell(item.buildVersion || "-", "text-slate-400"),
            createDeviceCell(item),
            createTextCell(
                item.ownerUserId ? `USER #${item.ownerUserId}` : options.labels.unclaimed,
                "text-slate-400"),
            createSessionCell(item),
            createActionsCell(item));

        let jsonRow = null;
        if (item.payloadJson) {
            jsonRow = document.createElement("tr");
            jsonRow.dataset.jsonRow = "true";
            jsonRow.dataset.jsonFor = item.key;
            jsonRow.className = "hidden border-b border-slate-900 bg-[#010302]";

            const cell = document.createElement("td");
            cell.colSpan = 8;
            cell.className = "px-4 py-4";

            const pre = document.createElement("pre");
            pre.className = "max-h-[280px] overflow-auto border border-slate-800 bg-black p-4 text-[10px] leading-5 text-green-300";

            const code = document.createElement("code");
            code.className = "json";
            code.textContent = item.payloadJson;

            pre.appendChild(code);
            cell.appendChild(pre);
            jsonRow.appendChild(cell);
        }

        return { mainRow: row, jsonRow };
    }

    function createTimeCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4";

        const server = document.createElement("div");
        server.className = "font-medium text-green-400";
        server.textContent = formatUtc(item.createdAtUtc);

        const client = document.createElement("div");
        client.className = "mt-1 text-[9px] text-slate-700";
        client.textContent = `${options.labels.clientTime}: ${item.clientTimeUtc ? formatUtc(item.clientTimeUtc) : "-"}`;

        cell.append(server, client);
        return cell;
    }

    function createEventCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4";

        const badge = document.createElement("span");
        badge.className = "inline-flex min-w-[150px] items-center justify-center whitespace-nowrap border border-cyan-900/60 bg-cyan-950/20 px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.13em] text-cyan-400";
        badge.textContent = item.eventType;

        const id = document.createElement("div");
        id.className = "mt-2 text-[9px] text-slate-700";
        id.textContent = `ID: ${item.id}`;

        cell.append(badge, id);
        return cell;
    }

    function createSceneCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4";

        const scene = document.createElement("div");
        scene.className = "font-medium text-slate-200";
        scene.textContent = item.scene || "-";

        const state = document.createElement("div");
        state.className = "mt-1 text-[9px] text-slate-600";
        state.textContent = `${item.gameState || "-"} / ${item.phase || "-"}`;

        cell.append(scene, state);
        return cell;
    }

    function createTextCell(value, className) {
        const cell = document.createElement("td");
        cell.className = `px-4 py-4 ${className}`;
        cell.textContent = value;
        return cell;
    }

    function createDeviceCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4";

        const name = document.createElement("div");
        name.className = "font-medium text-slate-200";
        name.textContent = item.deviceName || "-";

        const installation = document.createElement("div");
        installation.className = "mt-1 max-w-[220px] truncate text-[9px] text-slate-700";
        installation.title = item.installationId || "";
        installation.textContent = item.installationId || "-";

        cell.append(name, installation);
        return cell;
    }

    function createSessionCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4";

        const session = document.createElement("div");
        session.className = "max-w-[230px] truncate text-[9px] text-slate-500";
        session.title = item.sessionId || "";
        session.textContent = item.sessionId || "-";

        const button = document.createElement("button");
        button.type = "button";
        button.dataset.openTimeline = "true";
        button.dataset.sessionId = item.sessionId || "";
        button.className = "mt-3 inline-flex h-8 min-w-[150px] items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-3 text-[9px] font-semibold uppercase tracking-[0.13em] text-slate-300 transition-colors hover:bg-slate-800/90 hover:text-green-400";
        button.textContent = `[ ${options.labels.openTimeline} ]`;

        cell.append(session, button);
        return cell;
    }

    function createActionsCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 text-right";

        const wrapper = document.createElement("div");
        wrapper.className = "inline-flex min-w-[126px] flex-col items-end gap-2 whitespace-nowrap";

        if (item.payloadJson) {
            const jsonButton = document.createElement("button");
            jsonButton.type = "button";
            jsonButton.dataset.viewJson = "true";
            jsonButton.dataset.eventKey = item.key;
            jsonButton.setAttribute("aria-expanded", "false");
            jsonButton.className = "inline-flex h-8 min-w-[110px] items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-3 text-[9px] font-semibold uppercase tracking-[0.13em] text-slate-300 transition-colors hover:bg-slate-800/90 hover:text-green-400";
            jsonButton.textContent = `[ ${options.labels.viewJson} ]`;
            wrapper.appendChild(jsonButton);
        } else {
            const empty = document.createElement("span");
            empty.className = "text-[9px] uppercase tracking-[0.14em] text-slate-700";
            empty.textContent = options.labels.noPayload;
            wrapper.appendChild(empty);
        }

        const purgeButton = document.createElement("button");
        purgeButton.type = "button";
        purgeButton.dataset.purgeSession = "true";
        purgeButton.dataset.sessionId = item.sessionId || "";
        purgeButton.className = "btn-purge inline-flex h-8 min-w-[110px] items-center justify-center whitespace-nowrap border border-red-900/60 bg-red-950/20 px-3 text-[9px] font-semibold uppercase tracking-[0.13em] text-red-500 transition-colors hover:bg-red-900/30 hover:text-red-300 disabled:cursor-wait disabled:opacity-70";
        purgeButton.textContent = `[ ${options.labels.purge} ]`;

        const cancelButton = document.createElement("button");
        cancelButton.type = "button";
        cancelButton.dataset.cancelPurge = "true";
        cancelButton.dataset.sessionId = item.sessionId || "";
        cancelButton.className = "hidden h-7 min-w-[110px] items-center justify-center whitespace-nowrap text-[9px] font-semibold uppercase tracking-[0.13em] text-amber-400 hover:text-amber-300";
        cancelButton.textContent = `[ ${options.labels.cancel} ]`;

        wrapper.append(purgeButton, cancelButton);
        cell.appendChild(wrapper);
        return cell;
    }

    function startPurgeCountdown(sessionId) {
        if (!sessionId || purgeStates.has(sessionId)) {
            return;
        }

        const state = {
            remainingSeconds: 3,
            intervalId: null
        };

        purgeStates.set(sessionId, state);
        renderPurgeState(sessionId, state.remainingSeconds);

        state.intervalId = window.setInterval(() => {
            state.remainingSeconds -= 1;

            if (state.remainingSeconds <= 0) {
                window.clearInterval(state.intervalId);
                state.intervalId = null;
                void dispatchPurge(sessionId);
                return;
            }

            renderPurgeState(sessionId, state.remainingSeconds);
        }, 1000);
    }

    function cancelPurge(sessionId) {
        const state = purgeStates.get(sessionId);
        if (!state) {
            return;
        }

        if (state.intervalId) {
            window.clearInterval(state.intervalId);
        }

        purgeStates.delete(sessionId);
        resetPurgeControls(sessionId);
    }

    function renderPurgeState(sessionId, remainingSeconds) {
        for (const button of getPurgeButtons(sessionId)) {
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            button.textContent = formatPurgingLabel(remainingSeconds);
        }

        for (const button of getCancelButtons(sessionId)) {
            button.classList.remove("hidden");
            button.classList.add("inline-flex");
        }
    }

    function resetPurgeControls(sessionId) {
        for (const button of getPurgeButtons(sessionId)) {
            button.disabled = false;
            button.removeAttribute("aria-busy");
            button.textContent = `[ ${options.labels.purge} ]`;
        }

        for (const button of getCancelButtons(sessionId)) {
            button.classList.add("hidden");
            button.classList.remove("inline-flex");
        }
    }

    function formatPurgingLabel(remainingSeconds) {
        const template = options.labels.purgingIn || "Purging in {0}s...";
        return template.replace("{0}", String(remainingSeconds));
    }

    async function dispatchPurge(sessionId) {
        const state = purgeStates.get(sessionId);
        if (!state) {
            return;
        }

        for (const button of getCancelButtons(sessionId)) {
            button.disabled = true;
        }

        try {
            const url = new URL(
                `${options.purgeUrl}/${encodeURIComponent(sessionId)}`,
                window.location.origin);

            if (options.eventType) {
                url.searchParams.set("eventType", options.eventType);
            }

            if (options.buildVersion) {
                url.searchParams.set("buildVersion", options.buildVersion);
            }

            if (options.fromUtc) {
                url.searchParams.set("fromDate", options.fromUtc);
            }

            if (options.toUtc) {
                url.searchParams.set("toDate", options.toUtc);
            }

            const response = await fetch(url, {
                method: "DELETE",
                credentials: "same-origin",
                headers: {
                    Accept: "application/json",
                    RequestVerificationToken: options.antiForgeryToken
                }
            });

            if (!response.ok) {
                throw new Error(
                    `Session purge failed with status ${response.status}.`);
            }

            const result = await response.json();
            purgeStates.delete(sessionId);

            const removedVisibleRowCount =
                removeSessionRows(sessionId);

            const deletedMatchingEventCount = Number(
                result.deletedMatchingEventCount);

            const decrementAmount =
                Number.isFinite(deletedMatchingEventCount) &&
                deletedMatchingEventCount > 0
                    ? deletedMatchingEventCount
                    : removedVisibleRowCount;

            incrementTotalCount(-decrementAmount);
        } catch (error) {
            console.error("Session purge failed.", error);
            purgeStates.delete(sessionId);

            for (const button of getPurgeButtons(sessionId)) {
                button.disabled = false;
                button.removeAttribute("aria-busy");
                button.textContent = `[ ${options.labels.purgeFailed} ]`;
            }

            for (const button of getCancelButtons(sessionId)) {
                button.disabled = false;
                button.classList.add("hidden");
                button.classList.remove("inline-flex");
            }

            window.setTimeout(
                () => resetPurgeControls(sessionId),
                1600);
        }
    }

    function removeSessionRows(sessionId) {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return 0;
        }

        const rows = Array.from(
            body.querySelectorAll("[data-session-event-row]"))
            .filter(row => row.dataset.sessionId === sessionId);

        for (const row of rows) {
            const eventKey = row.dataset.eventKey;
            const jsonRow = eventKey
                ? body.querySelector(
                    `[data-json-row][data-json-for="${eventKey}"]`)
                : null;

            row.classList.add("opacity-0", "translate-x-2");
            jsonRow?.classList.add("opacity-0");

            window.setTimeout(() => {
                jsonRow?.remove();
                row.remove();
                ensureEmptyState();
            }, 300);
        }

        return rows.length;
    }

    function ensureEmptyState() {
        const body = document.getElementById(options.tableBodyId);
        if (!body || body.querySelector("[data-session-event-row]")) {
            return;
        }

        if (body.querySelector("[data-session-events-empty]")) {
            return;
        }

        const row = document.createElement("tr");
        row.dataset.sessionEventsEmpty = "true";

        const cell = document.createElement("td");
        cell.colSpan = 8;
        cell.className = "px-5 py-14 text-center text-xs uppercase tracking-[0.18em] text-slate-600";
        cell.textContent = options.labels.empty || "No events found";

        row.appendChild(cell);
        body.appendChild(row);
    }

    function getPurgeButtons(sessionId) {
        return Array.from(
            document.querySelectorAll("[data-purge-session]"))
            .filter(button => button.dataset.sessionId === sessionId);
    }

    function getCancelButtons(sessionId) {
        return Array.from(
            document.querySelectorAll("[data-cancel-purge]"))
            .filter(button => button.dataset.sessionId === sessionId);
    }

    function toggleJson(button) {
        const key = button.dataset.eventKey;
        const row = document.querySelector(`[data-json-row][data-json-for="${key}"]`);
        if (!row) {
            return;
        }

        const isHidden = row.classList.contains("hidden");
        row.classList.toggle("hidden", !isHidden);
        button.setAttribute("aria-expanded", String(isHidden));
    }

    function enforceRowLimit() {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        const rows = Array.from(body.querySelectorAll("[data-session-event-row]"));
        while (rows.length > options.maxRows) {
            const row = rows.pop();
            const key = row?.dataset.eventKey;
            if (key) {
                body.querySelector(`[data-json-row][data-json-for="${key}"]`)?.remove();
            }
            row?.remove();
        }
    }

    async function openTimeline(sessionId) {
        if (!sessionId) {
            return;
        }

        const dialog = document.getElementById(options.timelineDialogId);
        const loading = dialog?.querySelector("[data-timeline-loading]");
        const eventsContainer = dialog?.querySelector("[data-timeline-events]");
        const summary = dialog?.querySelector("[data-timeline-summary]");

        if (!dialog || !loading || !eventsContainer || !summary) {
            return;
        }

        loading.textContent = options.labels.timelineLoading;
        loading.classList.remove("hidden");
        eventsContainer.classList.add("hidden");
        eventsContainer.replaceChildren();
        summary.replaceChildren();
        dialog.showModal();

        try {
            const response = await fetch(
                `${options.timelineUrl}/${encodeURIComponent(sessionId)}`,
                {
                    credentials: "same-origin",
                    headers: { Accept: "application/json" }
                });

            if (!response.ok) {
                throw new Error(`Timeline request failed with status ${response.status}.`);
            }

            const timeline = await response.json();
            renderTimelineSummary(summary, timeline);
            renderTimelineEvents(eventsContainer, timeline);
            loading.classList.add("hidden");
            eventsContainer.classList.remove("hidden");
        } catch (error) {
            loading.textContent = error.message || options.labels.timelineEmpty;
            console.error("Session timeline failed.", error);
        }
    }

    function closeTimeline() {
        document.getElementById(options.timelineDialogId)?.close();
    }

    function renderTimelineSummary(container, timeline) {
        const entries = [
            ["SESSION", timeline.sessionId],
            ["DEVICE", timeline.deviceName],
            ["BUILD", timeline.buildVersion],
            ["STATUS", timeline.statusLabel]
        ];

        for (const [label, value] of entries) {
            const cell = document.createElement("div");
            cell.className = "bg-[#030706] px-4 py-3";

            const term = document.createElement("p");
            term.className = "text-[8px] uppercase tracking-[0.16em] text-slate-700";
            term.textContent = label;

            const description = document.createElement("p");
            description.className = "mt-2 truncate text-[10px] text-green-400";
            description.title = value || "-";
            description.textContent = value || "-";

            cell.append(term, description);
            container.appendChild(cell);
        }
    }

    function renderTimelineEvents(container, timeline) {
        const nodes = [];

        nodes.push({
            label: options.labels.connection,
            eventType: "CONNECTION",
            scene: timeline.currentScene || "-",
            gameState: timeline.currentGameState || "-",
            phase: timeline.currentPhase || "-",
            payloadJson: "",
            timeUtc: timeline.startedAtUtc,
            source: options.labels.serverTime
        });

        for (const item of timeline.events || []) {
            nodes.push({
                label: item.eventType || "SESSION_EVENT",
                eventType: item.eventType || "SESSION_EVENT",
                scene: item.scene || "-",
                gameState: item.gameState || "-",
                phase: item.phase || "-",
                payloadJson: formatPayload(item.payloadJson),
                timeUtc: item.clientTimeUtc || item.createdAtUtc,
                source: item.clientTimeUtc
                    ? options.labels.clientTime
                    : options.labels.serverTime
            });
        }

        nodes.push({
            label: timeline.endedAtUtc
                ? options.labels.disconnection
                : options.labels.lastHeartbeat,
            eventType: timeline.endedAtUtc ? "DISCONNECT" : "HEARTBEAT",
            scene: timeline.currentScene || "-",
            gameState: timeline.currentGameState || "-",
            phase: timeline.currentPhase || "-",
            payloadJson: "",
            timeUtc: timeline.endedAtUtc || timeline.lastHeartbeatUtc,
            source: options.labels.serverTime
        });

        nodes.sort((left, right) =>
            new Date(left.timeUtc) - new Date(right.timeUtc));

        if (nodes.length === 0) {
            const empty = document.createElement("div");
            empty.className = "border border-slate-800 bg-black/50 px-5 py-10 text-center text-xs uppercase tracking-[0.16em] text-slate-600";
            empty.textContent = options.labels.timelineEmpty;
            container.appendChild(empty);
            return;
        }

        nodes.forEach((node, index) => {
            const article = document.createElement("article");
            article.className = "relative pl-10 pb-8 last:pb-0";

            if (index < nodes.length - 1) {
                const line = document.createElement("span");
                line.className = "absolute left-[11px] top-5 h-full w-px bg-green-500/30";
                article.appendChild(line);
            }

            const marker = document.createElement("span");
            marker.className = "absolute left-0 top-1.5 h-6 w-6 border border-green-500/40 bg-[#020504] shadow-[0_0_14px_rgba(74,222,128,0.22)]";

            const dot = document.createElement("span");
            dot.className = "absolute left-[7px] top-[7px] h-2 w-2 bg-green-400";
            marker.appendChild(dot);

            const panel = document.createElement("div");
            panel.className = "border border-slate-800 bg-black/45 px-4 py-3";

            const header = document.createElement("div");
            header.className = "flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between";

            const label = document.createElement("p");
            label.className = "text-[10px] font-semibold uppercase tracking-[0.15em] text-green-400";
            label.textContent = node.label;

            const time = document.createElement("p");
            time.className = "text-[9px] uppercase tracking-[0.12em] text-slate-600";
            time.textContent = `${formatUtc(node.timeUtc)} // ${node.source}`;

            header.append(label, time);

            const metadata = document.createElement("p");
            metadata.className = "mt-3 text-[10px] leading-5 text-slate-500";
            metadata.textContent = `SCENE: ${node.scene} // STATE: ${node.gameState} // PHASE: ${node.phase}`;

            panel.append(header, metadata);

            if (node.payloadJson) {
                const pre = document.createElement("pre");
                pre.className = "mt-3 max-h-[220px] overflow-auto border border-slate-800 bg-[#010302] p-3 text-[9px] leading-5 text-green-300";
                const code = document.createElement("code");
                code.className = "json";
                code.textContent = node.payloadJson;
                pre.appendChild(code);
                panel.appendChild(pre);
            }

            article.append(marker, panel);
            container.appendChild(article);
        });
    }

    function setStreamStatus(online, text) {
        const element = document.getElementById(options.streamStatusId);
        if (!element) {
            return;
        }

        element.replaceChildren();

        const dot = document.createElement("span");
        dot.className = online
            ? "h-2 w-2 rounded-full bg-green-400 shadow-[0_0_12px_rgba(74,222,128,0.65)]"
            : "h-2 w-2 rounded-full bg-slate-700";

        const label = document.createTextNode(text);
        element.className = online
            ? "mt-3 inline-flex items-center gap-2 text-[9px] font-semibold uppercase tracking-[0.16em] text-green-400"
            : "mt-3 inline-flex items-center gap-2 text-[9px] font-semibold uppercase tracking-[0.16em] text-slate-500";
        element.append(dot, label);
    }

    function incrementTotalCount(amount) {
        const element = document.getElementById(options.totalCountId);
        if (!element) {
            return;
        }

        const current = Number(
            String(element.textContent || "0").replace(/[^0-9-]/g, ""));
        element.textContent = Number.isFinite(current)
            ? Math.max(0, current + amount)
                .toLocaleString(options.culture)
            : String(Math.max(0, amount));
    }

    function formatPayload(value) {
        if (!value) {
            return "";
        }

        if (typeof value === "object") {
            return prettyJson(value);
        }

        try {
            return prettyJson(JSON.parse(value));
        } catch {
            return String(value);
        }
    }

    function prettyJson(value) {
        return JSON.stringify(value, null, 2);
    }

    function formatUtc(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString(options.culture, {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false,
            timeZone: "UTC"
        }) + " UTC";
    }

    function shortId(value) {
        return String(value || "UNKNOWN").slice(0, 8);
    }

    return { init };
})();
