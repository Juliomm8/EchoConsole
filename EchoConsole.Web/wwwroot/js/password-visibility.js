(() => {
    "use strict";

    const toggles = document.querySelectorAll(
        "[data-password-toggle]"
    );

    for (const toggle of toggles) {
        const targetId =
            toggle.getAttribute("aria-controls");

        if (!targetId) {
            continue;
        }

        const input =
            document.getElementById(targetId);

        if (!(input instanceof HTMLInputElement)) {
            continue;
        }

        toggle.addEventListener("click", () => {
            const passwordIsVisible =
                input.type === "text";

            input.type =
                passwordIsVisible
                    ? "password"
                    : "text";

            toggle.setAttribute(
                "aria-pressed",
                passwordIsVisible
                    ? "false"
                    : "true"
            );

            const label =
                passwordIsVisible
                    ? toggle.dataset.showLabel
                    : toggle.dataset.hideLabel;

            const textNode =
                toggle.querySelector(
                    "[data-password-toggle-label]"
                );

            if (label) {
                toggle.setAttribute(
                    "aria-label",
                    label
                );
            }

            if (textNode && label) {
                textNode.textContent = label;
            }

            input.focus({
                preventScroll: true
            });

            const valueLength =
                input.value.length;

            input.setSelectionRange(
                valueLength,
                valueLength
            );
        });
    }
})();
