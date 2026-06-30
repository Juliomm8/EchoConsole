(() => {
    "use strict";

    const root = document.querySelector(
        "[data-discord-relay-status]"
    );

    if (!root) {
        return;
    }

    const label = root.querySelector(
        "[data-discord-relay-label]"
    );

    const dot = root.querySelector(
        "[data-discord-relay-dot]"
    );

    const refreshIntervalMs = 60000;
    let timerId = null;

    void refresh();

    timerId = window.setInterval(
        () => {
            if (!document.hidden) {
                void refresh();
            }
        },
        refreshIntervalMs
    );

    document.addEventListener(
        "visibilitychange",
        () => {
            if (!document.hidden) {
                void refresh();
            }
        }
    );

    window.addEventListener(
        "pagehide",
        () => {
            window.clearInterval(timerId);
        },
        {
            once: true
        }
    );

    async function refresh() {
        const url = root.dataset.statusUrl;

        if (!url) {
            renderUnavailable();
            return;
        }

        try {
            const response = await fetch(
                url,
                {
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            if (!response.ok) {
                throw new Error(
                    String(response.status)
                );
            }

            const status = await response.json();

            if (status.operational) {
                renderState(
                    root.dataset.labelOperational
                    ?? "OPERATIONAL",
                    "operational"
                );

                return;
            }

            if (!status.enabled) {
                renderState(
                    root.dataset.labelDisabled
                    ?? "DISABLED",
                    "disabled"
                );

                return;
            }

            renderState(
                root.dataset.labelMisconfigured
                ?? "MISCONFIGURED",
                "error"
            );
        } catch {
            renderUnavailable();
        }
    }

    function renderUnavailable() {
        renderState(
            root.dataset.labelUnavailable
            ?? "UNAVAILABLE",
            "error"
        );
    }

    function renderState(text, state) {
        if (label) {
            label.textContent = text;
        }

        dot?.classList.remove(
            "bg-slate-600",
            "bg-green-400",
            "bg-amber-400",
            "bg-rose-400",
            "animate-signal-pulse"
        );

        label?.classList.remove(
            "text-slate-500",
            "text-green-400",
            "text-amber-400",
            "text-rose-400"
        );

        if (state === "operational") {
            dot?.classList.add(
                "bg-green-400",
                "animate-signal-pulse"
            );

            label?.classList.add(
                "text-green-400"
            );

            return;
        }

        if (state === "disabled") {
            dot?.classList.add(
                "bg-amber-400"
            );

            label?.classList.add(
                "text-amber-400"
            );

            return;
        }

        dot?.classList.add(
            "bg-rose-400"
        );

        label?.classList.add(
            "text-rose-400"
        );
    }
})();
