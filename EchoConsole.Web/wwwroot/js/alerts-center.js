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
            hubUrl: config.hubUrl,
            pageUrl: config.pageUrl,
            resolveUrl: config.resolveUrl,
            aiUrl: config.aiUrl,
            discordUrl: config.discordUrl,
            tableBodyId: config.tableBodyId,
            totalCountId: config.totalCountId,
            resultsCountId: config.resultsCountId,
            pageNumber: Number(config.pageNumber) || 1,
            pageSize: Number(config.pageSize) || 20,
            severity: config.severity || "",
            isResolved: config.isResolved ?? "",
            antiForgeryToken: config.antiForgeryToken || "",
            culture: config.culture || "en",
            labels: config.labels || {}
        };

        bindActions();
        buildConnection();
        void startConnection();
    }

    function bindActions() {
        document.addEventListener(
            "click",
            event => {
                const resolveButton = event.target.closest(
                    "[data-alert-resolve]");

                if (resolveButton) {
                    void resolveAlert(resolveButton);
                    return;
                }

                if (event.target.closest("[data-alert-ai-run]")) {
                    void runAiAnalysis();
                    return;
                }

                if (event.target.closest("[data-alert-discord]")) {
                    void broadcastDiscord();
                }
            });
    }

    function buildConnection() {
        if (!window.signalR || !options.hubUrl) {
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl, {
                withCredentials: true
            })
            .withAutomaticReconnect(
                [0, 2000, 5000, 10000, 15000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on(
            "ReceiveTelemetryUpdate",
            envelope => {
                const eventType = findEventType(envelope);

                if (
                    eventType === "alertCreated"
                    || eventType === "alertUpdated"
                    || eventType === "liveSessionsChanged"
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
        if (
            !connection
            || connection.state
                !== signalR.HubConnectionState.Disconnected
        ) {
            return;
        }

        try {
            await connection.start();
        } catch (error) {
            console.error(
                "Alerts SignalR connection failed.",
                error);

            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(
                () => void startConnection(),
                5000);
        }
    }

    function findEventType(value) {
        if (!value || typeof value !== "object") {
            return "";
        }

        if (typeof value.eventType === "string") {
            return value.eventType;
        }

        return findEventType(value.payload);
    }

    function scheduleRefresh(immediate = false) {
        refreshPending = true;

        if (refreshInFlight) {
            return;
        }

        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(
            () => void refreshPage(),
            immediate ? 0 : realtimeThrottleMs);
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
            const url = new URL(
                options.pageUrl,
                window.location.origin);

            url.searchParams.set(
                "pageNumber",
                String(options.pageNumber));

            if (options.severity) {
                url.searchParams.set(
                    "severity",
                    options.severity);
            }

            if (options.isResolved !== "") {
                url.searchParams.set(
                    "isResolved",
                    String(options.isResolved));
            }

            const response = await fetch(url, {
                credentials: "same-origin",
                headers: {
                    Accept: "application/json"
                },
                signal: fetchController.signal
            });

            if (!response.ok) {
                throw new Error(
                    `Alerts refresh failed with status ${response.status}.`);
            }

            const page = await response.json();

            window.requestAnimationFrame(() => {
                patchTable(page);
                updateCount(options.totalCountId, page.totalCount);
                updateCount(options.resultsCountId, page.totalCount);
            });
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
        const body = document.getElementById(
            options.tableBodyId);

        if (!body) {
            return;
        }

        const items = Array.isArray(page.items)
            ? page.items.slice(0, options.pageSize)
            : [];

        const existingRows = new Map(
            Array.from(
                body.querySelectorAll("[data-alert-row]"))
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
            return;
        }

        for (const row of desiredRows) {
            body.appendChild(row);
        }
    }

    function createSignature(item) {
        return [
            item.id,
            item.severity,
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
            "border-b border-slate-900/80 transition-colors hover:bg-slate-900/35";

        row.append(
            createCell(formatUtc(item.createdAtUtc), "text-slate-500"),
            createCell(item.source || "-", "text-slate-300"),
            createMessageCell(item),
            createSeverityCell(item.severity),
            createStatusCell(item.isResolved),
            createActionCell(item));

        return row;
    }

    function createCell(text, colorClass) {
        const cell = document.createElement("td");
        cell.className = `px-5 py-4 align-middle text-xs ${colorClass}`;
        cell.textContent = text;
        return cell;
    }

    function createMessageCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-5 py-4 align-middle";

        const message = document.createElement("p");
        message.className =
            "max-w-xl text-xs leading-5 text-slate-300";
        message.textContent = item.message || "-";

        const installation = document.createElement("p");
        installation.className =
            "mt-1 text-[9px] uppercase tracking-[0.14em] text-slate-700";
        installation.textContent =
            item.installationId
                ? `NODE: ${item.installationId}`
                : "NODE: SYSTEM";

        cell.append(message, installation);
        return cell;
    }

    function createSeverityCell(severity) {
        const cell = document.createElement("td");
        cell.className = "px-5 py-4 align-middle";

        const badge = document.createElement("span");
        badge.className = severityClasses(severity);
        badge.textContent = severityLabel(severity);

        cell.appendChild(badge);
        return cell;
    }

    function createStatusCell(isResolved) {
        const cell = document.createElement("td");
        cell.className = "px-5 py-4 align-middle";

        const badge = document.createElement("span");
        badge.className = isResolved
            ? "inline-flex min-w-[88px] items-center justify-center whitespace-nowrap border border-emerald-500/25 bg-emerald-500/10 px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.14em] text-emerald-400"
            : "inline-flex min-w-[88px] items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.14em] text-slate-400";

        badge.textContent = isResolved
            ? options.labels.resolved
            : options.labels.open;

        cell.appendChild(badge);
        return cell;
    }

    function createActionCell(item) {
        const cell = document.createElement("td");
        cell.className = "px-5 py-4 text-right align-middle";

        if (item.isResolved) {
            const text = document.createElement("span");
            text.className =
                "inline-flex h-8 w-24 min-w-[96px] items-center justify-center whitespace-nowrap text-[9px] font-semibold uppercase tracking-[0.14em] text-emerald-500";
            text.textContent = options.labels.resolved;
            cell.appendChild(text);
            return cell;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.dataset.alertResolve = "true";
        button.dataset.alertId = String(item.id);
        button.className =
            "inline-flex h-8 w-24 min-w-[96px] shrink-0 items-center justify-center whitespace-nowrap border border-slate-700 bg-slate-900/70 px-2 text-[9px] font-semibold uppercase tracking-[0.14em] text-slate-300 transition-colors duration-150 hover:bg-slate-800/90 hover:text-green-400";
        button.textContent = `[ ${options.labels.resolve} ]`;
        cell.appendChild(button);
        return cell;
    }

    function createEmptyRow() {
        const row = document.createElement("tr");
        row.dataset.alertEmpty = "true";

        const cell = document.createElement("td");
        cell.colSpan = 6;
        cell.className =
            "px-5 py-12 text-center text-xs uppercase tracking-[0.18em] text-slate-600";
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
            const formData = new FormData();
            formData.set("id", alertId);
            formData.set("__RequestVerificationToken", options.antiForgeryToken);

            const response = await fetch(options.resolveUrl, {
                method: "POST",
                body: formData,
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(
                    `Resolve failed with status ${response.status}.`);
            }

            scheduleRefresh(true);
        } catch (error) {
            console.error("Alert resolve failed.", error);
            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    }

    async function runAiAnalysis() {
        const button = document.querySelector(
            "[data-alert-ai-run]");
        const output = document.querySelector(
            "[data-alert-ai-output]");
        const spinner = document.querySelector(
            "[data-alert-ai-spinner]");

        if (!button || !output || button.disabled) {
            return;
        }

        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        spinner?.classList.remove("hidden");
        output.textContent = options.labels.aiProcessing;
        output.classList.remove("text-red-400");

        const startedAt = performance.now();

        try {
            const response = await postForm(options.aiUrl);
            const elapsed = performance.now() - startedAt;

            if (elapsed < aiMinimumLatencyMs) {
                await delay(aiMinimumLatencyMs - elapsed);
            }

            const payload = await readJson(response);

            if (!response.ok) {
                throw new Error(
                    payload?.message
                    || `AI analysis failed with status ${response.status}.`);
            }

            output.textContent = payload.narrative;
        } catch (error) {
            output.textContent =
                error.message || options.labels.requestFailed;
            output.classList.add("text-red-400");
        } finally {
            spinner?.classList.add("hidden");
            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    }

    async function broadcastDiscord() {
        const button = document.querySelector(
            "[data-alert-discord]");
        const output = document.querySelector(
            "[data-alert-discord-output]");

        if (!button || !output || button.disabled) {
            return;
        }

        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        output.textContent = options.labels.discordSending;
        output.className =
            "mt-3 min-h-5 text-[10px] leading-5 text-slate-500";

        try {
            const response = await postForm(options.discordUrl);
            const payload = await readJson(response);

            if (!response.ok || !payload?.sent) {
                throw new Error(
                    payload?.message
                    || `Discord broadcast failed with status ${response.status}.`);
            }

            output.textContent = payload.message;
            output.className =
                "mt-3 min-h-5 text-[10px] leading-5 text-green-400";
        } catch (error) {
            output.textContent =
                error.message || options.labels.requestFailed;
            output.className =
                "mt-3 min-h-5 text-[10px] leading-5 text-red-400";
        } finally {
            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    }

    async function postForm(url) {
        const formData = new FormData();
        formData.set(
            "__RequestVerificationToken",
            options.antiForgeryToken);

        return await fetch(url, {
            method: "POST",
            body: formData,
            credentials: "same-origin",
            headers: {
                Accept: "application/json"
            }
        });
    }

    async function readJson(response) {
        const contentType =
            response.headers.get("content-type") || "";

        if (!contentType.includes("application/json")) {
            return null;
        }

        return await response.json();
    }

    function updateCount(id, value) {
        const element = document.getElementById(id);

        if (element) {
            element.textContent = Number(value || 0)
                .toLocaleString(options.culture);
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

    function severityClasses(severity) {
        const base =
            "inline-flex min-w-[92px] items-center justify-center whitespace-nowrap border px-3 py-1.5 text-[9px] font-semibold uppercase tracking-[0.14em]";

        switch (severity) {
            case "Fatal":
                return `${base} border-red-500/30 bg-red-500/10 text-red-400`;
            case "Critical":
                return `${base} border-orange-500/30 bg-orange-500/10 text-orange-400`;
            case "Warning":
                return `${base} border-amber-500/30 bg-amber-500/10 text-amber-400`;
            default:
                return `${base} border-slate-700 bg-slate-900/70 text-slate-300`;
        }
    }

    function severityLabel(severity) {
        return options.labels.severities?.[severity]
            || severity
            || "INFO";
    }

    function delay(milliseconds) {
        return new Promise(resolve => {
            window.setTimeout(resolve, milliseconds);
        });
    }

    return { init };
})();
