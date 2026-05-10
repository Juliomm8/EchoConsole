(() => {
    const storageKey = "echoConsole.lang";

    const translations = {
        en: {
            sidebar_modules: "Modules",
            sidebar_subtitle: "Community telemetry platform",
            nav_live_monitoring: "Live Monitoring",
            nav_games_builds: "Games and Builds",
            nav_devices_installations: "Devices and Installations",
            nav_alerts_reports: "Alerts and Reports",
            nav_user_management: "User Management",
            sidebar_footer_tagline: "Eat. Die. Repeat.",
            sidebar_footer_version: "Version",
            sidebar_footer_region: "Region",

            topbar_station: "Telemetry Control Station",
            topbar_live: "Live",
            topbar_stream_label: "Telemetry stream active",
            topbar_server_time: "Server Time",
            topbar_language: "Language",
            topbar_admin_role: "Administrator",

            // Textos del Dashboard
            dash_subtitle: "Live monitoring of sessions, installations, and operational game telemetry.",
            dash_stream: "Telemetry Stream",
            dash_server_utc: "Server Time UTC",
            dash_mod4: "Module 4",
            dash_live_monitor: "Live Sessions Monitor",
            dash_signalr_ready: "SignalR Ready",

            table_id: "Installation ID",
            table_scene: "Current Scene",
            table_state: "Game State",
            table_phase: "Current Phase",
            table_heartbeat: "Last Heartbeat",
            table_status: "Status",

            // Textos dinámicos de los KPIs
            "Registered Installations": "Registered Installations",
            "Active Sessions": "Active Sessions",
            "Average Session Duration": "Average Session Duration",
            "Peak Concurrent Players": "Peak Concurrent Players",
            "Validated over real internet ingestion": "Validated over real internet ingestion",
            "Updated through SignalR": "Updated through SignalR",
            "Dummy data for Sprint 1": "Dummy data for Sprint 1",
            "Live now": "Live now"
        },
        es: {
            sidebar_modules: "Módulos",
            sidebar_subtitle: "Plataforma comunitaria de telemetría",
            nav_live_monitoring: "Monitoreo en Vivo",
            nav_games_builds: "Juegos y Builds",
            nav_devices_installations: "Dispositivos e Instalaciones",
            nav_alerts_reports: "Alertas y Reportes",
            nav_user_management: "Gestión de Usuarios",
            sidebar_footer_tagline: "Come. Muere. Repite.",
            sidebar_footer_version: "Versión",
            sidebar_footer_region: "Región",

            topbar_station: "Estación de Control de Telemetría",
            topbar_live: "En Vivo",
            topbar_stream_label: "Flujo de telemetría activo",
            topbar_server_time: "Hora del Servidor",
            topbar_language: "Idioma",
            topbar_admin_role: "Administrador",

            // Textos del Dashboard
            dash_subtitle: "Monitoreo en vivo de sesiones, instalaciones y telemetría operativa del videojuego.",
            dash_stream: "Flujo de Telemetría",
            dash_server_utc: "Hora del Servidor UTC",
            dash_mod4: "Módulo 4",
            dash_live_monitor: "Monitor de Sesiones",
            dash_signalr_ready: "SignalR Activo",

            table_id: "ID de Instalación",
            table_scene: "Escena Actual",
            table_state: "Estado del Juego",
            table_phase: "Fase Actual",
            table_heartbeat: "Última Señal",
            table_status: "Estado",

            // Textos dinámicos de los KPIs
            "Registered Installations": "Instalaciones Registradas",
            "Active Sessions": "Sesiones Activas",
            "Average Session Duration": "Duración Promedio",
            "Peak Concurrent Players": "Pico de Jugadores",
            "Validated over real internet ingestion": "Validado por ingesta real de internet",
            "Updated through SignalR": "Actualizado mediante SignalR",
            "Dummy data for Sprint 1": "Datos de prueba (Sprint 1)",
            "Live now": "En vivo ahora"
        }
    };

    const activeClasses = [
        "border-cyan-400/40",
        "bg-cyan-500/15",
        "text-cyan-200"
    ];

    const inactiveClasses = [
        "border-slate-700",
        "bg-transparent",
        "text-slate-400"
    ];

    function getLanguage() {
        const saved = localStorage.getItem(storageKey);

        if (saved && translations[saved]) {
            return saved;
        }

        return "en";
    }

    function setLanguage(lang) {
        if (!translations[lang]) {
            return;
        }

        localStorage.setItem(storageKey, lang);
        document.documentElement.lang = lang;
        applyTranslations(lang);
        updateToggleState(lang);
    }

    function applyTranslations(lang) {
        const dict = translations[lang];
        const nodes = document.querySelectorAll("[data-i18n]");

        nodes.forEach(node => {
            const key = node.getAttribute("data-i18n");

            if (!key || !dict[key]) {
                return;
            }

            node.textContent = dict[key];
        });
    }

    function updateToggleState(lang) {
        const buttons = document.querySelectorAll("[data-lang]");

        buttons.forEach(button => {
            const isActive = button.getAttribute("data-lang") === lang;

            button.classList.remove(...activeClasses, ...inactiveClasses);

            if (isActive) {
                button.classList.add(...activeClasses);
                button.setAttribute("aria-pressed", "true");
            } else {
                button.classList.add(...inactiveClasses);
                button.setAttribute("aria-pressed", "false");
            }
        });
    }

    function updateServerTime() {
        const target = document.getElementById("topbar-server-time-value");

        if (!target) {
            return;
        }

        const now = new Date();
        const formatted = now.toLocaleString("en-GB", {
            year: "numeric",
            month: "short",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false,
            timeZone: "UTC"
        });

        target.textContent = `${formatted} UTC`;
    }

    function bindEvents() {
        const buttons = document.querySelectorAll("[data-lang]");

        buttons.forEach(button => {
            button.addEventListener("click", () => {
                const lang = button.getAttribute("data-lang") || "en";
                setLanguage(lang);
            });
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        bindEvents();
        setLanguage(getLanguage());
        updateServerTime();
        setInterval(updateServerTime, 1000);
    });
})();