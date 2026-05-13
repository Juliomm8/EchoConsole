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

            dashboard_live_monitoring_title: "LIVE MONITORING",
            dashboard_brand: "ECHOCONSOLE WEB",
            dashboard_hero_title: "COSMIC DINER TELEMETRY CONTROL",
            dashboard_hero_subtitle: "Live monitoring of sessions, installations, and operational game telemetry.",
            dash_stream: "Telemetry Stream",
            dash_server_utc: "Server Time UTC",
            dashboard_stream_status_live: "LIVE",
            dashboard_stream_status_offline: "OFFLINE",

            dash_mod4: "Module 4",
            dash_signalr_ready: "SignalR Active",
            live_sessions_monitor_title: "LIVE SESSIONS MONITOR",

            table_installation_id: "INSTALLATION ID",
            table_current_scene: "CURRENT SCENE",
            table_game_state: "GAME STATE",
            table_current_phase: "CURRENT PHASE",
            table_last_heartbeat: "LAST HEARTBEAT",
            table_status: "STATUS",
            table_no_live_sessions: "No live sessions detected.",

            kpi_registered_installations_title: "REGISTERED INSTALLATIONS",
            kpi_active_sessions_title: "ACTIVE SESSIONS",
            kpi_average_duration_title: "AVERAGE ACTIVE SESSION DURATION",
            kpi_latest_heartbeat_title: "MOST RECENT HEARTBEAT",

            kpi_registered_installations_subtitle: "Persisted in SQL Server through the API",
            kpi_active_sessions_subtitle: "Current active sessions reported by the backend",
            kpi_average_duration_subtitle: "Calculated from current live sessions",
            kpi_latest_heartbeat_subtitle: "Freshest session heartbeat received",

            kpi_delta_real_data: "Real data",
            kpi_delta_live_now: "Live now",
            kpi_delta_unavailable: "Unavailable",

            status_active: "Active",
            status_ended: "Ended",
            status_expired: "Expired",
            status_unknown: "Unknown",

            rel_seconds_ago: "s ago",
            rel_minutes_ago: "m ago",
            rel_hours_ago: "h ago",
            rel_days_ago: "d ago",

            installations_page_title: "DEVICES & INSTALLATIONS",
            installations_module_badge: "Module 3",
            installations_hero_title: "MASTER HARDWARE INVENTORY",
            installations_hero_subtitle: "Centralized inventory of registered devices and hardware profiles captured from Cosmic Diner installations.",
            installations_total_label: "Total Devices",

            installations_search_label: "Search",
            installations_search_placeholder: "Search by Installation ID or Device Name",
            installations_search_button: "Search",
            installations_reset_button: "Reset",

            installations_table_badge: "Device Registry",
            installations_table_title: "REGISTERED INSTALLATIONS",
            installations_results_label: "Results:",

            installations_col_installation_id: "INSTALLATION ID",
            installations_col_device_name: "DEVICE NAME",
            installations_col_os: "OS",
            installations_col_processor: "PROCESSOR",
            installations_col_gpu: "GPU",
            installations_col_ram: "RAM",
            installations_col_last_update: "LAST UPDATE",

            installations_empty_state: "No installations found.",

            pagination_page: "Page",
            pagination_of: "of",
            pagination_previous: "Previous",
            pagination_next: "Next",

            // --- GAMES AND BUILDS ---
            builds_page_title: "GAMES AND BUILDS",
            builds_module_badge: "Module 2",
            builds_hero_title: "GAME BUILD REGISTRY",
            builds_hero_subtitle: "Centralized registry of monitored Cosmic Diner builds and release versions.",
            builds_total_label: "Total Builds",

            builds_search_label: "Search",
            builds_search_placeholder: "Search by Version Number or Engine Version",
            builds_search_button: "Search",
            builds_reset_button: "Reset",

            builds_table_badge: "Build Registry",
            builds_table_title: "REGISTERED BUILDS",
            builds_results_label: "Results:",

            builds_col_version_number: "VERSION",
            builds_col_release_notes: "RELEASE NOTES",
            builds_col_release_date: "RELEASE DATE",
            builds_col_is_active: "ACTIVE",
            builds_col_engine_version: "ENGINE VERSION",

            builds_empty_state: "No builds found.",

            build_status_active: "Active",
            build_status_inactive: "Inactive",

            // --- ALERTS AND REPORTS ---
            alerts_page_title: "ALERTS AND REPORTS",
            alerts_module_badge: "Module 5",
            alerts_hero_title: "SYSTEM ALERTS CENTER",
            alerts_hero_subtitle: "Centralized monitoring of automatic alerts generated by anomalies and critical telemetry events.",
            alerts_total_label: "Total Alerts",

            alerts_filter_severity: "Severity",
            alerts_filter_status: "Status",
            alerts_filter_all_severities: "All Severities",
            alerts_filter_all_statuses: "All Statuses",
            alerts_filter_button: "Filter",
            alerts_reset_button: "Reset",

            alerts_table_badge: "Alert Registry",
            alerts_table_title: "REGISTERED ALERTS",
            alerts_results_label: "Results:",

            alerts_col_date: "DATE",
            alerts_col_source: "SOURCE",
            alerts_col_message: "MESSAGE",
            alerts_col_severity: "SEVERITY",
            alerts_col_status: "STATUS",
            alerts_col_actions: "ACTIONS",

            alerts_empty_state: "No alerts found.",

            alerts_status_open: "Open",
            alerts_status_resolved: "Resolved",
            alerts_action_resolve: "Resolve",

            alert_severity_info: "Info",
            alert_severity_warning: "Warning",
            alert_severity_critical: "Critical",
            alert_severity_fatal: "Fatal"
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

            dashboard_live_monitoring_title: "MONITOREO EN VIVO",
            dashboard_brand: "ECHOCONSOLE WEB",
            dashboard_hero_title: "CONTROL DE TELEMETRÍA DE COSMIC DINER",
            dashboard_hero_subtitle: "Monitoreo en vivo de sesiones, instalaciones y telemetría operativa del videojuego.",
            dash_stream: "Flujo de Telemetría",
            dash_server_utc: "Hora del Servidor UTC",
            dashboard_stream_status_live: "EN VIVO",
            dashboard_stream_status_offline: "DESCONECTADO",

            dash_mod4: "Módulo 4",
            dash_signalr_ready: "SignalR Activo",
            live_sessions_monitor_title: "MONITOR DE SESIONES",

            table_installation_id: "ID DE INSTALACIÓN",
            table_current_scene: "ESCENA ACTUAL",
            table_game_state: "ESTADO DEL JUEGO",
            table_current_phase: "FASE ACTUAL",
            table_last_heartbeat: "ÚLTIMA SEÑAL",
            table_status: "ESTADO",
            table_no_live_sessions: "No se detectaron sesiones en vivo.",

            kpi_registered_installations_title: "INSTALACIONES REGISTRADAS",
            kpi_active_sessions_title: "SESIONES ACTIVAS",
            kpi_average_duration_title: "DURACIÓN PROMEDIO DE SESIÓN ACTIVA",
            kpi_latest_heartbeat_title: "SEÑAL MÁS RECIENTE",

            kpi_registered_installations_subtitle: "Persistidas en SQL Server a través de la API",
            kpi_active_sessions_subtitle: "Sesiones activas actuales reportadas por el backend",
            kpi_average_duration_subtitle: "Calculada a partir de las sesiones activas",
            kpi_latest_heartbeat_subtitle: "Última señal de sesión recibida",

            kpi_delta_real_data: "Dato real",
            kpi_delta_live_now: "En vivo ahora",
            kpi_delta_unavailable: "No disponible",

            status_active: "Activa",
            status_ended: "Finalizada",
            status_expired: "Expirada",
            status_unknown: "Desconocido",

            rel_seconds_ago: "s",
            rel_minutes_ago: "m",
            rel_hours_ago: "h",
            rel_days_ago: "d",

            installations_page_title: "DISPOSITIVOS E INSTALACIONES",
            installations_module_badge: "Módulo 3",
            installations_hero_title: "INVENTARIO MAESTRO DE HARDWARE",
            installations_hero_subtitle: "Inventario centralizado de dispositivos registrados y perfiles de hardware capturados desde las instalaciones de Cosmic Diner.",
            installations_total_label: "Total de Equipos",

            installations_search_label: "Buscar",
            installations_search_placeholder: "Buscar por ID de Instalación o Nombre del Dispositivo",
            installations_search_button: "Buscar",
            installations_reset_button: "Limpiar",

            installations_table_badge: "Registro de Dispositivos",
            installations_table_title: "INSTALACIONES REGISTRADAS",
            installations_results_label: "Resultados:",

            installations_col_installation_id: "ID DE INSTALACIÓN",
            installations_col_device_name: "NOMBRE DEL DISPOSITIVO",
            installations_col_os: "SISTEMA OPERATIVO",
            installations_col_processor: "PROCESADOR",
            installations_col_gpu: "GPU",
            installations_col_ram: "RAM",
            installations_col_last_update: "ÚLTIMA ACTUALIZACIÓN",

            installations_empty_state: "No se encontraron instalaciones.",

            pagination_page: "Página",
            pagination_of: "de",
            pagination_previous: "Anterior",
            pagination_next: "Siguiente",

            // --- GAMES AND BUILDS ---
            builds_page_title: "JUEGOS Y BUILDS",
            builds_module_badge: "Módulo 2",
            builds_hero_title: "REGISTRO DE BUILDS DEL JUEGO",
            builds_hero_subtitle: "Registro centralizado de builds monitoreadas y versiones liberadas de Cosmic Diner.",
            builds_total_label: "Total de Builds",

            builds_search_label: "Buscar",
            builds_search_placeholder: "Buscar por Número de Versión o Versión del Motor",
            builds_search_button: "Buscar",
            builds_reset_button: "Limpiar",

            builds_table_badge: "Registro de Builds",
            builds_table_title: "BUILDS REGISTRADAS",
            builds_results_label: "Resultados:",

            builds_col_version_number: "VERSIÓN",
            builds_col_release_notes: "NOTAS DE LANZAMIENTO",
            builds_col_release_date: "FECHA DE LANZAMIENTO",
            builds_col_is_active: "ACTIVA",
            builds_col_engine_version: "VERSIÓN DEL MOTOR",

            builds_empty_state: "No se encontraron builds.",

            build_status_active: "Activa",
            build_status_inactive: "Inactiva",

            // --- ALERTS AND REPORTS ---
            alerts_page_title: "ALERTAS Y REPORTES",
            alerts_module_badge: "Módulo 5",
            alerts_hero_title: "CENTRO DE ALERTAS DEL SISTEMA",
            alerts_hero_subtitle: "Monitoreo centralizado de alertas automáticas generadas por anomalías y eventos críticos de telemetría.",
            alerts_total_label: "Total de Alertas",

            alerts_filter_severity: "Severidad",
            alerts_filter_status: "Estado",
            alerts_filter_all_severities: "Todas las Severidades",
            alerts_filter_all_statuses: "Todos los Estados",
            alerts_filter_button: "Filtrar",
            alerts_reset_button: "Limpiar",

            alerts_table_badge: "Registro de Alertas",
            alerts_table_title: "ALERTAS REGISTRADAS",
            alerts_results_label: "Resultados:",

            alerts_col_date: "FECHA",
            alerts_col_source: "ORIGEN",
            alerts_col_message: "MENSAJE",
            alerts_col_severity: "SEVERIDAD",
            alerts_col_status: "ESTADO",
            alerts_col_actions: "ACCIONES",

            alerts_empty_state: "No se encontraron alertas.",

            alerts_status_open: "Abierta",
            alerts_status_resolved: "Resuelta",
            alerts_action_resolve: "Resolver",

            alert_severity_info: "Info",
            alert_severity_warning: "Advertencia",
            alert_severity_critical: "Crítica",
            alert_severity_fatal: "Fatal"
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
        return saved && translations[saved] ? saved : "en";
    }

    function t(key) {
        const lang = getLanguage();
        return translations[lang]?.[key] ?? key;
    }

    function setLanguage(lang) {
        if (!translations[lang]) {
            return;
        }

        localStorage.setItem(storageKey, lang);
        document.documentElement.lang = lang;
        applyTranslations(lang);
        updateToggleState(lang);

        window.dispatchEvent(new CustomEvent("echoConsole:languageChanged", {
            detail: { lang }
        }));
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

        const placeholderNodes = document.querySelectorAll("[data-i18n-placeholder]");
        placeholderNodes.forEach(node => {
            const key = node.getAttribute("data-i18n-placeholder");
            if (!key || !dict[key]) {
                return;
            }
            node.setAttribute("placeholder", dict[key]);
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

    window.echoConsoleI18n = {
        t,
        getLanguage,
        setLanguage,
        applyTranslations
    };
})();