(() => {
    "use strict";

    document.addEventListener(
        "click",
        event => {
            const toggle =
                event.target.closest(
                    "[data-password-toggle]"
                );

            if (!(toggle instanceof HTMLButtonElement)) {
                return;
            }

            event.preventDefault();

            const targetId =
                toggle.dataset.target ??
                toggle.getAttribute(
                    "aria-controls"
                );

            if (!targetId) {
                return;
            }

            const input =
                document.getElementById(
                    targetId
                );

            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            const willShowPassword =
                input.type === "password";

            input.type =
                willShowPassword
                    ? "text"
                    : "password";

            const label =
                willShowPassword
                    ? toggle.dataset.hideLabel
                    : toggle.dataset.showLabel;

            toggle.setAttribute(
                "aria-pressed",
                willShowPassword
                    ? "true"
                    : "false"
            );

            if (label) {
                toggle.setAttribute(
                    "aria-label",
                    label
                );

                const labelElement =
                    toggle.querySelector(
                        "[data-password-toggle-label]"
                    );

                if (labelElement) {
                    labelElement.textContent =
                        label;
                }
            }

            input.focus({
                preventScroll: true
            });

            const cursorPosition =
                input.value.length;

            input.setSelectionRange(
                cursorPosition,
                cursorPosition
            );
        }
    );
})();
