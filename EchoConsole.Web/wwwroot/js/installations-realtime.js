window.echoConsoleInstallationsRealtime = (() => {
    "use strict";

    let connection = null;
    let options = null;
    let refreshTimer = null;
    let searchTimer = null;
    let reconnectTimer = null;
    let fetchController = null;
    let requestSequence = 0;

    const searchDebounceMs = 400;
    const realtimeRefreshDelayMs = 1000;

    function init(config) {
        options = {
            webBaseUrl: normalizeBaseUrl(config.webBaseUrl),
            tableBodyId: config.tableBodyId,
            totalCountValueId: config.totalCountValueId,
            resultsCountValueId: config.resultsCountValueId,
            pageNumberValueId: config.pageNumberValueId,
            totalPagesValueId: config.totalPagesValueId,
            prevPageLinkId: config.prevPageLinkId,
            nextPageLinkId: config.nextPageLinkId,
            searchFormId: config.searchFormId,
            searchInputId: config.searchInputId,
            pageSizeInputId: config.pageSizeInputId,
            emptyTextElementId: config.emptyTextElementId,
            editDialogId: config.editDialogId,
            editIdInputId: config.editIdInputId,
            editAliasInputId: config.editAliasInputId,
            editStatusInputId: config.editStatusInputId,
            detailsDialogId: config.detailsDialogId,
            detailsGridId: config.detailsGridId,
            deleteFormId: config.deleteFormId,
            deleteIdInputId: config.deleteIdInputId,
            culture:
                config.culture
                || document.documentElement.lang
                || "en",
            labels: config.labels || {}
        };

        bindSearch();
        bindActions();
        bindPagination();

        if (window.signalR) {
            buildConnection();
            void startConnection();
        }

        void refreshInstallations({
            pageNumber: getCurrentPageNumber(),
            updateUrl: false
        });
    }

    function bindSearch() {
        const form =
            document.getElementById(
                options.searchFormId);

        const input =
            document.getElementById(
                options.searchInputId);

        form?.addEventListener(
            "submit",
            event => {
                event.preventDefault();

                window.clearTimeout(searchTimer);

                void refreshInstallations({
                    pageNumber: 1,
                    updateUrl: true
                });
            });

        input?.addEventListener(
            "input",
            () => {
                window.clearTimeout(searchTimer);

                searchTimer = window.setTimeout(
                    () => {
                        void refreshInstallations({
                            pageNumber: 1,
                            updateUrl: true
                        });
                    },
                    searchDebounceMs);
            });
    }

    function bindPagination() {
        for (const id of [
            options.prevPageLinkId,
            options.nextPageLinkId
        ]) {
            document
                .getElementById(id)
                ?.addEventListener(
                    "click",
                    event => {
                        const link =
                            event.currentTarget;

                        if (
                            link.getAttribute(
                                "aria-disabled")
                            === "true"
                        ) {
                            event.preventDefault();
                            return;
                        }

                        event.preventDefault();

                        const targetUrl =
                            new URL(
                                link.href,
                                window.location.origin);

                        const targetPage =
                            Number(
                                targetUrl.searchParams
                                    .get("pageNumber")
                                ?? "1");

                        void refreshInstallations({
                            pageNumber:
                                Number.isFinite(targetPage)
                                    ? targetPage
                                    : 1,
                            updateUrl: true
                        });
                    });
        }
    }

    function bindActions() {
        document.addEventListener(
            "click",
            event => {
                const editButton =
                    event.target.closest(
                        "[data-installation-edit]");

                if (editButton) {
                    openEditDialog(editButton);
                    return;
                }

                const detailsButton =
                    event.target.closest(
                        "[data-installation-details]");

                if (detailsButton) {
                    openDetailsDialog(detailsButton);
                    return;
                }

                const deleteButton =
                    event.target.closest(
                        "[data-installation-delete]");

                if (deleteButton) {
                    deleteInstallation(deleteButton);
                    return;
                }

                const closeButton =
                    event.target.closest(
                        "[data-dialog-close]");

                if (closeButton) {
                    closeButton
                        .closest("dialog")
                        ?.close();
                }
            });

        for (const id of [
            options.editDialogId,
            options.detailsDialogId
        ]) {
            const dialog =
                document.getElementById(id);

            dialog?.addEventListener(
                "click",
                event => {
                    if (event.target === dialog) {
                        dialog.close();
                    }
                });
        }
    }

    function openEditDialog(button) {
        setInputValue(
            options.editIdInputId,
            button.dataset.installationId);

        setInputValue(
            options.editAliasInputId,
            button.dataset.adminAlias);

        setInputValue(
            options.editStatusInputId,
            button.dataset.adminStatus || "Active");

        const dialog =
            document.getElementById(
                options.editDialogId);

        dialog?.showModal();

        document
            .getElementById(options.editAliasInputId)
            ?.focus();
    }

    function openDetailsDialog(button) {
        const grid =
            document.getElementById(
                options.detailsGridId);

        if (!grid) {
            return;
        }

        const fields = [
            ["INSTALLATION_ID", button.dataset.installationId],
            ["ADMIN_ALIAS", button.dataset.adminAlias],
            ["ADMIN_STATUS", button.dataset.adminStatus],
            ["OWNER", button.dataset.owner],
            ["GAME_CODE", button.dataset.gameCode],
            ["BUILD_VERSION", button.dataset.buildVersion],
            ["PLATFORM", button.dataset.platform],
            ["DEVICE_NAME", button.dataset.deviceName],
            ["DEVICE_MODEL", button.dataset.deviceModel],
            ["OPERATING_SYSTEM", button.dataset.osVersion],
            ["PROCESSOR", button.dataset.processor],
            ["GPU", button.dataset.gpu],
            ["RAM", button.dataset.ram],
            ["TELEMETRY_STATUS", button.dataset.telemetryStatus],
            ["FIRST_SEEN_UTC", button.dataset.firstSeen],
            ["LAST_UPDATE_UTC", button.dataset.lastUpdate]
        ];

        const fragment =
            document.createDocumentFragment();

        for (const [label, rawValue] of fields) {
            const container =
                document.createElement("div");

            container.className =
                "min-w-0 bg-[#050807] px-5 py-4";

            const term =
                document.createElement("dt");

            term.className =
                "text-[9px] uppercase tracking-[0.18em] text-slate-600";

            term.textContent = label;

            const value =
                document.createElement("dd");

            value.className =
                "mt-2 break-words text-xs text-slate-200";

            value.textContent =
                rawValue?.trim()
                || "-";

            container.append(term, value);
            fragment.appendChild(container);
        }

        grid.replaceChildren(fragment);

        document
            .getElementById(
                options.detailsDialogId)
            ?.showModal();
    }

    function deleteInstallation(button) {
        const confirmed =
            window.confirm(
                options.labels.deleteConfirm
                || "Delete this installation and its related telemetry?");

        if (!confirmed) {
            return;
        }

        setInputValue(
            options.deleteIdInputId,
            button.dataset.installationId);

        document
            .getElementById(
                options.deleteFormId)
            ?.requestSubmit();
    }

    function buildConnection() {
        connection =
            new signalR.HubConnectionBuilder()
                .withUrl(
                    buildWebUrl(
                        "/hubs/admin-telemetry"),
                    {
                        withCredentials: true
                    })
                .withAutomaticReconnect(
                    [0, 2000, 5000, 10000, 15000])
                .configureLogging(
                    signalR.LogLevel.Warning)
                .build();

        connection.on(
            "ReceiveTelemetryUpdate",
            () => scheduleRealtimeRefresh());

        connection.onreconnected(
            () => scheduleRealtimeRefresh(true));

        connection.onclose(
            () => {
                window.clearTimeout(
                    reconnectTimer);

                reconnectTimer =
                    window.setTimeout(
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
                "Installations SignalR connection failed.",
                error);

            window.clearTimeout(
                reconnectTimer);

            reconnectTimer =
                window.setTimeout(
                    () => void startConnection(),
                    5000);
        }
    }

    function scheduleRealtimeRefresh(immediate = false) {
        window.clearTimeout(refreshTimer);

        refreshTimer =
            window.setTimeout(
                () => {
                    void refreshInstallations({
                        pageNumber:
                            getCurrentPageNumber(),
                        updateUrl: false
                    });
                },
                immediate
                    ? 0
                    : realtimeRefreshDelayMs);
    }

    async function refreshInstallations({
        pageNumber,
        updateUrl
    }) {
        const sequence =
            ++requestSequence;

        fetchController?.abort();
        fetchController =
            new AbortController();

        const signal =
            fetchController.signal;

        try {
            const pageSize =
                getPageSize();

            const searchTerm =
                getSearchTerm();

            let response =
                await fetchInstallationsPage(
                    pageNumber,
                    pageSize,
                    searchTerm,
                    signal);

            if (
                response.totalPages > 0
                && pageNumber > response.totalPages
            ) {
                pageNumber =
                    response.totalPages;

                response =
                    await fetchInstallationsPage(
                        pageNumber,
                        pageSize,
                        searchTerm,
                        signal);
            }

            if (sequence !== requestSequence) {
                return;
            }

            await new Promise(resolve => {
                window.requestAnimationFrame(() => {
                    if (sequence === requestSequence) {
                        renderInstallations(response);
                    }

                    resolve();
                });
            });

            if (
                updateUrl
                && sequence === requestSequence
            ) {
                updateBrowserUrl(
                    pageNumber,
                    pageSize,
                    searchTerm);
            }
        } catch (error) {
            if (error.name !== "AbortError") {
                console.error(
                    "Installations refresh failed.",
                    error);
            }
        }
    }

    async function fetchInstallationsPage(
        pageNumber,
        pageSize,
        searchTerm,
        signal) {
        const params =
            new URLSearchParams();

        params.set(
            "pageNumber",
            String(pageNumber));

        params.set(
            "pageSize",
            String(pageSize));

        if (searchTerm) {
            params.set(
                "searchTerm",
                searchTerm);
        }

        const response = await fetch(
            buildWebUrl(
                `/Installations/ListData?${params.toString()}`),
            {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    Accept: "application/json"
                },
                signal
            });

        if (!response.ok) {
            throw new Error(
                `Installations request failed with status ${response.status}`);
        }

        return await response.json();
    }

    function renderInstallations(response) {
        const tableBody =
            document.getElementById(
                options.tableBodyId);

        if (!tableBody) {
            return;
        }

        const items =
            Array.isArray(response.items)
                ? response.items
                : [];

        const totalCount =
            Number(response.totalCount ?? 0);

        const page =
            Number(response.page ?? 1);

        const totalPages =
            Math.max(
                1,
                Number(response.totalPages ?? 1));

        setText(
            options.totalCountValueId,
            formatNumber(totalCount));

        setText(
            options.resultsCountValueId,
            formatNumber(totalCount));

        setText(
            options.pageNumberValueId,
            String(page));

        setText(
            options.totalPagesValueId,
            String(totalPages));

        updatePaginationLinks(
            page,
            totalPages);

        if (items.length === 0) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-5 py-14 text-center text-xs uppercase tracking-[0.18em] text-slate-600">
                        [ ${escapeHtml(
                            options.labels.emptyState
                            || "No installations found."
                        )} ]
                    </td>
                </tr>`;

            return;
        }

        tableBody.innerHTML =
            items.map(renderRow).join("");
    }

    function renderRow(item) {
        const installationId =
            String(item.installationId ?? "");

        const adminAlias =
            String(item.adminAlias ?? "");

        const adminStatus =
            String(item.adminStatus || "Active");

        const displayName =
            adminAlias.trim()
            || item.deviceName
            || "-";

        const owner =
            item.ownerUserId
                ? `${item.ownerAlias || "-"} / #${item.ownerUserId}`
                : options.labels.ownerUnassigned
                  || "-";

        return `
            <tr class="transition-colors hover:bg-slate-900/40">
                <td class="px-4 py-4">
                    <p class="font-display text-sm text-slate-100">${escapeHtml(displayName)}</p>
                    <p class="mt-1 text-[9px] uppercase tracking-[0.13em] text-slate-700">
                        ${escapeHtml(item.deviceName || "-")} // ${escapeHtml(installationId)}
                    </p>
                </td>
                <td class="px-4 py-4">
                    <span class="border border-cyan-900/60 bg-cyan-950/20 px-2.5 py-1 text-xs text-cyan-400">
                        ${escapeHtml(item.buildVersion || "-")}
                    </span>
                    <p class="mt-2 text-[9px] uppercase tracking-[0.14em] text-slate-700">
                        ${escapeHtml(item.platform || "-")}
                    </p>
                </td>
                <td class="px-4 py-4 text-xs text-slate-400">${escapeHtml(owner)}</td>
                <td class="px-4 py-4">
                    <span class="inline-flex h-7 min-w-24 items-center justify-center border px-3 text-[9px] font-semibold uppercase tracking-[0.15em] ${statusClass(adminStatus)}">
                        ${escapeHtml(localizedStatus(adminStatus))}
                    </span>
                </td>
                <td class="px-4 py-4 text-xs text-slate-500">${escapeHtml(formatUtc(item.lastUpdateUtc))}</td>
                <td class="px-4 py-4 text-right">
                    <div class="inline-flex items-center gap-2">
                        ${renderEditButton(item, installationId, adminAlias, adminStatus)}
                        ${renderDetailsButton(item, installationId, adminAlias, adminStatus, owner)}
                        ${renderDeleteButton(installationId)}
                    </div>
                </td>
            </tr>`;
    }

    function renderEditButton(
        item,
        installationId,
        adminAlias,
        adminStatus) {
        return `
            <button type="button"
                    data-installation-edit
                    data-installation-id="${escapeAttribute(installationId)}"
                    data-admin-alias="${escapeAttribute(adminAlias)}"
                    data-admin-status="${escapeAttribute(adminStatus)}"
                    class="h-8 w-28 shrink-0 border border-slate-700 bg-slate-900/70 px-2 text-[9px] font-semibold uppercase tracking-[0.12em] text-slate-300 transition-colors hover:bg-slate-800/90 hover:text-cyan-300">
                [ ${escapeHtml(options.labels.editAlias || "EDIT ALIAS")} ]
            </button>`;
    }

    function renderDetailsButton(
        item,
        installationId,
        adminAlias,
        adminStatus,
        owner) {
        return `
            <button type="button"
                    data-installation-details
                    data-installation-id="${escapeAttribute(installationId)}"
                    data-game-code="${escapeAttribute(item.gameCode)}"
                    data-build-version="${escapeAttribute(item.buildVersion)}"
                    data-platform="${escapeAttribute(item.platform)}"
                    data-device-name="${escapeAttribute(item.deviceName)}"
                    data-device-model="${escapeAttribute(item.deviceModel)}"
                    data-os-version="${escapeAttribute(item.osVersion)}"
                    data-processor="${escapeAttribute(item.processor)}"
                    data-gpu="${escapeAttribute(item.gpu)}"
                    data-ram="${escapeAttribute(formatRam(item.ramMb))}"
                    data-telemetry-status="${escapeAttribute(item.telemetryStatus)}"
                    data-admin-alias="${escapeAttribute(adminAlias)}"
                    data-admin-status="${escapeAttribute(adminStatus)}"
                    data-owner="${escapeAttribute(owner)}"
                    data-first-seen="${escapeAttribute(formatUtc(item.firstSeenUtc))}"
                    data-last-update="${escapeAttribute(formatUtc(item.lastUpdateUtc))}"
                    class="h-8 w-24 shrink-0 border border-cyan-900/60 bg-cyan-950/20 px-2 text-[9px] font-semibold uppercase tracking-[0.12em] text-cyan-400 transition-colors hover:bg-cyan-900/30 hover:text-cyan-200">
                [ ${escapeHtml(options.labels.details || "DETAILS")} ]
            </button>`;
    }

    function renderDeleteButton(installationId) {
        return `
            <button type="button"
                    data-installation-delete
                    data-installation-id="${escapeAttribute(installationId)}"
                    class="h-8 w-20 shrink-0 border border-red-900/60 bg-red-950/20 px-2 text-[9px] font-semibold uppercase tracking-[0.12em] text-red-500 transition-colors hover:bg-red-900/30 hover:text-red-300">
                [ ${escapeHtml(options.labels.delete || "DELETE")} ]
            </button>`;
    }

    function statusClass(status) {
        if (status === "Banned") {
            return "border-red-500/30 bg-red-500/5 text-red-400";
        }

        if (status === "Inactive") {
            return "border-amber-500/30 bg-amber-500/5 text-amber-400";
        }

        return "border-green-500/30 bg-green-500/5 text-green-400";
    }

    function localizedStatus(status) {
        if (status === "Banned") {
            return options.labels.statusBanned || status;
        }

        if (status === "Inactive") {
            return options.labels.statusInactive || status;
        }

        return options.labels.statusActive || status;
    }

    function updatePaginationLinks(page, totalPages) {
        setPaginationLink(
            document.getElementById(
                options.prevPageLinkId),
            Math.max(1, page - 1),
            page > 1);

        setPaginationLink(
            document.getElementById(
                options.nextPageLinkId),
            Math.min(totalPages, page + 1),
            page < totalPages);
    }

    function setPaginationLink(
        element,
        targetPage,
        enabled) {
        if (!element) {
            return;
        }

        const url =
            new URL(
                window.location.pathname,
                window.location.origin);

        url.searchParams.set(
            "pageNumber",
            String(targetPage));

        url.searchParams.set(
            "pageSize",
            String(getPageSize()));

        const searchTerm =
            getSearchTerm();

        if (searchTerm) {
            url.searchParams.set(
                "searchTerm",
                searchTerm);
        }

        element.href =
            url.pathname + url.search;

        element.setAttribute(
            "aria-disabled",
            enabled ? "false" : "true");

        element.classList.toggle(
            "pointer-events-none",
            !enabled);

        element.classList.toggle(
            "opacity-40",
            !enabled);
    }

    function updateBrowserUrl(
        pageNumber,
        pageSize,
        searchTerm) {
        const url =
            new URL(window.location.href);

        url.searchParams.set(
            "pageNumber",
            String(pageNumber));

        url.searchParams.set(
            "pageSize",
            String(pageSize));

        if (searchTerm) {
            url.searchParams.set(
                "searchTerm",
                searchTerm);
        } else {
            url.searchParams.delete(
                "searchTerm");
        }

        window.history.replaceState(
            {},
            "",
            url.pathname + url.search);
    }

    function getSearchTerm() {
        return document
            .getElementById(
                options.searchInputId)
            ?.value
            ?.trim()
            ?? "";
    }

    function getPageSize() {
        const value =
            Number(
                document
                    .getElementById(
                        options.pageSizeInputId)
                    ?.value
                ?? 20);

        return Number.isFinite(value)
            && value > 0
                ? value
                : 20;
    }

    function getCurrentPageNumber() {
        const value =
            Number(
                document
                    .getElementById(
                        options.pageNumberValueId)
                    ?.textContent
                ?? 1);

        return Number.isFinite(value)
            && value > 0
                ? value
                : 1;
    }

    function setInputValue(id, value) {
        const input =
            document.getElementById(id);

        if (input) {
            input.value = value ?? "";
        }
    }

    function setText(id, value) {
        const element =
            document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
    }

    function formatNumber(value) {
        const numeric =
            Number(value);

        return Number.isFinite(numeric)
            ? new Intl.NumberFormat(
                options.culture)
                .format(numeric)
            : "0";
    }

    function formatRam(value) {
        const numeric =
            Number(value);

        return Number.isFinite(numeric)
            ? `${formatNumber(numeric)} MB`
            : "-";
    }

    function formatUtc(value) {
        if (!value) {
            return "-";
        }

        const date =
            new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return new Intl.DateTimeFormat(
            options.culture,
            {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
                hour12: false,
                timeZone: "UTC"
            })
            .format(date)
            + " UTC";
    }

    function buildWebUrl(path) {
        return `${options.webBaseUrl}${path}`;
    }

    function normalizeBaseUrl(baseUrl) {
        if (!baseUrl) {
            return "";
        }

        return baseUrl.endsWith("/")
            ? baseUrl.slice(0, -1)
            : baseUrl;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function escapeAttribute(value) {
        return escapeHtml(value)
            .replaceAll("`", "&#96;");
    }

    return {
        init
    };
})();
