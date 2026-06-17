window.echoConsoleAnalyticsCharts = (() => {
    const chartInstances = [];

    const palette = [
        "#22d3ee",
        "#e879f9",
        "#fbbf24",
        "#34d399",
        "#fb7185",
        "#818cf8",
        "#a3e635",
        "#f97316"
    ];

    function init(options) {
        if (!window.Chart) {
            console.error("Chart.js is not available.");
            return;
        }

        const dataElement = document.getElementById(options.dataElementId);

        if (!dataElement) {
            return;
        }

        const payload = JSON.parse(dataElement.textContent || "{}");

        configureDefaults();
        animateCounters();

        createTrendChart(
            options.trendCanvasId,
            payload.timeSeries);

        createEventTypeChart(
            options.eventTypeCanvasId,
            payload.eventTypes);

        createSceneChart(
            options.sceneCanvasId,
            payload.scenes);

        createBuildChart(
            options.buildCanvasId,
            payload.buildVersions);
    }

    function configureDefaults() {
        Chart.defaults.color = "#94a3b8";
        Chart.defaults.borderColor = "rgba(148, 163, 184, 0.12)";
        Chart.defaults.font.family = "Inter, sans-serif";
        Chart.defaults.animation.duration = 900;
        Chart.defaults.animation.easing = "easeOutQuart";
    }

    function createTrendChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);

        if (!canvas || !data) {
            return;
        }

        chartInstances.push(new Chart(canvas, {
            type: "line",
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: "Events",
                    data: data.values || [],
                    borderColor: "#22d3ee",
                    backgroundColor: "rgba(34, 211, 238, 0.15)",
                    pointBackgroundColor: "#e879f9",
                    pointBorderColor: "#070B12",
                    pointRadius: 4,
                    pointHoverRadius: 7,
                    borderWidth: 3,
                    tension: 0.32,
                    fill: true
                }]
            },
            options: createCartesianOptions(false)
        }));
    }

    function createEventTypeChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);

        if (!canvas || !data) {
            return;
        }

        chartInstances.push(new Chart(canvas, {
            type: "doughnut",
            data: {
                labels: data.labels || [],
                datasets: [{
                    data: data.values || [],
                    backgroundColor: palette,
                    borderColor: "#0f172a",
                    borderWidth: 3,
                    hoverOffset: 12
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "64%",
                plugins: createPluginOptions("Event Types")
            }
        }));
    }

    function createSceneChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);

        if (!canvas || !data) {
            return;
        }

        chartInstances.push(new Chart(canvas, {
            type: "bar",
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: "Events",
                    data: data.values || [],
                    backgroundColor: "rgba(232, 121, 249, 0.65)",
                    borderColor: "#e879f9",
                    borderWidth: 1,
                    borderRadius: 8
                }]
            },
            options: createCartesianOptions(true)
        }));
    }

    function createBuildChart(canvasId, data) {
        const canvas = document.getElementById(canvasId);

        if (!canvas || !data) {
            return;
        }

        chartInstances.push(new Chart(canvas, {
            type: "bar",
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: "Events",
                    data: data.values || [],
                    backgroundColor: "rgba(251, 191, 36, 0.65)",
                    borderColor: "#fbbf24",
                    borderWidth: 1,
                    borderRadius: 8
                }]
            },
            options: createCartesianOptions(false)
        }));
    }

    function createCartesianOptions(horizontal) {
        return {
            responsive: true,
            maintainAspectRatio: false,
            indexAxis: horizontal ? "y" : "x",
            interaction: {
                mode: "index",
                intersect: false
            },
            scales: {
                x: {
                    beginAtZero: true,
                    grid: {
                        color: "rgba(148, 163, 184, 0.08)"
                    },
                    ticks: {
                        precision: 0
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: "rgba(148, 163, 184, 0.08)"
                    },
                    ticks: {
                        precision: 0
                    }
                }
            },
            plugins: createPluginOptions()
        };
    }

    function createPluginOptions(title) {
        return {
            legend: {
                display: true,
                position: "bottom",
                labels: {
                    usePointStyle: true,
                    padding: 18
                }
            },
            title: {
                display: Boolean(title),
                text: title || "",
                color: "#e2e8f0",
                font: {
                    family: "Orbitron, sans-serif",
                    size: 13
                }
            },
            tooltip: {
                backgroundColor: "rgba(2, 6, 23, 0.96)",
                borderColor: "rgba(34, 211, 238, 0.25)",
                borderWidth: 1,
                padding: 12,
                displayColors: true
            }
        };
    }

    function animateCounters() {
        const counters = document.querySelectorAll("[data-analytics-counter]");

        for (const counter of counters) {
            const target = Number(counter.dataset.analyticsCounter || 0);
            const duration = 700;
            const startedAt = performance.now();

            function update(now) {
                const progress = Math.min(
                    1,
                    (now - startedAt) / duration);

                const value = Math.floor(
                    target * easeOut(progress));

                counter.textContent =
                    value.toLocaleString("en-US");

                if (progress < 1) {
                    requestAnimationFrame(update);
                }
            }

            requestAnimationFrame(update);
        }
    }

    function easeOut(value) {
        return 1 - Math.pow(1 - value, 3);
    }

    return {
        init
    };
})();