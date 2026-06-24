(() => {
    "use strict";

    const dialog = document.querySelector("[data-build-edit-dialog]");
    const editButtons = document.querySelectorAll("[data-build-edit]");
    const closeButtons = document.querySelectorAll("[data-build-edit-close]");
    const deleteForms = document.querySelectorAll("[data-build-delete-form]");
    const toggleForms = document.querySelectorAll("[data-build-toggle-form]");

    const setSubmittingState = form => {
        const button = form.querySelector("button[type='submit']");

        if (!button || button.disabled) {
            return;
        }

        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        button.classList.add("cursor-wait", "opacity-60");
    };

    toggleForms.forEach(form => {
        form.addEventListener("submit", () => {
            setSubmittingState(form);
        });
    });

    deleteForms.forEach(form => {
        form.addEventListener("submit", event => {
            const message = form.dataset.confirmMessage ?? "Delete this build?";

            if (!window.confirm(message)) {
                event.preventDefault();
                return;
            }

            setSubmittingState(form);
        });
    });

    if (!dialog) {
        return;
    }

    const fields = {
        id: dialog.querySelector("[data-edit-field='id']"),
        version: dialog.querySelector("[data-edit-field='version']"),
        engine: dialog.querySelector("[data-edit-field='engine']"),
        releaseDate: dialog.querySelector("[data-edit-field='release-date']"),
        releaseNotes: dialog.querySelector("[data-edit-field='release-notes']")
    };

    const populateDialog = button => {
        if (!fields.id ||
            !fields.version ||
            !fields.engine ||
            !fields.releaseDate ||
            !fields.releaseNotes) {
            return false;
        }

        fields.id.value = button.dataset.buildId ?? "";
        fields.version.value = button.dataset.buildVersion ?? "";
        fields.engine.value = button.dataset.buildEngine ?? "";
        fields.releaseDate.value = button.dataset.buildReleaseDate ?? "";
        fields.releaseNotes.value = button.dataset.buildReleaseNotes ?? "";

        return true;
    };

    editButtons.forEach(button => {
        button.addEventListener("click", () => {
            if (!populateDialog(button)) {
                return;
            }

            if (typeof dialog.showModal === "function") {
                dialog.showModal();
                fields.version?.focus();
            }
        });
    });

    closeButtons.forEach(button => {
        button.addEventListener("click", () => {
            dialog.close();
        });
    });

    dialog.addEventListener("click", event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });
})();
