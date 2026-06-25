window.echoConsoleAnalyticsCharts = (() => {
    "use strict";

    const charts = [];
    let runtimeOptions = null;

    const palette = [
        "#22c55e",
        "#06b6d4",
        "#f59e0b",
        "#a855f7",
        "#ef4444",
        "#84cc16",
        "#3b82f6",
        "#f97316"
    ];

    function init(options) {
        runtimeOptions = {
            culture: options.culture || document.documentElement.lang || "en",
            labels: {
                events: options.labels?.events || "Events",
                eventTypes: options.labels?.eventTypes || "Event Types"
            }
        };

        destroyCharts();
        animateCounters();

        if (!window.Chart) {
            console.error("Chart.js is not available.");
            return;
        }

        const dataElement = document.getElementById(options.dataElementId);
        if (!dataElement) {
            return;
        }

        let payload;
        try {
            payload = JSON.parse(dataElement.textContent || "{}");
        } catch (error) {
            console.error("Session event analytics payload is invalid.", error);
            return;
        }

        configureDefaults();
        createTrendChart(options.trendCanvasId, payload.timeSeries);
        createEventTypeChart(options.eventTypeCanvasId, payload.eventTypes);
        createSceneChart(options.sceneCanvasId, payload.scenes);
    }

    function configureDefaults() {
        Chart.defaults.color = "#94a3b8";
        Chart.defaults.borderColor = "rgba(51, 65, 85, 0.55)";
        Chart.defaults.font.family = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace";
        Chart.defaults.animation.duration = 480;
        Chart.defaults.animation.easing = "easeOutQuart";
    }

    function createTrendChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !data) {
            return;
        }

        charts.push(new Chart(canvas, {
            type: "line",
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: runtimeOptions.labels.events,
                    data: data.values || [],
                    borderColor: "#22c55e",
                    backgroundColor: "rgba(34, 197, 94, 0.10)",
                    pointBackgroundColor: "#d1fae5",
                    pointBorderColor: "#020504",
                    pointRadius: 2,
                    pointHoverRadius: 5,
                    borderWidth: 2,
                    tension: 0.28,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                normalized: true,
                interaction: {
                    mode: "index",
                    intersect: false
                },
                scales: {
                    x: createAxisOptions(false),
                    y: createAxisOptions(true)
                },
                plugins: createPluginOptions(false)
            }
        }));
    }

    function createEventTypeChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !data) {
            return;
        }

        charts.push(new Chart(canvas, {
            type: "doughnut",
            data: {
                labels: data.labels || [],
                datasets: [{
                    data: data.values || [],
                    backgroundColor: palette,
                    borderColor: "#020504",
                    borderWidth: 2,
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "66%",
                plugins: createPluginOptions(true)
            }
        }));
    }

    function createSceneChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !data) {
            return;
        }

        charts.push(new Chart(canvas, {
            type: "bar",
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: runtimeOptions.labels.events,
                    data: data.values || [],
                    backgroundColor: "rgba(6, 182, 212, 0.46)",
                    borderColor: "#06b6d4",
                    borderWidth: 1,
                    borderRadius: 2,
                    barThickness: 12,
                    maxBarThickness: 14
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                normalized: true,
                indexAxis: "y",
                interaction: {
                    mode: "nearest",
                    intersect: false
                },
                scales: {
                    x: createAxisOptions(true),
                    y: createCategoryAxisOptions()
                },
                plugins: createPluginOptions(false)
            }
        }));
    }

    function createAxisOptions(beginAtZero) {
        return {
            beginAtZero,
            grid: {
                color: "rgba(51, 65, 85, 0.35)",
                drawBorder: false
            },
            ticks: {
                color: "#64748b",
                precision: 0,
                maxRotation: 0,
                autoSkip: true,
                maxTicksLimit: 10
            }
        };
    }

    function createCategoryAxisOptions() {
        return {
            grid: {
                display: false
            },
            ticks: {
                color: "#94a3b8",
                autoSkip: false,
                font: {
                    size: 9
                }
            }
        };
    }

    function createPluginOptions(showLegend) {
        return {
            legend: {
                display: showLegend,
                position: "bottom",
                labels: {
                    usePointStyle: true,
                    pointStyle: "rect",
                    boxWidth: 8,
                    boxHeight: 8,
                    padding: 10,
                    color: "#94a3b8",
                    font: {
                        size: 9
                    }
                }
            },
            tooltip: {
                backgroundColor: "rgba(2, 5, 4, 0.97)",
                borderColor: "rgba(34, 197, 94, 0.30)",
                borderWidth: 1,
                padding: 10,
                titleColor: "#dcfce7",
                bodyColor: "#cbd5e1",
                displayColors: true
            }
        };
    }

    function animateCounters() {
        const counters = document.querySelectorAll("[data-analytics-counter]");

        for (const counter of counters) {
            const target = Number(counter.dataset.analyticsCounter || 0);
            const duration = 520;
            const startedAt = performance.now();

            function update(now) {
                const progress = Math.min(1, (now - startedAt) / duration);
                const value = Math.floor(target * easeOut(progress));

                counter.textContent = new Intl.NumberFormat(
                    runtimeOptions?.culture || document.documentElement.lang || "en")
                    .format(value);

                if (progress < 1) {
                    window.requestAnimationFrame(update);
                }
            }

            window.requestAnimationFrame(update);
        }
    }

    function destroyCharts() {
        while (charts.length > 0) {
            charts.pop()?.destroy();
        }
    }

    function easeOut(value) {
        return 1 - Math.pow(1 - value, 3);
    }

    return { init };
})();
