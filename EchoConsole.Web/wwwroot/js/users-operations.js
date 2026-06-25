window.echoConsoleUsersOperations = (() => {
    "use strict";

    let usersById = new Map();
    let drawer = null;
    let overlay = null;
    let previousFocus = null;

    function init() {
        drawer = document.querySelector("[data-user-drawer]");
        overlay = document.querySelector("[data-user-drawer-overlay]");

        const payload = document.getElementById("users-hardware-data");

        if (!drawer || !overlay || !payload) {
            return;
        }

        try {
            const users = JSON.parse(payload.textContent || "[]");
            usersById = new Map(
                users.map(user => [String(user.id), user]));
        } catch (error) {
            console.error("Users hardware payload could not be parsed.", error);
            return;
        }

        bindActions();
    }

    function bindActions() {
        document.addEventListener("click", event => {
            const auditButton = event.target.closest("[data-user-audit]");

            if (auditButton) {
                event.stopPropagation();
                openDrawer(auditButton.dataset.userId, auditButton);
                return;
            }

            const row = event.target.closest("[data-user-row]");

            if (row) {
                openDrawer(row.dataset.userId, row);
                return;
            }

            if (
                event.target.closest("[data-user-drawer-close]") ||
                event.target === overlay
            ) {
                closeDrawer();
            }
        });

        document.addEventListener("keydown", event => {
            const row = event.target.closest?.("[data-user-row]");

            if (row && (event.key === "Enter" || event.key === " ")) {
                event.preventDefault();
                openDrawer(row.dataset.userId, row);
                return;
            }

            if (event.key === "Escape" && drawer.getAttribute("aria-hidden") === "false") {
                closeDrawer();
            }
        });
    }

    function openDrawer(userId, trigger) {
        const user = usersById.get(String(userId));

        if (!user) {
            return;
        }

        previousFocus = trigger || document.activeElement;
        renderUser(user);

        drawer.classList.remove("translate-x-full");
        drawer.setAttribute("aria-hidden", "false");
        overlay.classList.remove("pointer-events-none", "opacity-0");
        overlay.classList.add("opacity-100");
        document.documentElement.classList.add("overflow-hidden");
        drawer.querySelector("[data-user-drawer-close]")?.focus();
    }

    function closeDrawer() {
        drawer.classList.add("translate-x-full");
        drawer.setAttribute("aria-hidden", "true");
        overlay.classList.add("pointer-events-none", "opacity-0");
        overlay.classList.remove("opacity-100");
        document.documentElement.classList.remove("overflow-hidden");
        previousFocus?.focus?.();
    }

    function renderUser(user) {
        setText("[data-drawer-user-name]", user.name);
        setText("[data-drawer-user-email]", user.email);
        setText("[data-drawer-user-role]", user.role);
        setText("[data-drawer-user-status]", user.status);

        const installations = Array.isArray(user.installations)
            ? user.installations
            : [];

        setText("[data-drawer-node-count]", String(installations.length));

        const container = document.querySelector("[data-drawer-stations]");
        const emptyState = document.querySelector("[data-drawer-empty]");

        if (!container || !emptyState) {
            return;
        }

        container.replaceChildren();

        if (installations.length === 0) {
            emptyState.classList.remove("hidden");
            return;
        }

        emptyState.classList.add("hidden");

        const fragment = document.createDocumentFragment();

        for (const installation of installations) {
            fragment.appendChild(createStationCard(installation));
        }

        container.appendChild(fragment);
    }

    function createStationCard(installation) {
        const card = document.createElement("article");
        card.className = "border border-slate-800 bg-[linear-gradient(180deg,rgba(8,13,16,0.96),rgba(1,3,4,0.99))]";

        const header = document.createElement("header");
        header.className = "flex items-start justify-between gap-4 border-b border-slate-800 px-4 py-3";

        const identity = document.createElement("div");
        const nodeLabel = document.createElement("p");
        nodeLabel.className = "text-[8px] uppercase tracking-[0.18em] text-slate-700";
        nodeLabel.textContent = "DEVICE_NODE";

        const nodeName = document.createElement("h3");
        nodeName.className = "mt-2 font-display text-sm uppercase tracking-[0.11em] text-cyan-400";
        nodeName.textContent = installation.deviceName || "UNKNOWN_NODE";

        identity.append(nodeLabel, nodeName);

        const status = document.createElement("span");
        status.className = "inline-flex min-w-[92px] items-center justify-center whitespace-nowrap border border-green-500/25 bg-green-500/10 px-3 py-1.5 text-[8px] font-semibold uppercase tracking-[0.14em] text-green-400";
        status.textContent = installation.adminStatus || "ACTIVE";

        header.append(identity, status);

        const grid = document.createElement("dl");
        grid.className = "grid gap-px bg-slate-800 sm:grid-cols-2";

        grid.append(
            createMetric("CPU", installation.cpu || "UNKNOWN_CPU"),
            createMetric("GPU", installation.gpu || "UNKNOWN_GPU"),
            createMetric("RAM", formatRam(installation.ramMb)),
            createMetric("OS / PLATFORM", `${installation.osVersion || "UNKNOWN_OS"} / ${installation.platform || "UNKNOWN_PLATFORM"}`),
            createMetric("BUILD", installation.buildVersion || "UNKNOWN_BUILD"),
            createMetric("LAST_TELEMETRY", formatUtc(installation.lastUpdateUtc))
        );

        card.append(header, grid);
        return card;
    }

    function createMetric(label, value) {
        const container = document.createElement("div");
        container.className = "min-w-0 bg-[#030706] px-4 py-3";

        const term = document.createElement("dt");
        term.className = "text-[8px] uppercase tracking-[0.16em] text-slate-700";
        term.textContent = label;

        const description = document.createElement("dd");
        description.className = "mt-2 break-words text-xs leading-5 text-slate-300";
        description.textContent = value;

        container.append(term, description);
        return container;
    }

    function formatRam(ramMb) {
        const numeric = Number(ramMb);

        if (!Number.isFinite(numeric) || numeric <= 0) {
            return "UNKNOWN_RAM";
        }

        const ramGb = Math.round((numeric / 1024) * 10) / 10;
        const display = Number.isInteger(ramGb)
            ? ramGb.toFixed(0)
            : ramGb.toFixed(1);

        return `${display} GB RAM`;
    }

    function formatUtc(value) {
        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "NO_TELEMETRY";
        }

        return date.toISOString().replace("T", " ").slice(0, 16) + " UTC";
    }

    function setText(selector, value) {
        const element = document.querySelector(selector);

        if (element) {
            element.textContent = value || "-";
        }
    }

    return { init };
})();

document.addEventListener("DOMContentLoaded", () => {
    window.echoConsoleUsersOperations.init();
});
