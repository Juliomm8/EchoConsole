(() => {
    "use strict";

    const root = document.querySelector(
        "[data-simulation-summary]"
    );

    if (!root) {
        return;
    }

    const targetInput = root.querySelector(
        "[data-simulation-summary-target]"
    );

    const startButton = root.querySelector(
        "[data-simulation-summary-start]"
    );

    const stopButton = root.querySelector(
        "[data-simulation-summary-stop]"
    );

    const openButton = root.querySelector(
        "[data-simulation-summary-open]"
    );

    const state = root.querySelector(
        "[data-simulation-summary-state]"
    );

    const stateLabel = root.querySelector(
        "[data-simulation-summary-state-label]"
    );

    const stateDot = root.querySelector(
        "[data-simulation-summary-dot]"
    );

    const count = root.querySelector(
        "[data-simulation-summary-count]"
    );

    const targetReadout = root.querySelector(
        "[data-simulation-summary-target-readout]"
    );

    const phase = root.querySelector(
        "[data-simulation-summary-phase]"
    );

    let pendingTimerId = null;

    startButton?.addEventListener(
        "click",
        () => {
            const target = normalizeTarget(
                targetInput?.value
            );

            setPending(true);

            dispatchCommand(
                "start",
                {
                    target
                }
            );
        }
    );

    stopButton?.addEventListener(
        "click",
        () => {
            const message =
                root.dataset.confirmStop;

            if (
                message &&
                !window.confirm(message)
            ) {
                return;
            }

            setPending(true);
            dispatchCommand("stop");
        }
    );

    openButton?.addEventListener(
        "click",
        () => {
            dispatchCommand("open");
        }
    );

    document.addEventListener(
        "echo-console:simulation-status",
        event => {
            if (!(event instanceof CustomEvent)) {
                return;
            }

            renderStatus(event.detail ?? {});
            setPending(false);
        }
    );

    dispatchCommand("refresh");

    function dispatchCommand(
        action,
        detail = {}
    ) {
        document.dispatchEvent(
            new CustomEvent(
                "echo-console:simulation-command",
                {
                    detail: {
                        action,
                        ...detail
                    }
                }
            )
        );
    }

    function normalizeTarget(value) {
        const parsed = Number.parseInt(
            String(value),
            10
        );

        return Number.isFinite(parsed)
            ? Math.min(
                250,
                Math.max(1, parsed)
            )
            : Number(
                root.dataset.defaultTarget
                ?? 40
            );
    }

    function renderStatus(status) {
        const running =
            status.isRunning === true;

        const activeSessions = Number(
            status.activeSimulatedSessions
            ?? 0
        );

        const targetSessions = Number(
            status.targetActiveSessions
            ?? 0
        );

        const currentPhase = String(
            status.phase
            ?? (running ? "ACTIVE" : "IDLE")
        );

        if (count) {
            count.textContent =
                String(activeSessions);
        }

        if (targetReadout) {
            targetReadout.textContent =
                String(targetSessions);
        }

        if (targetInput && running) {
            targetInput.value =
                String(Math.max(1, targetSessions));
        }

        if (phase) {
            phase.textContent = currentPhase;
        }

        if (stateLabel) {
            stateLabel.textContent = running
                ? root.dataset.stateActive ?? "ACTIVE"
                : root.dataset.stateIdle ?? "IDLE";
        }

        state?.classList.remove(
            "border-slate-700",
            "text-slate-500",
            "border-green-500/35",
            "text-green-300"
        );

        stateDot?.classList.remove(
            "bg-slate-600",
            "bg-green-400",
            "animate-signal-pulse"
        );

        if (running) {
            state?.classList.add(
                "border-green-500/35",
                "text-green-300"
            );

            stateDot?.classList.add(
                "bg-green-400",
                "animate-signal-pulse"
            );

            return;
        }

        state?.classList.add(
            "border-slate-700",
            "text-slate-500"
        );

        stateDot?.classList.add(
            "bg-slate-600"
        );
    }

    function setPending(value) {
        if (startButton) {
            startButton.disabled = value;
        }

        if (stopButton) {
            stopButton.disabled = value;
        }

        window.clearTimeout(pendingTimerId);

        if (value) {
            pendingTimerId = window.setTimeout(
                () => setPending(false),
                8000
            );
        }
    }
})();
