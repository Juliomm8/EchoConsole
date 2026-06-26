window.echoConsoleNavigationShell = (() => {
    function init() {
        const dropdowns = Array.from(
            document.querySelectorAll("details[data-echo-dropdown]"));

        for (const dropdown of dropdowns) {
            dropdown.addEventListener("toggle", () => {
                if (!dropdown.open) {
                    return;
                }

                for (const other of dropdowns) {
                    if (other !== dropdown) {
                        other.open = false;
                    }
                }
            });
        }

        document.addEventListener("click", event => {
            for (const dropdown of dropdowns) {
                if (dropdown.open && !dropdown.contains(event.target)) {
                    dropdown.open = false;
                }
            }
        });

        document.addEventListener("keydown", event => {
            if (event.key !== "Escape") {
                return;
            }

            for (const dropdown of dropdowns) {
                dropdown.open = false;
            }
        });

        const sidebar = document.querySelector("[data-sidebar-shell]");

        if (sidebar) {
            sidebar.addEventListener("mouseleave", () => {
                if (!window.matchMedia("(min-width: 1280px)").matches) {
                    return;
                }

                for (const dropdown of sidebar.querySelectorAll("details")) {
                    dropdown.open = false;
                }
            });
        }
    }

    return {
        init
    };
})();

document.addEventListener("DOMContentLoaded", () => {
    window.echoConsoleNavigationShell.init();
});
