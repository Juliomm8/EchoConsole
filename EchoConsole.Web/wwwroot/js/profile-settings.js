(() => {
    "use strict";

    const root = document.querySelector(
        "[data-profile-settings]"
    );

    if (!root) {
        return;
    }

    const antiforgeryToken =
        root.querySelector(
            "[data-antiforgery-form] input[name='__RequestVerificationToken']"
        )?.value ?? "";

    const endpoints = {
        security:
            root.dataset.securityUrl,
        fleet:
            root.dataset.fleetUrl,
        updateIdentity:
            root.dataset.updateIdentityUrl,
        changePassword:
            root.dataset.changePasswordUrl,
        revokeSession:
            root.dataset.revokeSessionUrl,
        terminateSessions:
            root.dataset.terminateSessionsUrl,
        claimDevice:
            root.dataset.claimDeviceUrl,
        unlinkDevice:
            root.dataset.unlinkDeviceUrl,
        deleteProfile:
            root.dataset.deleteProfileUrl
    };

    const labels = {
        currentSession:
            root.dataset.labelCurrentSession ?? "CURRENT",
        revokeSession:
            root.dataset.labelRevokeSession ?? "REVOKE",
        noSessions:
            root.dataset.labelNoSessions ?? "NO ACTIVE SESSIONS",
        noDevices:
            root.dataset.labelNoDevices ?? "NO LINKED DEVICES",
        unlinkDevice:
            root.dataset.labelUnlinkDevice ?? "UNLINK DEVICE",
        active:
            root.dataset.labelActive ?? "ACTIVE",
        inactive:
            root.dataset.labelInactive ?? "INACTIVE",
        notAvailable:
            root.dataset.labelNotAvailable ?? "N/A",
        confirmRevoke:
            root.dataset.messageConfirmRevoke ??
            "Revoke this session?",
        confirmUnlink:
            root.dataset.messageConfirmUnlink ??
            "Unlink this device?",
        confirmTerminate:
            root.dataset.messageConfirmTerminate ??
            "Terminate every other session?",
        deleteFirst:
            root.dataset.messageDeleteFirst ??
            "Open the permanent deletion protocol?",
        deleteSecond:
            root.dataset.messageDeleteSecond ??
            "This action cannot be undone.",
        passwordMismatch:
            root.dataset.messagePasswordMismatch ??
            "Passwords do not match.",
        confirmPasswordChange:
            root.dataset.messageConfirmPasswordChange ??
            "Change the current password?",
        successPrefix:
            root.dataset.toastSuccessPrefix ??
            "[ SYSTEM ACKNOWLEDGED ]",
        errorPrefix:
            root.dataset.toastErrorPrefix ??
            "[ COMMAND REJECTED ]",
        genericError:
            root.dataset.messageGenericError ??
            "The operation failed."
    };

    const identityForm =
        root.querySelector(
            "[data-identity-form]"
        );

    const passwordForm =
        root.querySelector(
            "[data-password-form]"
        );

    const claimDeviceForm =
        root.querySelector(
            "[data-claim-device-form]"
        );

    const deleteProfileForm =
        root.querySelector(
            "[data-delete-profile-form]"
        );

    const sessionList =
        root.querySelector(
            "[data-session-list]"
        );

    const fleetList =
        root.querySelector(
            "[data-fleet-list]"
        );

    let securityLoaded = false;
    let fleetLoaded = false;

    initialize();

    function initialize() {
        bindAnchorNavigation();
        bindLazyLoading();
        bindIdentityForm();
        bindPasswordForm();
        bindSessionActions();
        bindDeviceActions();
        bindAvatarModal();
        bindDeleteProfile();
        syncStickyOffset();

        window.addEventListener(
            "resize",
            () => {
                window.requestAnimationFrame(
                    syncStickyOffset
                );
            }
        );
    }

    function bindAnchorNavigation() {
        const anchors = Array.from(
            root.querySelectorAll(
                "[data-settings-anchor]"
            )
        );

        const sections = Array.from(
            root.querySelectorAll(
                "[data-settings-section]"
            )
        );

        for (const anchor of anchors) {
            anchor.addEventListener(
                "click",
                event => {
                    event.preventDefault();

                    const sectionName =
                        anchor.dataset.settingsAnchor;

                    const target =
                        root.querySelector(
                            `[data-settings-section="${CSS.escape(sectionName)}"]`
                        );

                    target?.scrollIntoView({
                        behavior:
                            prefersReducedMotion()
                                ? "auto"
                                : "smooth",
                        block: "start"
                    });

                    window.history.replaceState(
                        null,
                        "",
                        `#settings-${sectionName}`
                    );
                }
            );
        }

        if ("IntersectionObserver" in window) {
            const observer =
                new IntersectionObserver(
                    entries => {
                        const visible = entries
                            .filter(
                                entry =>
                                    entry.isIntersecting
                            )
                            .sort(
                                (left, right) =>
                                    right.intersectionRatio -
                                    left.intersectionRatio
                            )[0];

                        if (!visible) {
                            return;
                        }

                        const activeSection =
                            visible.target
                                .dataset.settingsSection;

                        for (const anchor of anchors) {
                            const active =
                                anchor.dataset.settingsAnchor ===
                                activeSection;

                            anchor.classList.toggle(
                                "border-green-500/30",
                                active
                            );

                            anchor.classList.toggle(
                                "bg-green-500/10",
                                active
                            );

                            anchor.classList.toggle(
                                "text-green-200",
                                active
                            );

                            anchor.classList.toggle(
                                "border-slate-800",
                                !active
                            );

                            anchor.classList.toggle(
                                "bg-black",
                                !active
                            );

                            anchor.classList.toggle(
                                "text-slate-400",
                                !active
                            );
                        }
                    },
                    {
                        rootMargin:
                            "-24% 0px -60% 0px",
                        threshold:
                            [0.05, 0.2, 0.5]
                    }
                );

            for (const section of sections) {
                observer.observe(section);
            }
        }
    }

    function bindLazyLoading() {
        const securitySection =
            root.querySelector(
                '[data-settings-section="security"]'
            );

        const devicesSection =
            root.querySelector(
                '[data-settings-section="devices"]'
            );

        if (!("IntersectionObserver" in window)) {
            void loadSecurity();
            void loadFleet();
            return;
        }

        const observer =
            new IntersectionObserver(
                entries => {
                    for (const entry of entries) {
                        if (!entry.isIntersecting) {
                            continue;
                        }

                        if (
                            entry.target ===
                            securitySection
                        ) {
                            void loadSecurity();
                        }

                        if (
                            entry.target ===
                            devicesSection
                        ) {
                            void loadFleet();
                        }

                        observer.unobserve(
                            entry.target
                        );
                    }
                },
                {
                    rootMargin: "360px 0px",
                    threshold: 0.01
                }
            );

        if (securitySection) {
            observer.observe(
                securitySection
            );
        }

        if (devicesSection) {
            observer.observe(
                devicesSection
            );
        }
    }

    function bindIdentityForm() {
        identityForm?.addEventListener(
            "submit",
            async event => {
                event.preventDefault();

                const formData =
                    new FormData(identityForm);

                const button =
                    identityForm.querySelector(
                        "[data-identity-submit]"
                    );

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.updateIdentity,
                            {
                                method: "POST",
                                body: {
                                    alias:
                                        formData
                                            .get("Alias")
                                            ?.toString()
                                            .trim() ?? "",
                                    avatarKey:
                                        formData
                                            .get("AvatarKey")
                                            ?.toString() ?? "",
                                    theme:
                                        formData
                                            .get("Theme")
                                            ?.toString() ?? "",
                                    preferredLanguage:
                                        formData
                                            .get("PreferredLanguage")
                                            ?.toString() ?? ""
                                }
                            }
                        );

                    setText(
                        "[data-settings-alias]",
                        response.data?.alias
                    );

                    applyTerminalTheme(
                        response.data?.theme
                    );

                    const avatarImageUrl =
                        response.data?.avatarImageUrl;

                    const selectedAvatar =
                        root.querySelector(
                            "[data-avatar-radio]:checked"
                        );

                    updateAvatarImages(
                        avatarImageUrl,
                        selectedAvatar?.dataset
                            .avatarNameValue
                    );

                    showToast(
                        response.message,
                        "success"
                    );

                    if (
                        response.data?.reloadRequired
                    ) {
                        window.setTimeout(
                            () => window.location.reload(),
                            450
                        );
                    }
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                } finally {
                    setBusy(button, false);
                }
            }
        );
    }

    function bindPasswordForm() {
        const feedback =
            root.querySelector(
                "[data-password-feedback]"
            );

        passwordForm?.addEventListener(
            "submit",
            async event => {
                event.preventDefault();

                setPasswordFeedback(
                    feedback,
                    "",
                    "neutral"
                );

                const formData =
                    new FormData(passwordForm);

                const newPassword =
                    formData
                        .get("NewPassword")
                        ?.toString() ?? "";

                const confirmPassword =
                    formData
                        .get("ConfirmPassword")
                        ?.toString() ?? "";

                if (
                    newPassword !==
                    confirmPassword
                ) {
                    setPasswordFeedback(
                        feedback,
                        labels.passwordMismatch,
                        "error"
                    );

                    showToast(
                        labels.passwordMismatch,
                        "error"
                    );
                    return;
                }

                if (
                    !window.confirm(
                        labels.confirmPasswordChange
                    )
                ) {
                    return;
                }

                const button =
                    passwordForm.querySelector(
                        "[data-password-submit]"
                    );

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.changePassword,
                            {
                                method: "POST",
                                body: {
                                    currentPassword:
                                        formData
                                            .get("CurrentPassword")
                                            ?.toString() ?? "",
                                    newPassword,
                                    confirmPassword
                                }
                            }
                        );

                    passwordForm.reset();
                    securityLoaded = false;
                    await loadSecurity();

                    setPasswordFeedback(
                        feedback,
                        response.message,
                        "success"
                    );

                    showToast(
                        response.message,
                        "success"
                    );
                } catch (error) {
                    const message =
                        getErrorMessage(error);

                    setPasswordFeedback(
                        feedback,
                        message,
                        "error"
                    );

                    showToast(
                        message,
                        "error"
                    );
                } finally {
                    setBusy(button, false);
                }
            }
        );

        root.querySelector(
            "[data-terminate-sessions]"
        )?.addEventListener(
            "click",
            async event => {
                if (
                    !window.confirm(
                        labels.confirmTerminate
                    )
                ) {
                    return;
                }

                const button =
                    event.currentTarget;

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.terminateSessions,
                            {
                                method: "POST"
                            }
                        );

                    securityLoaded = false;
                    await loadSecurity();

                    showToast(
                        response.message,
                        "success"
                    );
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                } finally {
                    setBusy(button, false);
                }
            }
        );
    }

    function bindSessionActions() {
        sessionList?.addEventListener(
            "click",
            async event => {
                const button =
                    event.target.closest(
                        "[data-revoke-session]"
                    );

                if (!button) {
                    return;
                }

                if (
                    !window.confirm(
                        labels.confirmRevoke
                    )
                ) {
                    return;
                }

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.revokeSession,
                            {
                                method: "POST",
                                body: {
                                    sessionId:
                                        Number(
                                            button.dataset
                                                .revokeSession
                                        )
                                }
                            }
                        );

                    securityLoaded = false;
                    await loadSecurity();

                    showToast(
                        response.message,
                        "success"
                    );
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                    setBusy(button, false);
                }
            }
        );
    }

    function bindDeviceActions() {
        claimDeviceForm?.addEventListener(
            "submit",
            async event => {
                event.preventDefault();

                const formData =
                    new FormData(
                        claimDeviceForm
                    );

                const button =
                    claimDeviceForm.querySelector(
                        "[data-claim-device-submit]"
                    );

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.claimDevice,
                            {
                                method: "POST",
                                body: {
                                    installationId:
                                        formData
                                            .get("InstallationId")
                                            ?.toString()
                                            .trim() ?? ""
                                }
                            }
                        );

                    claimDeviceForm.reset();
                    fleetLoaded = false;
                    await loadFleet();

                    showToast(
                        response.message,
                        "success"
                    );
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                } finally {
                    setBusy(button, false);
                }
            }
        );

        fleetList?.addEventListener(
            "click",
            async event => {
                const button =
                    event.target.closest(
                        "[data-unlink-device]"
                    );

                if (!button) {
                    return;
                }

                if (
                    !window.confirm(
                        labels.confirmUnlink
                    )
                ) {
                    return;
                }

                setBusy(button, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.unlinkDevice,
                            {
                                method: "POST",
                                body: {
                                    installationId:
                                        button.dataset
                                            .unlinkDevice
                                }
                            }
                        );

                    fleetLoaded = false;
                    await loadFleet();

                    showToast(
                        response.message,
                        "success"
                    );
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                    setBusy(button, false);
                }
            }
        );
    }

    function bindAvatarModal() {
        const modal =
            root.querySelector(
                "[data-avatar-modal]"
            );

        const openButton =
            root.querySelector(
                "[data-open-avatar-modal]"
            );

        const closeButton =
            root.querySelector(
                "[data-close-avatar-modal]"
            );

        const avatarRadios =
            Array.from(
                root.querySelectorAll(
                    "[data-avatar-radio]"
                )
            );

        const avatarCards =
            Array.from(
                root.querySelectorAll(
                    "[data-avatar-card]"
                )
            );

        openButton?.addEventListener(
            "click",
            () => {
                modal?.classList.remove(
                    "hidden"
                );

                modal?.classList.add("flex");
            }
        );

        closeButton?.addEventListener(
            "click",
            closeAvatarModal
        );

        modal?.addEventListener(
            "click",
            event => {
                if (event.target === modal) {
                    closeAvatarModal();
                }
            }
        );

        for (const radio of avatarRadios) {
            radio.addEventListener(
                "change",
                () => {
                    if (!radio.checked) {
                        return;
                    }

                    setText(
                        "[data-avatar-name]",
                        radio.dataset
                            .avatarNameValue
                    );

                    updateAvatarImages(
                        radio.dataset.avatarImage,
                        radio.dataset.avatarNameValue
                    );

                    closeAvatarModal();
                }
            );
        }

        for (const card of avatarCards) {
            card.addEventListener(
                "click",
                event => {
                    if (
                        event.target.closest(
                            "[data-avatar-radio]"
                        )
                    ) {
                        return;
                    }

                    event.preventDefault();

                    const radio =
                        card.querySelector(
                            "[data-avatar-radio]"
                        );

                    if (!radio) {
                        return;
                    }

                    radio.checked = true;

                    radio.dispatchEvent(
                        new Event(
                            "change",
                            {
                                bubbles: true
                            }
                        )
                    );
                }
            );
        }

        function closeAvatarModal() {
            modal?.classList.add("hidden");
            modal?.classList.remove("flex");
        }
    }

    function bindDeleteProfile() {
        const modal =
            root.querySelector(
                "[data-delete-modal]"
            );

        const openButton =
            root.querySelector(
                "[data-open-delete-modal]"
            );

        const closeButton =
            root.querySelector(
                "[data-close-delete-modal]"
            );

        const acknowledgement =
            root.querySelector(
                "[data-delete-acknowledgement]"
            );

        const submitButton =
            root.querySelector(
                "[data-delete-profile-submit]"
            );

        openButton?.addEventListener(
            "click",
            () => {
                if (
                    !window.confirm(
                        labels.deleteFirst
                    )
                ) {
                    return;
                }

                modal?.classList.remove(
                    "hidden"
                );

                modal?.classList.add("flex");
            }
        );

        closeButton?.addEventListener(
            "click",
            closeDeleteModal
        );

        acknowledgement?.addEventListener(
            "change",
            () => {
                if (submitButton) {
                    submitButton.disabled =
                        !acknowledgement.checked;
                }
            }
        );

        deleteProfileForm?.addEventListener(
            "submit",
            async event => {
                event.preventDefault();

                if (
                    !window.confirm(
                        labels.deleteSecond
                    )
                ) {
                    return;
                }

                const formData =
                    new FormData(
                        deleteProfileForm
                    );

                setBusy(submitButton, true);

                try {
                    const response =
                        await requestJson(
                            endpoints.deleteProfile,
                            {
                                method: "POST",
                                body: {
                                    password:
                                        formData
                                            .get("Password")
                                            ?.toString() ?? "",
                                    confirmationText:
                                        formData
                                            .get("ConfirmationText")
                                            ?.toString() ?? ""
                                }
                            }
                        );

                    window.location.assign(
                        response.redirectUrl ?? "/"
                    );
                } catch (error) {
                    showToast(
                        getErrorMessage(error),
                        "error"
                    );
                    setBusy(
                        submitButton,
                        false
                    );
                }
            }
        );

        function closeDeleteModal() {
            modal?.classList.add("hidden");
            modal?.classList.remove("flex");
            deleteProfileForm?.reset();

            if (submitButton) {
                submitButton.disabled = true;
            }
        }
    }

    async function loadSecurity() {
        if (securityLoaded) {
            return;
        }

        const loading =
            root.querySelector(
                "[data-security-loading]"
            );

        toggleElement(loading, true);

        try {
            const response =
                await requestJson(
                    endpoints.security
                );

            renderSecurity(
                response.data
            );

            securityLoaded = true;
        } catch (error) {
            showToast(
                getErrorMessage(error),
                "error"
            );
        } finally {
            toggleElement(
                loading,
                false
            );
        }
    }

    async function loadFleet() {
        if (fleetLoaded) {
            return;
        }

        const loading =
            root.querySelector(
                "[data-fleet-loading]"
            );

        toggleElement(loading, true);

        try {
            const response =
                await requestJson(
                    endpoints.fleet
                );

            renderFleet(
                response.data
            );

            fleetLoaded = true;
        } catch (error) {
            showToast(
                getErrorMessage(error),
                "error"
            );
        } finally {
            toggleElement(
                loading,
                false
            );
        }
    }

    function renderSecurity(data) {
        const passwordUnavailable =
            root.querySelector(
                "[data-password-unavailable]"
            );

        const hasLocalPassword =
            Boolean(
                data?.hasLocalPassword
            );

        passwordUnavailable?.classList.toggle(
            "hidden",
            hasLocalPassword
        );

        if (passwordForm) {
            passwordForm.classList.remove(
                "hidden"
            );

            const passwordControls =
                passwordForm.querySelectorAll(
                    [
                        "input[type='password']",
                        "input[type='text'][autocomplete='current-password']",
                        "input[type='text'][autocomplete='new-password']",
                        "[data-password-toggle]",
                        "[data-password-submit]"
                    ].join(",")
                );

            for (
                const control of
                passwordControls
            ) {
                control.disabled =
                    !hasLocalPassword;
            }
        }

        const sessions =
            Array.isArray(
                data?.activeSessions
            )
                ? data.activeSessions
                : [];

        if (!sessionList) {
            return;
        }

        if (sessions.length === 0) {
            sessionList.innerHTML =
                `<div class="border border-slate-800 bg-black p-8 text-center text-xs uppercase tracking-[0.18em] text-slate-600">${escapeHtml(labels.noSessions)}</div>`;

            return;
        }

        sessionList.innerHTML = sessions
            .map(session => `
                <article class="border ${session.isCurrent
                    ? "border-green-500/35 bg-green-500/[0.05]"
                    : "border-slate-800 bg-black"} p-5">
                    <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                            <div class="flex flex-wrap items-center gap-2">
                                <h4 class="text-sm font-bold uppercase tracking-[0.12em] text-green-200">
                                    ${escapeHtml(session.deviceLabel)}
                                </h4>
                                ${session.isCurrent
                                    ? `<span class="border border-green-500/30 bg-green-500/10 px-2 py-1 text-[8px] uppercase tracking-[0.16em] text-green-300">${escapeHtml(labels.currentSession)}</span>`
                                    : ""}
                            </div>
                            <p class="mt-2 text-xs text-slate-500">
                                ${escapeHtml(session.browser)} // ${escapeHtml(session.operatingSystem)}
                            </p>
                            <p class="mt-2 text-[10px] uppercase tracking-[0.14em] text-slate-600">
                                ${escapeHtml(session.maskedIpAddress)} // ${escapeHtml(formatDateTime(session.lastSeenAtUtc))}
                            </p>
                        </div>
                        ${session.isCurrent
                            ? ""
                            : `<button type="button"
                                       data-revoke-session="${Number(session.id)}"
                                       class="border border-rose-500/25 bg-rose-500/[0.06] px-3 py-2 text-[9px] font-bold uppercase tracking-[0.14em] text-rose-300 hover:border-rose-400 disabled:opacity-40">
                                    ${escapeHtml(labels.revokeSession)}
                               </button>`}
                    </div>
                </article>
            `)
            .join("");
    }

    function renderFleet(data) {
        const devices =
            Array.isArray(data?.devices)
                ? data.devices
                : [];

        if (!fleetList) {
            return;
        }

        if (devices.length === 0) {
            fleetList.innerHTML =
                `<div class="border border-slate-800 bg-black p-10 text-center text-xs uppercase tracking-[0.18em] text-slate-600 xl:col-span-2">${escapeHtml(labels.noDevices)}</div>`;

            return;
        }

        fleetList.innerHTML = devices
            .map(device => {
                const active =
                    String(
                        device.telemetryStatus ??
                        ""
                    ).toLowerCase() ===
                    "active";

                return `
                    <article class="border ${active
                        ? "border-green-500/30 bg-green-500/[0.04]"
                        : "border-slate-800 bg-black"} p-5">
                        <div class="flex items-start justify-between gap-4">
                            <div class="min-w-0">
                                <p class="truncate text-[9px] uppercase tracking-[0.16em] text-slate-600">
                                    ${escapeHtml(device.installationId)}
                                </p>
                                <h3 class="mt-2 truncate font-display text-xl uppercase tracking-[0.08em] text-green-100">
                                    ${escapeHtml(device.displayName)}
                                </h3>
                                <p class="mt-2 text-xs text-slate-500">
                                    ${escapeHtml(device.platform)} // ${escapeHtml(device.buildVersion)}
                                </p>
                            </div>
                            <span class="border ${active
                                ? "border-green-500/30 bg-green-500/10 text-green-300"
                                : "border-slate-700 bg-black text-slate-500"} px-3 py-2 text-[9px] uppercase tracking-[0.16em]">
                                ${escapeHtml(active ? labels.active : labels.inactive)}
                            </span>
                        </div>
                        <div class="mt-4 border border-slate-900 bg-black/60 p-3 text-[9px] uppercase tracking-[0.14em] text-slate-500">
                            ${escapeHtml(device.currentScene || labels.notAvailable)}
                        </div>
                        <button type="button"
                                data-unlink-device="${escapeAttribute(device.installationId)}"
                                class="mt-5 w-full border border-rose-500/25 bg-rose-500/[0.06] px-4 py-3 text-[10px] font-bold uppercase tracking-[0.18em] text-rose-300 transition hover:border-rose-400 disabled:opacity-40">
                            ${escapeHtml(labels.unlinkDevice)}
                        </button>
                    </article>
                `;
            })
            .join("");
    }

    function updateAvatarImages(
        imageUrl,
        altText
    ) {
        if (!imageUrl) {
            return;
        }

        const images =
            new Set([
                ...document.querySelectorAll(
                    "[data-user-avatar-image]"
                ),
                ...root.querySelectorAll(
                    "[data-avatar-preview-image]"
                )
            ]);

        for (const image of images) {
            image.src = imageUrl;

            if (altText) {
                image.alt = altText;
            }
        }
    }

    function applyTerminalTheme(theme) {
        const allowedThemes =
            new Set([
                "green",
                "amber",
                "cyan",
                "monochrome"
            ]);

        const normalizedTheme =
            allowedThemes.has(theme)
                ? theme
                : "green";

        document.documentElement.dataset
            .terminalTheme =
            normalizedTheme;

        document.body.dataset
            .terminalTheme =
            normalizedTheme;
    }

    async function requestJson(
        url,
        options = {}
    ) {
        if (!url) {
            throw new Error(
                "Endpoint is not configured."
            );
        }

        const method =
            options.method ?? "GET";

        const headers = {
            Accept: "application/json"
        };

        if (
            method !== "GET" &&
            method !== "HEAD"
        ) {
            headers.RequestVerificationToken =
                antiforgeryToken;
        }

        if (
            options.body !== undefined
        ) {
            headers["Content-Type"] =
                "application/json";
        }

        const response = await fetch(
            url,
            {
                method,
                credentials: "same-origin",
                cache: "no-store",
                headers,
                body:
                    options.body === undefined
                        ? undefined
                        : JSON.stringify(
                            options.body
                        )
            }
        );

        const contentType =
            response.headers.get(
                "content-type"
            ) ?? "";

        const payload =
            contentType.includes(
                "application/json"
            )
                ? await response.json()
                : {
                    message:
                        await response.text()
                };

        if (!response.ok) {
            const error = new Error(
                payload.message ??
                labels.genericError
            );

            error.payload = payload;
            throw error;
        }

        return payload;
    }

    function showToast(
        message,
        type
    ) {
        const host =
            root.querySelector(
                "[data-settings-toast-host]"
            );

        if (!host) {
            return;
        }

        const toast =
            document.createElement("div");

        toast.className = type === "error"
            ? "pointer-events-auto border border-rose-500/35 bg-black px-5 py-4 text-sm text-rose-200 shadow-[0_0_30px_rgba(244,63,94,0.12)]"
            : "pointer-events-auto border border-green-500/35 bg-black px-5 py-4 text-sm text-green-200 shadow-[0_0_30px_rgba(57,255,20,0.12)]";

        toast.innerHTML = `
            <div class="text-[9px] font-bold uppercase tracking-[0.18em]">
                ${escapeHtml(
                    type === "error"
                        ? labels.errorPrefix
                        : labels.successPrefix
                )}
            </div>
            <div class="mt-2 leading-6">
                ${escapeHtml(
                    message ?? ""
                )}
            </div>
        `;

        host.appendChild(toast);

        window.setTimeout(
            () => toast.remove(),
            type === "error"
                ? 7000
                : 4500
        );
    }

    function setPasswordFeedback(
        element,
        message,
        type
    ) {
        if (!element) {
            return;
        }

        const hasMessage =
            Boolean(message);

        element.textContent =
            message ?? "";

        element.classList.toggle(
            "hidden",
            !hasMessage
        );

        element.classList.toggle(
            "border-green-500/30",
            hasMessage &&
                type === "success"
        );

        element.classList.toggle(
            "bg-green-500/10",
            hasMessage &&
                type === "success"
        );

        element.classList.toggle(
            "text-green-200",
            hasMessage &&
                type === "success"
        );

        element.classList.toggle(
            "border-rose-500/30",
            hasMessage &&
                type === "error"
        );

        element.classList.toggle(
            "bg-rose-500/[0.06]",
            hasMessage &&
                type === "error"
        );

        element.classList.toggle(
            "text-rose-200",
            hasMessage &&
                type === "error"
        );
    }

    function getErrorMessage(error) {
        const errors =
            error?.payload?.errors;

        if (Array.isArray(errors)) {
            return errors.join(" ");
        }

        if (
            errors &&
            typeof errors === "object"
        ) {
            return Object.values(errors)
                .flat()
                .join(" ");
        }

        return error?.message ??
            labels.genericError;
    }

    function syncStickyOffset() {
        const anchorBar =
            root.querySelector(
                "[data-settings-anchor-bar]"
            );

        if (!anchorBar) {
            return;
        }

        const globalTopbar =
            Array.from(
                document.querySelectorAll(
                    "header.sticky"
                )
            ).find(
                header =>
                    !root.contains(header)
            );

        const top =
            globalTopbar
                ? Math.ceil(
                    globalTopbar
                        .getBoundingClientRect()
                        .height
                )
                : 0;

        anchorBar.style.top =
            `${top}px`;

        const offset =
            top +
            Math.ceil(
                anchorBar
                    .getBoundingClientRect()
                    .height
            ) +
            24;

        for (
            const section of
            root.querySelectorAll(
                "[data-settings-section]"
            )
        ) {
            section.style.scrollMarginTop =
                `${offset}px`;
        }
    }

    function prefersReducedMotion() {
        return window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        ).matches;
    }

    function formatDateTime(value) {
        if (!value) {
            return labels.notAvailable;
        }

        const date =
            new Date(value);

        if (
            Number.isNaN(
                date.getTime()
            )
        ) {
            return labels.notAvailable;
        }

        return new Intl.DateTimeFormat(
            document.documentElement.lang ||
            "en",
            {
                dateStyle: "medium",
                timeStyle: "short"
            }
        ).format(date);
    }

    function setText(selector, value) {
        const element =
            root.querySelector(selector);

        if (element) {
            element.textContent =
                value ?? "";
        }
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

    function toggleElement(
        element,
        show
    ) {
        element?.classList.toggle(
            "hidden",
            !show
        );
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
