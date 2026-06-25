window.echoConsoleAlertsCenter = (() => {
    "use strict";

    let options = null;
    let connection = null;
    let refreshTimer = null;
    let reconnectTimer = null;
    let fetchController = null;
    let refreshInFlight = false;
    let refreshPending = false;

    const realtimeThrottleMs = 1200;
    const aiMinimumLatencyMs = 1500;

    function init(config) {
        options = {
            ...config,
            pageNumber: Number(config.pageNumber) || 1,
            pageSize: Number(config.pageSize) || 20,
            severity: config.severity || "",
            status: config.status || "OPEN",
            labels: config.labels || {}
        };

        bindActions();
        buildConnection();
        void startConnection();
    }

    function bindActions() {
        document.addEventListener("click", event => {
            const resolveButton = event.target.closest("[data-alert-resolve]");
            if (resolveButton) {
                void resolveAlert(resolveButton);
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

        document.querySelector("[data-alert-type-form]")?.addEventListener(
            "submit",
            event => {
                event.preventDefault();
                void updateAlertType(event.currentTarget);
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

        connection.on("ReceiveTelemetryUpdate", envelope => {
            const eventType = findEventType(envelope);
            if (
                eventType === "alertCreated" ||
                eventType === "alertUpdated" ||
                eventType === "liveSessionsChanged"
            ) {
                scheduleRefresh();
            }
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
        if (!connection || connection.state !== signalR.HubConnectionState.Disconnected) {
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

    function findEventType(value) {
        return value?.eventType || value?.type || value?.name || value?.eventName || "";
    }

    function scheduleRefresh(immediate = false) {
        refreshPending = true;
        if (refreshInFlight || refreshTimer) {
            return;
        }

        refreshTimer = window.setTimeout(() => {
            refreshTimer = null;
            void refreshPage();
        }, immediate ? 0 : realtimeThrottleMs);
    }

    async function refreshPage() {
        if (refreshInFlight) {
            refreshPending = true;
            return;
        }

        refreshInFlight = true;
        refreshPending = false;
        fetchController?.abort();
        fetchController = new AbortController();

        try {
            const url = new URL(options.pageUrl, window.location.origin);
            url.searchParams.set("status", options.status);
            url.searchParams.set("pageNumber", String(options.pageNumber));
            if (options.severity) {
                url.searchParams.set("severity", options.severity);
            }

            const response = await fetch(url, {
                credentials: "same-origin",
                headers: { Accept: "application/json" },
                signal: fetchController.signal
            });

            if (!response.ok) {
                throw new Error(`Refresh failed with status ${response.status}.`);
            }

            const page = await response.json();
            window.requestAnimationFrame(() => patchTable(page));
        } catch (error) {
            if (error.name !== "AbortError") {
                console.error("Alerts refresh failed.", error);
            }
        } finally {
            refreshInFlight = false;
            if (refreshPending) {
                scheduleRefresh();
            }
        }
    }

    function patchTable(page) {
        const body = document.getElementById(options.tableBodyId);
        if (!body) {
            return;
        }

        const items = Array.isArray(page.items)
            ? page.items.slice(0, options.pageSize)
            : [];

        const existingRows = new Map(
            Array.from(body.querySelectorAll("[data-alert-row]"))
                .map(row => [row.dataset.alertId, row]));

        const desiredRows = [];

        for (const item of items) {
            const id = String(item.id);
            const signature = createSignature(item);
            let row = existingRows.get(id);

            if (!row || row.dataset.signature !== signature) {
                const replacement = createAlertRow(item);
                if (row) {
                    row.replaceWith(replacement);
                }
                row = replacement;
            }

            row.dataset.signature = signature;
            desiredRows.push(row);
            existingRows.delete(id);
        }

        for (const staleRow of existingRows.values()) {
            staleRow.remove();
        }

        body.querySelector("[data-alert-empty]")?.remove();

        if (desiredRows.length === 0) {
            body.appendChild(createEmptyRow());
        } else {
            for (const row of desiredRows) {
                body.appendChild(row);
            }
        }

        updateCount(options.resultsCountId, page.totalCount);
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
        row.className = "border-b border-slate-900/80 transition-colors hover:bg-slate-900/35";

        row.append(
            createCell(formatUtc(item.createdAtUtc), "text-slate-500"),
            createCell(item.source || "-", "text-slate-300"),
            createBadgeCell(item.errorTypeCode || "UNCLASSIFIED"),
            createCell(item.buildVersion || "UNKNOWN_BUILD", "text-cyan-600"),
            createMessageCell(item),
            createSeverityCell(item.severity),
            createCell(item.status || (item.isResolved ? "RESOLVED" : "OPEN"), item.isResolved ? "text-emerald-400 uppercase" : "text-slate-400 uppercase"),
            createActionCell(item));

        return row;
    }

    function createCell(text, colorClass) {
        const cell = document.createElement("td");
        cell.className = `px-4 py-4 align-middle text-xs ${colorClass}`;
        cell.textContent = text;
        return cell;
    }

    function createBadgeCell(text) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 align-middle";
        const badge = document.createElement("span");
        badge.className = "inline-flex min-w-[180px] border border-cyan-900/50 bg-cyan-950/20 px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.12em] text-cyan-400";
        badge.textContent = text;
        cell.appendChild(badge);
        return cell;
    }

    function createMessageCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 align-middle";
        const message = document.createElement("p");
        message.className = "max-w-xl text-xs leading-5 text-slate-300";
        message.textContent = item.message || "-";
        const node = document.createElement("p");
        node.className = "mt-1 text-[9px] uppercase tracking-[0.14em] text-slate-700";
        node.textContent = `NODE: ${item.installationId || "SYSTEM"}`;
        cell.append(message, node);
        return cell;
    }

    function createSeverityCell(severity) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 align-middle text-xs uppercase text-slate-300";
        cell.textContent = options.labels.severities?.[severity] || severity || "INFO";
        return cell;
    }

    function createActionCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-4 py-4 text-right align-middle";

        if (item.isResolved) {
            const status = document.createElement("span");
            status.className = "inline-flex h-8 w-24 min-w-[96px] items-center justify-center whitespace-nowrap text-[9px] font-semibold uppercase tracking-[0.14em] text-emerald-500";
            status.textContent = options.labels.resolved;
            cell.appendChild(status);
            return cell;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.dataset.alertResolve = "true";
        button.dataset.alertId = String(item.id);
        button.className = "inline-flex h-8 w-24 min-w-[96px] items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-2 text-[9px] font-semibold uppercase tracking-[0.14em] text-slate-300 transition-colors hover:bg-slate-800/90 hover:text-green-400";
        button.textContent = `[ ${options.labels.resolve} ]`;
        cell.appendChild(button);
        return cell;
    }

    function createEmptyRow() {
        const row = document.createElement("tr");
        row.dataset.alertEmpty = "true";
        const cell = document.createElement("td");
        cell.colSpan = 8;
        cell.className = "px-5 py-12 text-center text-xs uppercase tracking-[0.18em] text-slate-600";
        cell.textContent = options.labels.empty;
        row.appendChild(cell);
        return row;
    }

    async function resolveAlert(button) {
        const alertId = button.dataset.alertId;
        if (!alertId || button.disabled) {
            return;
        }

        button.disabled = true;
        button.setAttribute("aria-busy", "true");

        try {
            const response = await postForm(options.resolveUrl, { id: alertId });
            if (!response.ok) {
                throw new Error(`Resolve failed with status ${response.status}.`);
            }

            if (options.status === "OPEN") {
                const row = button.closest("[data-alert-row]");
                row?.classList.add("opacity-0");
                window.setTimeout(() => row?.remove(), 160);
            }

            scheduleRefresh(true);
        } catch (error) {
            console.error("Alert resolve failed.", error);
            button.disabled = false;
            button.removeAttribute("aria-busy");
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
                throw new Error(payload?.message || options.labels.requestFailed);
            }
            output.textContent = payload.narrative;
        } catch (error) {
            output.textContent = error.message || options.labels.requestFailed;
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

    async function updateAlertType(form) {
        const id = document.querySelector("[data-type-id]")?.value;
        if (!id) {
            return;
        }

        const response = await postForm(options.updateTypeUrl, {
            id,
            name: document.querySelector("[data-type-name]")?.value || "",
            description: document.querySelector("[data-type-description]")?.value || "",
            defaultSeverity: document.querySelector("[data-type-severity]")?.value || "Warning",
            isActive: document.querySelector("[data-type-active]")?.checked ? "true" : "false"
        });

        const feedback = document.querySelector("[data-type-feedback]");
        if (!response.ok) {
            if (feedback) {
                feedback.textContent = options.labels.requestFailed;
                feedback.className = "mt-3 min-h-5 text-[10px] text-red-400";
            }
            return;
        }

        if (feedback) {
            feedback.textContent = options.labels.typeUpdated;
            feedback.className = "mt-3 min-h-5 text-[10px] text-green-400";
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
        formData.set("__RequestVerificationToken", options.antiForgeryToken);
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

    function setValue(selector, value) {
        const element = document.querySelector(selector);
        if (element) {
            element.value = value || "";
        }
    }

    function updateCount(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = Number(value || 0).toLocaleString(options.culture);
        }
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
        return new Promise(resolve => window.setTimeout(resolve, milliseconds));
    }

    return { init };
})();
