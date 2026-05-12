window.echoConsoleInstallationsRealtime = (() => {
    let connection = null;
    let options = null;
    let refreshTimer = null;
    let currentResponse = null;

    function init(config) {
        options = {
            apiBaseUrl: normalizeBaseUrl(config.apiBaseUrl),
            tableBodyId: config.tableBodyId,
            totalCountValueId: config.totalCountValueId,
            pageNumberValueId: config.pageNumberValueId,
            totalPagesValueId: config.totalPagesValueId,
            prevPageLinkId: config.prevPageLinkId,
            nextPageLinkId: config.nextPageLinkId,
            searchInputId: config.searchInputId,
            pageSizeInputId: config.pageSizeInputId,
            emptyTextElementId: config.emptyTextElementId
        };

        if (!options.apiBaseUrl) {
            console.error("Installations realtime: apiBaseUrl is missing.");
            return;
        }

        if (!window.signalR) {
            console.error("Installations realtime: SignalR library not found.");
            return;
        }

        window.addEventListener("echoConsole:languageChanged", () => {
            if (currentResponse) {
                renderInstallations(currentResponse);
            }
        });

        buildConnection();
        startConnection();
    }

    function buildConnection() {
        const hubUrl = buildApiUrl("/hubs/telemetry");

        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { withCredentials: true })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("installationUpdated", onInstallationChanged);
        connection.on("newInstallation", onInstallationChanged);
        connection.on("ReceiveTelemetryUpdate", onGenericTelemetryEvent);

        connection.onreconnecting(() => {
            console.warn("Installations SignalR reconnecting...");
        });

        connection.onreconnected(() => {
            console.info("Installations SignalR reconnected.");
            scheduleRefresh(true);
        });

        connection.onclose(() => {
            console.warn("Installations SignalR closed. Retrying...");
            setTimeout(startConnection, 5000);
        });
    }

    async function startConnection() {
        if (!connection) {
            return;
        }

        try {
            await connection.start();
            console.info("Installations SignalR connected.");
        } catch (error) {
            console.error("Installations SignalR connection failed.", error);
            setTimeout(startConnection, 5000);
        }
    }

    function onInstallationChanged() {
        scheduleRefresh(false);
    }

    function onGenericTelemetryEvent(payload) {
        if (!payload) {
            return;
        }

        scheduleRefresh(false);
    }

    function scheduleRefresh(immediate) {
        if (refreshTimer) {
            clearTimeout(refreshTimer);
        }

        refreshTimer = setTimeout(() => {
            refreshInstallations();
        }, immediate ? 0 : 300);
    }

    async function refreshInstallations() {
        try {
            const response = await fetchInstallations();
            currentResponse = response;
            renderInstallations(response);
        } catch (error) {
            console.error("Installations refresh failed.", error);
        }
    }

    async function fetchInstallations() {
        let pageNumber = getCurrentPageNumber();
        const pageSize = getPageSize();
        const searchTerm = getSearchTerm();

        let response = await fetchInstallationsPage(pageNumber, pageSize, searchTerm);

        if (response.totalPages > 0 && pageNumber > response.totalPages) {
            pageNumber = response.totalPages;
            response = await fetchInstallationsPage(pageNumber, pageSize, searchTerm);
        }

        return response;
    }

    async function fetchInstallationsPage(pageNumber, pageSize, searchTerm) {
        const params = new URLSearchParams();
        params.set("page", String(pageNumber));
        params.set("pageSize", String(pageSize));

        if (searchTerm) {
            params.set("search", searchTerm);
        }

        const url = buildApiUrl(`/api/admin/installations?${params.toString()}`);

        const response = await fetch(url, {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        });

        if (!response.ok) {
            throw new Error(`Installations request failed with status ${response.status}`);
        }

        return await response.json();
    }

    function renderInstallations(response) {
        const tableBody = document.getElementById(options.tableBodyId);
        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";

        const items = Array.isArray(response.items) ? response.items : [];
        const totalCount = Number(response.totalCount ?? 0);
        const page = Number(response.page ?? 1);
        const totalPages = Math.max(1, Number(response.totalPages ?? 1));

        setText(options.totalCountValueId, formatNumber(totalCount));
        setText(options.pageNumberValueId, String(page));
        setText(options.totalPagesValueId, String(totalPages));

        updatePaginationLinks(page, totalPages);

        if (items.length === 0) {
            const emptyRow = document.createElement("tr");
            const emptyText =
                window.echoConsoleI18n?.t?.("installations_empty_state") ||
                document.getElementById(options.emptyTextElementId)?.textContent?.trim() ||
                "No installations found.";

            emptyRow.innerHTML = `
                <td colspan="7" class="px-5 py-6 text-center text-sm text-slate-400">
                    ${escapeHtml(emptyText)}
                </td>
            `;

            tableBody.appendChild(emptyRow);
            return;
        }

        for (const item of items) {
            const row = document.createElement("tr");
            row.className = "transition-colors duration-150 hover:bg-slate-900/70";

            row.innerHTML = `
                <td class="px-5 py-4 text-sm text-cyan-300">${escapeHtml(item.installationId ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-200">${escapeHtml(item.deviceName ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-300">${escapeHtml(item.osVersion ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-300">${escapeHtml(item.processor ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-300">${escapeHtml(item.gpu ?? "-")}</td>
                <td class="px-5 py-4 text-sm text-slate-300">${formatRam(item.ramMb)}</td>
                <td class="px-5 py-4 text-sm text-slate-400">${formatUtc(item.lastUpdateUtc)}</td>
            `;

            tableBody.appendChild(row);
        }
    }

    function updatePaginationLinks(page, totalPages) {
        const prev = document.getElementById(options.prevPageLinkId);
        const next = document.getElementById(options.nextPageLinkId);

        if (!prev || !next) {
            return;
        }

        const searchTerm = getSearchTerm();
        const pageSize = getPageSize();

        setPaginationLink(prev, Math.max(1, page - 1), page > 1, searchTerm, pageSize);
        setPaginationLink(next, Math.min(totalPages, page + 1), page < totalPages, searchTerm, pageSize);
    }

    function setPaginationLink(element, targetPage, enabled, searchTerm, pageSize) {
        const url = new URL(window.location.pathname, window.location.origin);
        url.searchParams.set("pageNumber", String(targetPage));
        url.searchParams.set("pageSize", String(pageSize));

        if (searchTerm) {
            url.searchParams.set("searchTerm", searchTerm);
        }

        element.setAttribute("href", url.pathname + url.search);
        element.setAttribute("aria-disabled", enabled ? "false" : "true");

        if (enabled) {
            element.classList.remove("pointer-events-none", "opacity-40");
        } else {
            element.classList.add("pointer-events-none", "opacity-40");
        }
    }

    function getSearchTerm() {
        const input = document.getElementById(options.searchInputId);
        return input?.value?.trim() ?? "";
    }

    function getPageSize() {
        const input = document.getElementById(options.pageSizeInputId);
        const value = Number(input?.value ?? 20);
        return Number.isNaN(value) || value < 1 ? 20 : value;
    }

    function getCurrentPageNumber() {
        const element = document.getElementById(options.pageNumberValueId);
        const value = Number(element?.textContent ?? 1);
        return Number.isNaN(value) || value < 1 ? 1 : value;
    }

    function buildApiUrl(path) {
        return `${options.apiBaseUrl}${path}`;
    }

    function normalizeBaseUrl(baseUrl) {
        if (!baseUrl) {
            return "";
        }

        return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value;
        }
    }

    function formatNumber(value) {
        const numeric = Number(value);
        if (Number.isNaN(numeric)) {
            return "0";
        }

        return numeric.toLocaleString("en-US");
    }

    function formatRam(value) {
        if (value === null || value === undefined || value === "") {
            return "-";
        }

        const numeric = Number(value);
        if (Number.isNaN(numeric)) {
            return "-";
        }

        return `${numeric.toLocaleString("en-US")} MB`;
    }

    function formatUtc(value) {
        if (!value) {
            return "-";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("en-GB", {
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