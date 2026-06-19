window.echoConsolePresentationMode = (() => {
    const storageKey = "echoConsole.presentationMode";

    function init() {
        const toggleButton = document.querySelector("[data-presentation-toggle]");
        const fullscreenButton = document.querySelector("[data-fullscreen-toggle]");

        if (toggleButton) {
            toggleButton.addEventListener("click", togglePresentationMode);
        }

        if (fullscreenButton) {
            fullscreenButton.addEventListener("click", toggleFullscreen);
        }

        document.addEventListener("fullscreenchange", updateFullscreenControl);
        updatePresentationControl();
        updateFullscreenControl();
    }

    function togglePresentationMode() {
        const nextState = !document.documentElement.classList.contains("presentation-mode");
        applyPresentationMode(nextState);
    }

    function applyPresentationMode(enabled) {
        document.documentElement.classList.toggle("presentation-mode", enabled);
        sessionStorage.setItem(storageKey, enabled ? "true" : "false");
        updatePresentationControl();
    }

    async function toggleFullscreen() {
        try {
            if (document.fullscreenElement) {
                await document.exitFullscreen();
                return;
            }

            await document.documentElement.requestFullscreen();
        } catch (error) {
            console.error("Fullscreen mode could not be changed.", error);
        }
    }

    function updatePresentationControl() {
        const button = document.querySelector("[data-presentation-toggle]");

        if (!button) {
            return;
        }

        const enabled = document.documentElement.classList.contains("presentation-mode");
        const label = enabled
            ? button.dataset.disableLabel
            : button.dataset.enableLabel;

        button.setAttribute("aria-pressed", enabled ? "true" : "false");
        button.title = label || "";

        const labelElement = button.querySelector("[data-presentation-toggle-label]");

        if (labelElement) {
            labelElement.textContent = label || "";
        }
    }

    function updateFullscreenControl() {
        const button = document.querySelector("[data-fullscreen-toggle]");

        if (!button) {
            return;
        }

        const enabled = Boolean(document.fullscreenElement);
        const label = enabled
            ? button.dataset.exitLabel
            : button.dataset.enterLabel;

        button.setAttribute("aria-pressed", enabled ? "true" : "false");
        button.title = label || "";

        const labelElement = button.querySelector("[data-fullscreen-toggle-label]");

        if (labelElement) {
            labelElement.textContent = label || "";
        }
    }

    return {
        init
    };
})();

document.addEventListener("DOMContentLoaded", () => {
    window.echoConsolePresentationMode.init();
});
