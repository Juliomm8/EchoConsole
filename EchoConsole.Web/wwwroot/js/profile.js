(() => {
    "use strict";

    const root = document.querySelector("[data-profile-root]");

    if (!root) {
        return;
    }

    const antiforgeryToken =
        document.querySelector(
            "[data-antiforgery-form] input[name='__RequestVerificationToken']"
        )?.value ?? "";

    const endpoints = {
        identity: root.dataset.identityUrl,
        security: root.dataset.securityUrl,
        fleet: root.dataset.fleetUrl,
        updateIdentity: root.dataset.updateIdentityUrl,
        changePassword: root.dataset.changePasswordUrl,
        terminateSessions: root.dataset.terminateSessionsUrl,
        unlinkDevice: root.dataset.unlinkDeviceUrl
    };

    const tabs = Array.from(
        root.querySelectorAll("[data-profile-tab]")
    );

    const panels = Array.from(
        root.querySelectorAll("[data-profile-panel]")
    );

    const identityForm = root.querySelector("[data-identity-form]");
    const passwordForm = root.querySelector("[data-password-form]");
    const terminateSessionsButton = root.querySelector("[data-terminate-sessions]");
    const fleetList = root.querySelector("[data-fleet-list]");
    const loadedSections = new Set();

    const sectionLoaders = {
        identity: loadIdentity,
        security: loadSecurity,
        fleet: loadFleet
    };

    let modalResolver = null;

    initialize();

    function initialize() {
        bindTabs();
        bindKeyboardShortcuts();
        bindIdentityForm();
        bindPasswordForm();
        bindSessionTermination();
        bindFleetActions();
        bindConfirmationModal();

        activateSection(
            normalizeSection(root.dataset.initialSection),
            {
                updateUrl: false,
                focusTab: false
            }
        );
    }

    function bindTabs() {
        for (const tab of tabs) {
            tab.addEventListener("click", () => {
                activateSection(
                    normalizeSection(tab.dataset.profileTab),
                    {
                        updateUrl: true,
                        focusTab: false
                    }
                );
            });

            tab.addEventListener("keydown", event => {
                const currentIndex = tabs.indexOf(tab);
                let nextIndex = currentIndex;

                if (event.key === "ArrowRight") {
                    nextIndex = (currentIndex + 1) % tabs.length;
                } else if (event.key === "ArrowLeft") {
                    nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
                } else if (event.key === "Home") {
                    nextIndex = 0;
                } else if (event.key === "End") {
                    nextIndex = tabs.length - 1;
                } else {
                    return;
                }

                event.preventDefault();

                activateSection(
                    normalizeSection(tabs[nextIndex].dataset.profileTab),
                    {
                        updateUrl: true,
                        focusTab: true
                    }
                );
            });
        }
    }

    function bindKeyboardShortcuts() {
        document.addEventListener("keydown", event => {
            if (
                !event.altKey ||
                event.ctrlKey ||
                event.metaKey ||
                event.shiftKey ||
                isTypingTarget(event.target)
            ) {
                return;
            }

            const sectionByKey = {
                "1": "identity",
                "2": "security",
                "3": "fleet"
            };

            const section = sectionByKey[event.key];

            if (!section) {
                return;
            }

            event.preventDefault();

            activateSection(section, {
                updateUrl: true,
                focusTab: true
            });
        });

        window.addEventListener("popstate", () => {
            const section = new URL(window.location.href)
                .searchParams
                .get("section");

            activateSection(normalizeSection(section), {
                updateUrl: false,
                focusTab: false
            });
        });
    }

    function bindIdentityForm() {
        if (!identityForm) {
            return;
        }

        identityForm.addEventListener(
            "change",
            updateIdentityPreviewFromForm
        );

        identityForm.addEventListener(
            "input",
            updateIdentityPreviewFromForm
        );

        identityForm.addEventListener("submit", async event => {
            event.preventDefault();

            const submitButton = identityForm.querySelector(
                "[data-identity-submit]"
            );

            const formData = new FormData(identityForm);
            const payload = {
                alias: formData.get("Alias")?.toString().trim() ?? "",
                avatarKey: formData.get("AvatarKey")?.toString() ?? "",
                theme: formData.get("Theme")?.toString() ?? "",
                preferredLanguage:
                    formData.get("PreferredLanguage")?.toString() ?? ""
            };

            setBusy(submitButton, true);

            try {
                const response = await requestJson(
                    endpoints.updateIdentity,
                    {
                        method: "POST",
                        body: payload
                    }
                );

                applyIdentityMutation(response.data);
                loadedSections.delete("identity");

                showToast(
                    response.message ?? "Player identity synchronized.",
                    "success"
                );
            } catch (error) {
                showToast(getErrorMessage(error), "error");
            } finally {
                setBusy(submitButton, false);
            }
        });
    }

    function bindPasswordForm() {
        if (!passwordForm) {
            return;
        }

        passwordForm.addEventListener("submit", async event => {
            event.preventDefault();

            const formData = new FormData(passwordForm);
            const newPassword =
                formData.get("NewPassword")?.toString() ?? "";
            const confirmPassword =
                formData.get("ConfirmPassword")?.toString() ?? "";

            if (newPassword !== confirmPassword) {
                showToast(
                    "The password confirmation does not match.",
                    "error"
                );
                return;
            }

            const accepted = await confirmAction({
                title: "ROTATE CREDENTIALS",
                message:
                    "Changing the password will terminate every other authenticated browser session."
            });

            if (!accepted) {
                return;
            }

            const submitButton = passwordForm.querySelector(
                "[data-password-submit]"
            );

            setBusy(submitButton, true);

            try {
                const response = await requestJson(
                    endpoints.changePassword,
                    {
                        method: "POST",
                        body: {
                            currentPassword:
                                formData.get("CurrentPassword")?.toString() ?? "",
                            newPassword,
                            confirmPassword
                        }
                    }
                );

                passwordForm.reset();
                loadedSections.delete("security");
                await loadSecurity();

                showToast(
                    response.message ?? "Credentials rotated successfully.",
                    "success"
                );
            } catch (error) {
                showToast(getErrorMessage(error), "error");
            } finally {
                setBusy(submitButton, false);
            }
        });
    }

    function bindSessionTermination() {
        terminateSessionsButton?.addEventListener("click", async () => {
            const accepted = await confirmAction({
                title: "TERMINATE REMOTE SESSIONS",
                message:
                    "All authenticated browser sessions except this terminal will be revoked."
            });

            if (!accepted) {
                return;
            }

            setBusy(terminateSessionsButton, true);

            try {
                const response = await requestJson(
                    endpoints.terminateSessions,
                    {
                        method: "POST"
                    }
                );

                loadedSections.delete("security");
                await loadSecurity();

                showToast(
                    response.message ?? "Remote sessions terminated.",
                    "success"
                );
            } catch (error) {
                showToast(getErrorMessage(error), "error");
            } finally {
                setBusy(terminateSessionsButton, false);
            }
        });
    }

    function bindFleetActions() {
        fleetList?.addEventListener("click", async event => {
            const button = event.target.closest("[data-unlink-device]");

            if (!button) {
                return;
            }

            const installationId = button.dataset.unlinkDevice;
            const deviceName = button.dataset.deviceName ?? "this device";

            const accepted = await confirmAction({
                title: "UNLINK DEVICE",
                message:
                    `Remove ${deviceName} from your player inventory? ` +
                    "Telemetry history will remain stored."
            });

            if (!accepted) {
                return;
            }

            setBusy(button, true);

            try {
                const response = await requestJson(
                    endpoints.unlinkDevice,
                    {
                        method: "POST",
                        body: {
                            installationId
                        }
                    }
                );

                loadedSections.delete("fleet");
                loadedSections.delete("identity");
                await loadFleet();

                showToast(
                    response.message ??
                        "Device removed from the player fleet.",
                    "success"
                );
            } catch (error) {
                showToast(getErrorMessage(error), "error");
                setBusy(button, false);
            }
        });
    }

    function bindConfirmationModal() {
        const modal = root.querySelector("[data-confirm-modal]");

        modal?.querySelector("[data-confirm-cancel]")
            ?.addEventListener("click", () => {
                closeConfirmation(false);
            });

        modal?.querySelector("[data-confirm-accept]")
            ?.addEventListener("click", () => {
                closeConfirmation(true);
            });

        modal?.addEventListener("click", event => {
            if (event.target === modal) {
                closeConfirmation(false);
            }
        });

        document.addEventListener("keydown", event => {
            if (
                event.key === "Escape" &&
                modal &&
                !modal.classList.contains("hidden")
            ) {
                closeConfirmation(false);
            }
        });
    }

    async function activateSection(section, options = {}) {
        const {
            updateUrl = true,
            focusTab = false
        } = options;

        for (const tab of tabs) {
            const isActive = tab.dataset.profileTab === section;

            tab.setAttribute(
                "aria-selected",
                isActive ? "true" : "false"
            );
            tab.tabIndex = isActive ? 0 : -1;
            tab.classList.toggle("border-green-400", isActive);
            tab.classList.toggle("bg-green-500/10", isActive);
            tab.classList.toggle(
                "shadow-[inset_3px_0_0_rgba(74,222,128,0.8)]",
                isActive
            );
            tab.classList.toggle("border-slate-800", !isActive);
            tab.classList.toggle("bg-black", !isActive);

            if (isActive && focusTab) {
                tab.focus();
            }
        }

        for (const panel of panels) {
            const isActive = panel.dataset.profilePanel === section;
            panel.classList.toggle("hidden", !isActive);
            panel.hidden = !isActive;
        }

        if (updateUrl) {
            const url = new URL(window.location.href);
            url.searchParams.set("section", section);

            window.history.pushState(
                { section },
                "",
                url
            );
        }

        const loader = sectionLoaders[section];

        if (loader && !loadedSections.has(section)) {
            await loader();
        }
    }

    async function loadIdentity() {
        const loading = root.querySelector("[data-identity-loading]");
        toggleElement(loading, true);

        try {
            const response = await requestJson(endpoints.identity);
            renderIdentity(response.data);
            loadedSections.add("identity");
        } catch (error) {
            showToast(getErrorMessage(error), "error");
        } finally {
            toggleElement(loading, false);
        }
    }

    async function loadSecurity() {
        const loading = root.querySelector("[data-security-loading]");
        toggleElement(loading, true);

        try {
            const response = await requestJson(endpoints.security);
            renderSecurity(response.data);
            loadedSections.add("security");
        } catch (error) {
            showToast(getErrorMessage(error), "error");
        } finally {
            toggleElement(loading, false);
        }
    }

    async function loadFleet() {
        const loading = root.querySelector("[data-fleet-loading]");
        toggleElement(loading, true);

        try {
            const response = await requestJson(endpoints.fleet);
            renderFleet(response.data);
            loadedSections.add("fleet");
        } catch (error) {
            showToast(getErrorMessage(error), "error");
        } finally {
            toggleElement(loading, false);
        }
    }

    function renderIdentity(data) {
        if (!data) {
            return;
        }

        setText("[data-player-alias]", data.alias);
        setText("[data-player-name]", data.name);
        setText("[data-player-email]", data.email);
        setText("[data-player-role]", data.roleDisplayName);
        setText("[data-profile-role]", data.roleDisplayName);
        setText("[data-profile-status]", data.status);
        const avatarKey = normalizeAvatarKey(data.avatarKey);
        const theme = normalizeTheme(data.theme);

        setText("[data-player-avatar-key]", avatarKey);
        setText("[data-player-theme]", theme);
        setText(
            "[data-player-language]",
            data.preferredLanguage?.toUpperCase()
        );
        setText(
            "[data-email-confirmed]",
            data.emailConfirmed ? "VERIFIED" : "PENDING"
        );
        setText(
            "[data-total-play-time]",
            formatNumber(data.totalPlayTimeHours, 1)
        );
        setText(
            "[data-total-sessions]",
            formatNumber(data.totalSessions)
        );
        setText(
            "[data-linked-nodes]",
            formatNumber(data.linkedNodeCount)
        );
        setText(
            "[data-longest-session]",
            `${formatNumber(data.longestSessionMinutes)} MIN`
        );
        setText(
            "[data-favorite-build]",
            data.favoriteBuild || "N/A"
        );
        setText(
            "[data-last-activity]",
            data.lastActivityUtc
                ? formatDateTime(data.lastActivityUtc)
                : "NO ACTIVITY RECORDED"
        );
        setText(
            "[data-player-initials]",
            createInitials(data.alias)
        );

        const aliasInput = identityForm?.querySelector("[name='Alias']");

        if (aliasInput) {
            aliasInput.value = data.alias ?? "";
        }

        setRadioValue("AvatarKey", avatarKey);
        setRadioValue("Theme", theme);

        const languageSelect = identityForm?.querySelector(
            "[name='PreferredLanguage']"
        );

        if (languageSelect) {
            languageSelect.value = data.preferredLanguage ?? "en";
        }
    }

    function renderSecurity(data) {
        if (!data) {
            return;
        }

        const passwordUnavailable = root.querySelector(
            "[data-password-unavailable]"
        );

        toggleElement(
            passwordUnavailable,
            !data.hasLocalPassword
        );

        if (passwordForm) {
            for (const control of passwordForm.querySelectorAll(
                "input, button"
            )) {
                control.disabled = !data.hasLocalPassword;
            }

            passwordForm.classList.toggle(
                "hidden",
                !data.hasLocalPassword
            );
        }

        const sessionList = root.querySelector("[data-session-list]");

        if (!sessionList) {
            return;
        }

        const sessions = Array.isArray(data.activeSessions)
            ? data.activeSessions
            : [];

        if (terminateSessionsButton) {
            terminateSessionsButton.disabled =
                sessions.filter(session => !session.isCurrent).length === 0;
        }

        if (sessions.length === 0) {
            sessionList.innerHTML = `
                <div class="border border-slate-800 bg-black p-8 text-center text-xs uppercase tracking-[0.18em] text-slate-600">
                    NO ACTIVE AUTHENTICATION SESSIONS
                </div>
            `;
            return;
        }

        sessionList.innerHTML = sessions
            .map(session => `
                <article class="border ${
                    session.isCurrent
                        ? "border-green-500/35 bg-green-500/[0.05]"
                        : "border-slate-800 bg-black"
                } p-5">
                    <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                            <div class="flex flex-wrap items-center gap-2">
                                <h3 class="text-sm font-bold uppercase tracking-[0.12em] text-green-200">
                                    ${escapeHtml(session.deviceLabel)}
                                </h3>
                                ${
                                    session.isCurrent
                                        ? `<span class="border border-green-500/30 bg-green-500/10 px-2 py-1 text-[8px] uppercase tracking-[0.16em] text-green-300">CURRENT TERMINAL</span>`
                                        : ""
                                }
                            </div>
                            <p class="mt-2 text-xs text-slate-500">
                                ${escapeHtml(session.browser)} // ${escapeHtml(session.operatingSystem)}
                            </p>
                        </div>
                        <span class="text-[10px] uppercase tracking-[0.15em] text-slate-600">
                            ${escapeHtml(session.maskedIpAddress)}
                        </span>
                    </div>
                    <div class="mt-4 grid gap-3 text-[9px] uppercase tracking-[0.15em] sm:grid-cols-3">
                        ${renderSessionDate("CREATED", session.createdAtUtc)}
                        ${renderSessionDate("LAST SEEN", session.lastSeenAtUtc)}
                        ${renderSessionDate("EXPIRES", session.expiresAtUtc)}
                    </div>
                </article>
            `)
            .join("");
    }

    function renderSessionDate(label, value) {
        return `
            <div class="border border-slate-900 bg-black/60 p-3">
                <span class="block text-slate-700">${label}</span>
                <span class="mt-1 block text-slate-400">${formatDateTime(value)}</span>
            </div>
        `;
    }

    function renderFleet(data) {
        if (!data) {
            return;
        }

        const devices = Array.isArray(data.devices)
            ? data.devices
            : [];

        setText(
            "[data-fleet-count]",
            formatNumber(data.linkedDeviceCount)
        );

        const empty = root.querySelector("[data-fleet-empty]");
        toggleElement(empty, devices.length === 0);

        if (!fleetList) {
            return;
        }

        if (devices.length === 0) {
            fleetList.innerHTML = "";
            return;
        }

        fleetList.innerHTML = devices
            .map(device => {
                const active =
                    device.telemetryStatus?.toLowerCase() === "active";

                return `
                    <article class="border ${
                        active
                            ? "border-green-500/30 bg-green-500/[0.04]"
                            : "border-slate-800 bg-black"
                    } p-5">
                        <div class="flex items-start justify-between gap-4">
                            <div class="min-w-0">
                                <p class="text-[9px] uppercase tracking-[0.18em] text-slate-600">
                                    ${escapeHtml(device.installationId)}
                                </p>
                                <h3 class="mt-2 truncate font-display text-xl uppercase tracking-[0.08em] text-green-100">
                                    ${escapeHtml(device.displayName)}
                                </h3>
                                <p class="mt-2 text-xs text-slate-500">
                                    ${escapeHtml(device.deviceModel)}
                                </p>
                            </div>
                            <span class="shrink-0 border ${
                                active
                                    ? "border-green-500/30 bg-green-500/10 text-green-300"
                                    : "border-slate-700 bg-black text-slate-500"
                            } px-3 py-2 text-[9px] uppercase tracking-[0.16em]">
                                ${escapeHtml(device.telemetryStatus)}
                            </span>
                        </div>
                        <div class="mt-5 grid grid-cols-2 gap-3 text-[9px] uppercase tracking-[0.15em]">
                            ${renderFleetMetric("PLATFORM", device.platform)}
                            ${renderFleetMetric("BUILD", device.buildVersion)}
                            ${renderFleetMetric("SCENE", device.currentScene)}
                            ${renderFleetMetric("LAST UPDATE", formatDateTime(device.lastUpdateUtc))}
                        </div>
                        <button type="button"
                                data-unlink-device="${escapeAttribute(device.installationId)}"
                                data-device-name="${escapeAttribute(device.displayName)}"
                                class="mt-5 w-full border border-rose-500/25 bg-rose-500/[0.06] px-4 py-3 text-[10px] font-bold uppercase tracking-[0.18em] text-rose-300 transition hover:border-rose-400 hover:bg-rose-500/10 disabled:cursor-wait disabled:opacity-50">
                            UNLINK NODE
                        </button>
                    </article>
                `;
            })
            .join("");
    }

    function renderFleetMetric(label, value) {
        return `
            <div class="border border-slate-900 bg-black/60 p-3">
                <span class="block text-slate-700">${label}</span>
                <span class="mt-1 block truncate text-slate-400">${escapeHtml(value ?? "N/A")}</span>
            </div>
        `;
    }


    function normalizeAvatarKey(value) {
        const normalized = String(value ?? "").trim().toLowerCase();
        const allowed = new Set([
            "operator-01",
            "operator-02",
            "operator-03",
            "operator-04",
            "operator-05",
            "operator-06"
        ]);

        if (allowed.has(normalized)) {
            return normalized;
        }

        if (/^avatar-0[1-6]$/.test(normalized)) {
            return normalized.replace("avatar-", "operator-");
        }

        return "operator-01";
    }

    function normalizeTheme(value) {
        const normalized = String(value ?? "").trim().toLowerCase();

        if ([
            "phosphor-green",
            "amber-monitor",
            "cold-cyan",
            "monochrome-crt"
        ].includes(normalized)) {
            return normalized;
        }

        if (normalized === "amber") {
            return "amber-monitor";
        }

        if (normalized === "cyan") {
            return "cold-cyan";
        }

        return "phosphor-green";
    }

    function updateIdentityPreviewFromForm() {
        if (!identityForm) {
            return;
        }

        const formData = new FormData(identityForm);
        const alias = formData.get("Alias")?.toString().trim() || "PLAYER";
        const avatarKey =
            formData.get("AvatarKey")?.toString() ?? "operator-01";
        const theme =
            formData.get("Theme")?.toString() ?? "phosphor-green";
        const language =
            formData.get("PreferredLanguage")?.toString() ?? "en";

        setText("[data-player-alias]", alias);
        setText("[data-player-initials]", createInitials(alias));
        setText("[data-player-avatar-key]", avatarKey);
        setText("[data-player-theme]", theme);
        setText("[data-player-language]", language.toUpperCase());
    }

    function applyIdentityMutation(data) {
        if (!data) {
            return;
        }

        setText("[data-player-alias]", data.alias);
        setText("[data-player-initials]", createInitials(data.alias));
        setText("[data-player-avatar-key]", data.avatarKey);
        setText("[data-player-theme]", data.theme);
        setText(
            "[data-player-language]",
            data.preferredLanguage?.toUpperCase()
        );
    }

    async function requestJson(url, options = {}) {
        if (!url) {
            throw new Error(
                "A required profile endpoint is not configured."
            );
        }

        const requestOptions = {
            method: options.method ?? "GET",
            credentials: "same-origin",
            headers: {
                Accept: "application/json"
            }
        };

        if (
            requestOptions.method !== "GET" &&
            requestOptions.method !== "HEAD"
        ) {
            requestOptions.headers.RequestVerificationToken =
                antiforgeryToken;
        }

        if (options.body !== undefined) {
            requestOptions.headers["Content-Type"] =
                "application/json";
            requestOptions.body = JSON.stringify(options.body);
        }

        const response = await fetch(url, requestOptions);

        if (response.status === 401) {
            const returnUrl = encodeURIComponent(
                window.location.pathname + window.location.search
            );

            window.location.assign(
                `/Auth/Login?returnUrl=${returnUrl}`
            );

            throw new Error("Authentication is required.");
        }

        const contentType =
            response.headers.get("content-type") ?? "";

        const payload = contentType.includes("application/json")
            ? await response.json()
            : {
                message: await response.text()
            };

        if (!response.ok) {
            const error = new Error(
                payload.message ?? "The requested command failed."
            );
            error.payload = payload;
            error.status = response.status;
            throw error;
        }

        return payload;
    }

    function confirmAction({ title, message }) {
        const modal = root.querySelector("[data-confirm-modal]");

        if (!modal) {
            return Promise.resolve(window.confirm(message));
        }

        setText("[data-confirm-title]", title);
        setText("[data-confirm-message]", message);
        modal.classList.remove("hidden");
        modal.classList.add("flex");
        modal.querySelector("[data-confirm-accept]")?.focus();

        return new Promise(resolve => {
            modalResolver = resolve;
        });
    }

    function closeConfirmation(result) {
        const modal = root.querySelector("[data-confirm-modal]");

        if (!modal) {
            return;
        }

        modal.classList.add("hidden");
        modal.classList.remove("flex");

        if (modalResolver) {
            modalResolver(result);
            modalResolver = null;
        }
    }

    function showToast(message, type = "success") {
        const host = root.querySelector("[data-toast-host]");

        if (!host) {
            return;
        }

        const toast = document.createElement("div");
        toast.className = type === "error"
            ? "pointer-events-auto border border-rose-500/35 bg-black px-5 py-4 text-sm text-rose-200 shadow-[0_0_30px_rgba(244,63,94,0.12)]"
            : "pointer-events-auto border border-green-500/35 bg-black px-5 py-4 text-sm text-green-200 shadow-[0_0_30px_rgba(57,255,20,0.12)]";
        toast.setAttribute("role", "status");

        const prefix = type === "error"
            ? "[ COMMAND REJECTED ]"
            : "[ SYSTEM ACKNOWLEDGED ]";

        toast.innerHTML = `
            <div class="text-[9px] font-bold uppercase tracking-[0.18em]">
                ${prefix}
            </div>
            <div class="mt-2 leading-6">
                ${escapeHtml(message)}
            </div>
        `;

        host.appendChild(toast);

        window.setTimeout(() => {
            toast.remove();
        }, type === "error" ? 7000 : 4500);
    }

    function setBusy(element, busy) {
        if (!element) {
            return;
        }

        element.disabled = busy;
        element.setAttribute(
            "aria-busy",
            busy ? "true" : "false"
        );
    }

    function setText(selector, value) {
        const element = root.querySelector(selector);

        if (!element) {
            return;
        }

        element.textContent =
            value === null || value === undefined || value === ""
                ? "N/A"
                : value;
    }

    function setRadioValue(name, value) {
        if (!identityForm || !value) {
            return;
        }

        const input = Array.from(
            identityForm.querySelectorAll(`[name="${name}"]`)
        ).find(candidate => candidate.value === value);

        if (input) {
            input.checked = true;
        }
    }

    function toggleElement(element, show) {
        element?.classList.toggle("hidden", !show);
    }

    function normalizeSection(section) {
        return ["identity", "security", "fleet"].includes(section)
            ? section
            : "identity";
    }

    function isTypingTarget(target) {
        if (!(target instanceof HTMLElement)) {
            return false;
        }

        return target.matches(
            "input, textarea, select, [contenteditable='true']"
        );
    }

    function createInitials(alias) {
        const normalized = alias?.trim() || "CD";

        return normalized
            .split(/\s+/)
            .slice(0, 2)
            .map(part => part.charAt(0).toUpperCase())
            .join("")
            .slice(0, 2);
    }

    function formatNumber(value, maximumFractionDigits = 0) {
        return new Intl.NumberFormat(
            document.documentElement.lang || "en",
            {
                maximumFractionDigits
            }
        ).format(Number(value ?? 0));
    }

    function formatDateTime(value) {
        if (!value) {
            return "N/A";
        }

        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "N/A";
        }

        return new Intl.DateTimeFormat(
            document.documentElement.lang || "en",
            {
                dateStyle: "medium",
                timeStyle: "short"
            }
        ).format(date);
    }

    function getErrorMessage(error) {
        const validationErrors = error?.payload?.errors;

        if (Array.isArray(validationErrors)) {
            return validationErrors.join(" ");
        }

        if (
            validationErrors &&
            typeof validationErrors === "object"
        ) {
            return Object.values(validationErrors)
                .flat()
                .join(" ");
        }

        return error?.message ??
            "The operation could not be completed.";
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#039;");
    }

    function escapeAttribute(value) {
        return escapeHtml(value);
    }
})();
