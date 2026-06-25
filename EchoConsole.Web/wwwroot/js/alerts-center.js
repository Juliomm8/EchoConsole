window.echoConsoleAlertsCenter = (() => {
    "use strict";

    let options = null;
    let connection = null;
    let refreshTimer = null;
    let reconnectTimer = null;
    let fetchController = null;
    let requestVersion = 0;

    const resolveOperations = new Map();
    const realtimeThrottleMs = 120;
    const aiMinimumLatencyMs = 1500;
    const resolveDelaySeconds = 5;

    function init(config) {
        options = {
            ...config,
            pageNumber: Number(config.pageNumber) || 1,
            pageSize: Number(config.pageSize) || 20,
            severity: config.severity || "",
            status: normalizeStatus(config.status),
            labels: config.labels || {}
        };

        bindActions();
        updateStatusUi();
        buildConnection();
        void startConnection();
    }

    function bindActions() {
        document.addEventListener("click", event => {
            const tab = event.target.closest("[data-alert-tab]");
            if (tab) {
                event.preventDefault();
                void switchStatus(tab.dataset.status);
                return;
            }

            const pageButton = event.target.closest("[data-alert-page]");
            if (pageButton) {
                event.preventDefault();
                if (!pageButton.disabled) {
                    const delta = Number(pageButton.dataset.pageDelta) || 0;
                    void switchPage(options.pageNumber + delta);
                }
                return;
            }

            const resolveButton = event.target.closest("[data-alert-resolve]");
            if (resolveButton) {
                startResolveCountdown(resolveButton);
                return;
            }

            const undoButton = event.target.closest("[data-alert-resolve-undo]");
            if (undoButton) {
                cancelResolveCountdown(undoButton.dataset.alertId, true);
                return;
            }

            if (event.target.closest("[data-alert-ai-run]")) {
                void runAiAnalysis();
                return;
            }

            if (event.target.closest("[data-alert-types-open]")) {
                document.querySelector("[data-alert-types-dialog]")?.showModal();
                return;
            }

            if (event.target.closest("[data-alert-types-close]")) {
                document.querySelector("[data-alert-types-dialog]")?.close();
                return;
            }

            const editButton = event.target.closest("[data-alert-type-edit]");
            if (editButton) {
                populateTypeForm(editButton);
                return;
            }

            const deleteButton = event.target.closest("[data-alert-type-delete]");
            if (deleteButton) {
                void deleteAlertType(deleteButton);
            }
        });

        document.querySelector("[data-alert-filter-form]")?.addEventListener(
            "submit",
            event => {
                event.preventDefault();
                const select = event.currentTarget.querySelector(
                    "[data-alert-severity-filter]");
                options.severity = select?.value || "";
                options.pageNumber = 1;
                updateBrowserUrl(true);
                void refreshPage(true);
            });

        document.querySelector("[data-alert-type-form]")?.addEventListener(
            "submit",
            event => {
                event.preventDefault();
                void updateAlertType(event.currentTarget);
            });

        window.addEventListener("popstate", () => {
            const url = new URL(window.location.href);
            options.status = normalizeStatus(url.searchParams.get("status"));
            options.severity = url.searchParams.get("severity") || "";
            options.pageNumber = Math.max(
                1,
                Number(url.searchParams.get("pageNumber")) || 1);

            const select = document.querySelector(
                "[data-alert-severity-filter]");
            if (select) {
                select.value = options.severity;
            }

            updateStatusUi();
            void refreshPage(true);
        });
    }

    function buildConnection() {
        if (!window.signalR || !options.hubUrl) {
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl, { withCredentials: true })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("ReceiveTelemetryUpdate", handleRealtimeEnvelope);
        connection.on("alertCreated", payload => {
            handleRealtimeEvent("alertCreated", payload);
        });
        connection.on("alertUpdated", payload => {
            handleRealtimeEvent("alertUpdated", payload);
        });

        connection.onreconnected(() => scheduleRefresh(true));
        connection.onclose(() => {
            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(
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
            await connection.start();
        } catch (error) {
            console.error("Alerts SignalR connection failed.", error);
            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(
                () => void startConnection(),
                5000);
        }
    }

    function handleRealtimeEnvelope(envelope) {
        const realtimeEvent = unwrapRealtimeEvent(envelope);
        if (!realtimeEvent) {
            return;
        }

        handleRealtimeEvent(
            realtimeEvent.eventType,
            realtimeEvent.payload);
    }

    function unwrapRealtimeEvent(value, depth = 0) {
        if (!value || typeof value !== "object" || depth > 6) {
            return null;
        }

        const eventType = firstString(
            value.eventType,
            value.type,
            value.eventName,
            value.name);

        if (eventType) {
            return {
                eventType,
                payload: value.payload ?? value.data ?? value.value ?? value
            };
        }

        for (const key of ["payload", "data", "value", "message"]) {
            const nested = unwrapRealtimeEvent(value[key], depth + 1);
            if (nested) {
                return nested;
            }
        }

        return null;
    }

    function handleRealtimeEvent(eventType, payload) {
        const normalizedType = String(eventType || "").toLowerCase();
        if (normalizedType !== "alertcreated" &&
            normalizedType !== "alertupdated") {
            return;
        }

        const normalizedAlert = normalizeAlertPayload(payload);
        if (normalizedAlert) {
            applyRealtimeAlert(normalizedAlert);
        }

        scheduleRefresh(true);
    }

    function normalizeAlertPayload(payload) {
        if (!payload || typeof payload !== "object") {
            return null;
        }

        const id = payload.id ?? payload.alertId ?? payload.Id ?? payload.AlertId;
        if (!id) {
            return null;
        }

        const isResolved = Boolean(
            payload.isResolved ?? payload.IsResolved ?? false);

        return {
            id,
            severity: payload.severity ?? payload.Severity ?? "Info",
            status: payload.status ?? payload.Status ??
                (isResolved ? "RESOLVED" : "OPEN"),
            errorTypeCode: payload.errorTypeCode ??
                payload.ErrorTypeCode ?? "UNCLASSIFIED",
            buildVersion: payload.buildVersion ??
                payload.BuildVersion ?? "UNKNOWN_BUILD",
            message: payload.message ?? payload.Message ?? "-",
            source: payload.source ?? payload.Source ?? "SYSTEM",
            installationId: payload.installationId ??
                payload.InstallationId ?? "SYSTEM",
            createdAtUtc: payload.createdAtUtc ??
                payload.CreatedAtUtc ?? new Date().toISOString(),
            isResolved,
            resolvedAtUtc: payload.resolvedAtUtc ??
                payload.ResolvedAtUtc ?? null
        };
    }

    function applyRealtimeAlert(item) {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        const itemStatus = item.isResolved ? "RESOLVED" : "OPEN";
        const existing = body.querySelector(
            `[data-alert-row][data-alert-id="${cssEscape(String(item.id))}"]`);

        if (itemStatus !== options.status) {
            if (existing) {
                cancelResolveCountdown(String(item.id), false);
                existing.remove();
                updateVisibleCount(-1);
            }
            ensureEmptyRow(body);
            return;
        }

        const row = createAlertRow(item);
        row.dataset.signature = createSignature(item);

        body.querySelector("[data-alert-empty]")?.remove();

        if (existing) {
            if (existing.dataset.resolvePending === "true") {
                return;
            }
            existing.replaceWith(row);
        } else {
            body.insertBefore(row, body.firstChild);
            updateVisibleCount(1);
        }

        pruneRows(body);
    }

    function scheduleRefresh(immediate = false) {
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(
            () => {
                refreshTimer = null;
                void refreshPage(false);
            },
            immediate ? 0 : realtimeThrottleMs);
    }

    async function switchStatus(status) {
        const normalizedStatus = normalizeStatus(status);
        if (normalizedStatus === options.status && options.pageNumber === 1) {
            return;
        }

        cancelAllResolveCountdowns();
        options.status = normalizedStatus;
        options.pageNumber = 1;
        updateStatusUi();
        updateBrowserUrl(true);
        await refreshPage(true);
    }

    async function switchPage(pageNumber) {
        const normalizedPage = Math.max(1, Number(pageNumber) || 1);
        if (normalizedPage === options.pageNumber) {
            return;
        }

        cancelAllResolveCountdowns();
        options.pageNumber = normalizedPage;
        updateBrowserUrl(true);
        await refreshPage(true);
    }

    async function refreshPage(replaceContents) {
        const currentVersion = ++requestVersion;
        const requestedStatus = options.status;
        const requestedSeverity = options.severity;
        const requestedPage = options.pageNumber;

        fetchController?.abort();
        fetchController = new AbortController();

        if (replaceContents) {
            showLoadingRow();
        }

        try {
            const url = new URL(options.pageUrl, window.location.origin);
            url.searchParams.set("status", requestedStatus);
            url.searchParams.set("pageNumber", String(requestedPage));
            if (requestedSeverity) {
                url.searchParams.set("severity", requestedSeverity);
            }

            const response = await fetch(url, {
                credentials: "same-origin",
                headers: { Accept: "application/json" },
                signal: fetchController.signal
            });

            if (!response.ok) {
                throw new Error(
                    `Refresh failed with status ${response.status}.`);
            }

            const page = await response.json();

            if (currentVersion !== requestVersion ||
                requestedStatus !== options.status ||
                requestedSeverity !== options.severity ||
                requestedPage !== options.pageNumber) {
                return;
            }

            window.requestAnimationFrame(
                () => patchTable(page, replaceContents));
        } catch (error) {
            if (error.name !== "AbortError") {
                console.error("Alerts refresh failed.", error);
                showRequestFailureRow();
            }
        }
    }

    function patchTable(page, replaceContents) {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        const items = Array.isArray(page.items)
            ? page.items.slice(0, options.pageSize)
            : [];

        if (replaceContents) {
            cancelAllResolveCountdowns();
            body.replaceChildren();
        }

        const existingRows = new Map(
            Array.from(body.querySelectorAll("[data-alert-row]"))
                .map(row => [row.dataset.alertId, row]));

        const desiredRows = [];

        for (const item of items) {
            const id = String(item.id);
            const signature = createSignature(item);
            let row = existingRows.get(id);

            if (row?.dataset.resolvePending === "true") {
                desiredRows.push(row);
                existingRows.delete(id);
                continue;
            }

            if (!row || row.dataset.signature !== signature) {
                const replacement = createAlertRow(item);
                replacement.dataset.signature = signature;

                if (row) {
                    row.replaceWith(replacement);
                }

                row = replacement;
            }

            desiredRows.push(row);
            existingRows.delete(id);
        }

        for (const staleRow of existingRows.values()) {
            cancelResolveCountdown(staleRow.dataset.alertId, false);
            staleRow.remove();
        }

        body.querySelector("[data-alert-empty]")?.remove();
        body.querySelector("[data-alert-loading]")?.remove();
        body.querySelector("[data-alert-request-failed]")?.remove();

        if (desiredRows.length === 0) {
            body.appendChild(createEmptyRow());
        } else {
            for (const row of desiredRows) {
                body.appendChild(row);
            }
        }

        updateCount(options.resultsCountId, page.totalCount);
        updatePagination(page);
    }

    function createSignature(item) {
        return [
            item.id,
            item.severity,
            item.status,
            item.errorTypeCode,
            item.buildVersion,
            item.message,
            item.source,
            item.installationId,
            item.createdAtUtc,
            item.isResolved,
            item.resolvedAtUtc
        ].join("|");
    }

    function createAlertRow(item) {
        const row = document.createElement("tr");
        row.dataset.alertRow = "true";
        row.dataset.alertId = String(item.id);
        row.className =
            "border-b border-slate-900/80 transition-all duration-150 hover:bg-slate-900/35";

        row.append(
            createCell(formatUtc(item.createdAtUtc), "text-slate-500"),
            createCell(item.source || "-", "text-slate-300"),
            createBadgeCell(item.errorTypeCode || "UNCLASSIFIED"),
            createCell(item.buildVersion || "UNKNOWN_BUILD", "text-cyan-600"),
            createMessageCell(item),
            createSeverityCell(item.severity),
            createCell(
                item.status || (item.isResolved ? "RESOLVED" : "OPEN"),
                item.isResolved
                    ? "text-emerald-400 uppercase"
                    : "text-slate-400 uppercase"),
            createActionCell(item));

        return row;
    }

    function createCell(text, colorClass) {
        const cell = document.createElement("td");
        cell.className =
            `px-4 py-4 align-middle text-xs ${colorClass}`;
        cell.textContent = text;
        return cell;
    }

    function createBadgeCell(text) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 align-middle";

        const badge = document.createElement("span");
        badge.className =
            "inline-flex min-w-[180px] border border-cyan-900/50 bg-cyan-950/20 px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.12em] text-cyan-400";
        badge.textContent = text;

        cell.appendChild(badge);
        return cell;
    }

    function createMessageCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 align-middle";

        const message = document.createElement("p");
        message.className =
            "max-w-xl text-xs leading-5 text-slate-300";
        message.textContent = item.message || "-";

        const node = document.createElement("p");
        node.className =
            "mt-1 text-[9px] uppercase tracking-[0.14em] text-slate-700";
        node.textContent = `NODE: ${item.installationId || "SYSTEM"}`;

        cell.append(message, node);
        return cell;
    }

    function createSeverityCell(severity) {
        const cell = document.createElement("td");
        cell.className =
            "px-4 py-4 align-middle text-xs uppercase text-slate-300";
        cell.textContent =
            options.labels.severities?.[severity] || severity || "INFO";
        return cell;
    }

    function createActionCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 text-right align-middle";

        if (item.isResolved) {
            const status = document.createElement("span");
            status.className =
                "inline-flex h-8 w-40 min-w-[160px] items-center justify-center whitespace-nowrap text-[9px] font-semibold uppercase tracking-[0.14em] text-emerald-500";
            status.textContent = options.labels.resolved;
            cell.appendChild(status);
            return cell;
        }

        const group = document.createElement("div");
        group.dataset.alertActionGroup = "true";
        group.className =
            "inline-flex min-w-[280px] items-center justify-end gap-2 whitespace-nowrap";

        const resolveButton = document.createElement("button");
        resolveButton.type = "button";
        resolveButton.dataset.alertResolve = "true";
        resolveButton.dataset.alertId = String(item.id);
        resolveButton.className =
            "inline-flex h-8 w-40 min-w-[160px] shrink-0 items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-2 text-[9px] font-semibold uppercase tracking-[0.12em] text-slate-300 transition-colors hover:bg-slate-800/90 hover:text-green-400 disabled:cursor-wait disabled:opacity-80";
        resolveButton.textContent = `[ ${options.labels.resolve} ]`;

        const undoButton = document.createElement("button");
        undoButton.type = "button";
        undoButton.dataset.alertResolveUndo = "true";
        undoButton.dataset.alertId = String(item.id);
        undoButton.className =
            "pointer-events-none invisible inline-flex h-8 w-28 min-w-[112px] shrink-0 items-center justify-center whitespace-nowrap border border-amber-900/60 bg-amber-950/20 px-2 text-[9px] font-semibold uppercase tracking-[0.10em] text-amber-400 transition-colors hover:bg-amber-900/30 hover:text-amber-200";
        undoButton.textContent = `[ ${options.labels.cancelUndo} ]`;

        group.append(resolveButton, undoButton);
        cell.appendChild(group);
        return cell;
    }

    function createEmptyRow() {
        const row = document.createElement("tr");
        row.dataset.alertEmpty = "true";

        const cell = document.createElement("td");
        cell.colSpan = 8;
        cell.className =
            "px-5 py-12 text-center text-xs uppercase tracking-[0.18em] text-slate-600";
        cell.textContent = options.labels.empty;

        row.appendChild(cell);
        return row;
    }

    function createStateRow(attributeName, text, colorClass) {
        const row = document.createElement("tr");
        row.dataset[attributeName] = "true";

        const cell = document.createElement("td");
        cell.colSpan = 8;
        cell.className =
            `px-5 py-12 text-center text-xs uppercase tracking-[0.18em] ${colorClass}`;
        cell.textContent = text;

        row.appendChild(cell);
        return row;
    }

    function showLoadingRow() {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        cancelAllResolveCountdowns();
        body.replaceChildren(
            createStateRow(
                "alertLoading",
                options.labels.loading,
                "text-green-500"));
    }

    function showRequestFailureRow() {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        body.replaceChildren(
            createStateRow(
                "alertRequestFailed",
                options.labels.requestFailed,
                "text-red-400"));
    }

    function startResolveCountdown(button) {
        const alertId = button.dataset.alertId;
        if (!alertId || resolveOperations.has(alertId)) {
            return;
        }

        const row = button.closest("[data-alert-row]");
        const group = button.closest("[data-alert-action-group]");
        const undoButton = group?.querySelector(
            "[data-alert-resolve-undo]");

        if (!row || !undoButton) {
            return;
        }

        const operation = {
            alertId,
            row,
            button,
            undoButton,
            originalLabel: button.textContent,
            deadline: Date.now() + resolveDelaySeconds * 1000,
            intervalId: null,
            timeoutId: null,
            lastRemaining: null,
            committing: false
        };

        resolveOperations.set(alertId, operation);
        row.dataset.resolvePending = "true";
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        button.classList.add(
            "border-green-500/30",
            "bg-green-500/10",
            "text-green-400");

        undoButton.classList.remove(
            "invisible",
            "pointer-events-none");

        renderResolveCountdown(operation);

        operation.intervalId = window.setInterval(
            () => renderResolveCountdown(operation),
            200);

        operation.timeoutId = window.setTimeout(
            () => void commitResolve(operation),
            resolveDelaySeconds * 1000);
    }

    function renderResolveCountdown(operation) {
        const milliseconds = Math.max(
            0,
            operation.deadline - Date.now());
        const remaining = Math.max(
            1,
            Math.ceil(milliseconds / 1000));

        if (remaining === operation.lastRemaining) {
            return;
        }

        operation.lastRemaining = remaining;
        operation.button.textContent = formatCountdownLabel(remaining);
    }

    function formatCountdownLabel(remaining) {
        const template = options.labels.resolveCountdown ||
            "Resolving in {0}s...";
        return template.replace("{0}", String(remaining));
    }

    function cancelResolveCountdown(alertId, restoreUi) {
        const operation = resolveOperations.get(String(alertId || ""));
        if (!operation || operation.committing) {
            return;
        }

        window.clearInterval(operation.intervalId);
        window.clearTimeout(operation.timeoutId);
        resolveOperations.delete(operation.alertId);

        if (!restoreUi) {
            return;
        }

        restoreResolveControls(operation);
    }

    function cancelAllResolveCountdowns() {
        for (const operation of Array.from(resolveOperations.values())) {
            if (operation.committing) {
                continue;
            }

            window.clearInterval(operation.intervalId);
            window.clearTimeout(operation.timeoutId);
            resolveOperations.delete(operation.alertId);
            restoreResolveControls(operation);
        }
    }

    function restoreResolveControls(operation) {
        operation.row.removeAttribute("data-resolve-pending");
        operation.button.disabled = false;
        operation.button.removeAttribute("aria-busy");
        operation.button.textContent = operation.originalLabel;
        operation.button.classList.remove(
            "border-green-500/30",
            "bg-green-500/10",
            "text-green-400");

        operation.undoButton.disabled = false;
        operation.undoButton.classList.add(
            "invisible",
            "pointer-events-none");
    }

    async function commitResolve(operation) {
        if (!resolveOperations.has(operation.alertId)) {
            return;
        }

        operation.committing = true;
        window.clearInterval(operation.intervalId);
        window.clearTimeout(operation.timeoutId);
        operation.button.textContent = options.labels.resolving;
        operation.undoButton.disabled = true;
        operation.undoButton.classList.add(
            "invisible",
            "pointer-events-none");

        try {
            const response = await postForm(
                options.resolveUrl,
                { id: operation.alertId });

            if (!response.ok) {
                throw new Error(
                    `Resolve failed with status ${response.status}.`);
            }

            resolveOperations.delete(operation.alertId);
            operation.row.removeAttribute("data-resolve-pending");

            if (options.status === "OPEN") {
                operation.row.classList.add(
                    "opacity-0",
                    "-translate-y-1");
                window.setTimeout(
                    () => {
                        operation.row.remove();
                        ensureEmptyRow(
                            document.getElementById(options.tableBodyId));
                    },
                    160);
                updateVisibleCount(-1);
            }

            scheduleRefresh(true);
        } catch (error) {
            console.error("Alert resolve failed.", error);
            operation.committing = false;
            restoreResolveControls(operation);
            resolveOperations.delete(operation.alertId);
        }
    }

    async function runAiAnalysis() {
        const button = document.querySelector("[data-alert-ai-run]");
        const output = document.querySelector("[data-alert-ai-output]");
        const spinner = document.querySelector("[data-alert-ai-spinner]");

        if (!button || !output || button.disabled) {
            return;
        }

        button.disabled = true;
        spinner?.classList.remove("hidden");
        output.classList.remove("text-red-400");
        output.textContent = options.labels.aiProcessing;
        const startedAt = performance.now();

        try {
            const response = await postForm(options.aiUrl);
            const elapsed = performance.now() - startedAt;

            if (elapsed < aiMinimumLatencyMs) {
                await delay(aiMinimumLatencyMs - elapsed);
            }

            const payload = await response.json();
            if (!response.ok) {
                throw new Error(
                    payload?.message || options.labels.requestFailed);
            }

            output.textContent = payload.narrative;
        } catch (error) {
            output.textContent =
                error.message || options.labels.requestFailed;
            output.classList.add("text-red-400");
        } finally {
            spinner?.classList.add("hidden");
            button.disabled = false;
        }
    }

    function populateTypeForm(button) {
        setValue("[data-type-id]", button.dataset.id);
        setValue("[data-type-code]", button.dataset.code);
        setValue("[data-type-name]", button.dataset.name);
        setValue("[data-type-description]", button.dataset.description);
        setValue("[data-type-severity]", button.dataset.severity);

        const active = document.querySelector("[data-type-active]");
        if (active) {
            active.checked = button.dataset.active === "true";
        }
    }

    async function updateAlertType() {
        const id = document.querySelector("[data-type-id]")?.value;
        if (!id) {
            return;
        }

        const response = await postForm(options.updateTypeUrl, {
            id,
            name: document.querySelector("[data-type-name]")?.value || "",
            description:
                document.querySelector("[data-type-description]")?.value || "",
            defaultSeverity:
                document.querySelector("[data-type-severity]")?.value || "Warning",
            isActive: document.querySelector("[data-type-active]")?.checked
                ? "true"
                : "false"
        });

        const feedback = document.querySelector("[data-type-feedback]");
        if (!response.ok) {
            if (feedback) {
                feedback.textContent = options.labels.requestFailed;
                feedback.className =
                    "mt-3 min-h-5 text-[10px] text-red-400";
            }
            return;
        }

        if (feedback) {
            feedback.textContent = options.labels.typeUpdated;
            feedback.className =
                "mt-3 min-h-5 text-[10px] text-green-400";
        }

        window.setTimeout(() => window.location.reload(), 450);
    }

    async function deleteAlertType(button) {
        if (!window.confirm(options.labels.deleteTypeConfirm)) {
            return;
        }

        const response = await postForm(options.deleteTypeUrl, {
            id: button.dataset.id
        });

        if (!response.ok) {
            window.alert(options.labels.requestFailed);
            return;
        }

        button.closest("[data-alert-type-row]")?.remove();
    }

    async function postForm(url, values = {}) {
        const formData = new FormData();
        formData.set(
            "__RequestVerificationToken",
            options.antiForgeryToken);

        for (const [key, value] of Object.entries(values)) {
            formData.set(key, value);
        }

        return await fetch(url, {
            method: "POST",
            body: formData,
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        });
    }

    function updateStatusUi() {
        for (const tab of document.querySelectorAll("[data-alert-tab]")) {
            const isActive =
                normalizeStatus(tab.dataset.status) === options.status;

            tab.setAttribute("aria-pressed", String(isActive));
            tab.classList.toggle("border-green-500/35", isActive);
            tab.classList.toggle("bg-green-500/10", isActive);
            tab.classList.toggle("text-green-400", isActive);
            tab.classList.toggle("border-slate-800", !isActive);
            tab.classList.toggle("bg-slate-950", !isActive);
            tab.classList.toggle("text-slate-500", !isActive);
        }

        const title = document.getElementById(options.statusTitleId);
        if (title) {
            title.textContent = options.status === "OPEN"
                ? options.labels.tabOpen
                : options.labels.tabResolved;
        }

        const hiddenStatus = document.querySelector(
            "[data-alert-filter-status]");
        if (hiddenStatus) {
            hiddenStatus.value = options.status;
        }
    }

    function updatePagination(page) {
        options.pageNumber = Math.max(
            1,
            Number(page.pageNumber) || options.pageNumber);

        updateCount(options.pageCurrentId, options.pageNumber);
        updateCount(options.pageTotalId, Math.max(1, page.totalPages || 1));

        const previous = document.querySelector(
            '[data-alert-page][data-page-delta="-1"]');
        const next = document.querySelector(
            '[data-alert-page][data-page-delta="1"]');

        if (previous) {
            previous.disabled = options.pageNumber <= 1;
        }

        if (next) {
            next.disabled = options.pageNumber >= Math.max(
                1,
                Number(page.totalPages) || 1);
        }
    }

    function updateBrowserUrl(pushState) {
        const url = new URL(window.location.href);
        url.searchParams.set("status", options.status);
        url.searchParams.set("pageNumber", String(options.pageNumber));

        if (options.severity) {
            url.searchParams.set("severity", options.severity);
        } else {
            url.searchParams.delete("severity");
        }

        const method = pushState ? "pushState" : "replaceState";
        window.history[method]({}, "", url);
    }

    function updateVisibleCount(delta) {
        const element = document.getElementById(options.resultsCountId);
        if (!element) {
            return;
        }

        const current = Number(
            String(element.textContent || "0").replace(/[^0-9-]/g, "")) || 0;
        element.textContent = Math.max(0, current + delta)
            .toLocaleString(options.culture);
    }

    function ensureEmptyRow(body) {
        if (!body || body.querySelector("[data-alert-row]")) {
            return;
        }

        if (!body.querySelector("[data-alert-empty]")) {
            body.appendChild(createEmptyRow());
        }
    }

    function pruneRows(body) {
        const rows = Array.from(
            body.querySelectorAll("[data-alert-row]"));

        for (const row of rows.slice(options.pageSize)) {
            cancelResolveCountdown(row.dataset.alertId, false);
            row.remove();
        }
    }

    function setValue(selector, value) {
        const element = document.querySelector(selector);
        if (element) {
            element.value = value || "";
        }
    }

    function updateCount(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = Number(value || 0)
                .toLocaleString(options.culture);
        }
    }

    function normalizeStatus(status) {
        return String(status || "OPEN").toUpperCase() === "RESOLVED"
            ? "RESOLVED"
            : "OPEN";
    }

    function firstString(...values) {
        return values.find(value =>
            typeof value === "string" && value.length > 0) || "";
    }

    function cssEscape(value) {
        if (window.CSS?.escape) {
            return window.CSS.escape(value);
        }

        return value.replace(/["\\]/g, "\\$&");
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
            hour12: false,
            timeZone: "UTC"
        }) + " UTC";
    }

    function delay(milliseconds) {
        return new Promise(resolve =>
            window.setTimeout(resolve, milliseconds));
    }

    return { init };
})();
