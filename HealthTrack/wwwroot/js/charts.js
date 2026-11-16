// Chart drawing shared by progress-charts.js, dashboard-charts.js and reports.js (load it after chart.umd.js).
// A chart lives in a card: [data-chart-card] holds the canvas (inside .chart-box), an empty-state paragraph
// [data-chart-empty] and a <details data-chart-table> that gets the same numbers as a table.
// Marks follow one set of rules: one y-axis per chart, 2px lines, 8px points ringed in the card colour, bars at most 24px wide,
// series colours in a fixed order, a grey dashed line for targets, and a legend whenever there are two series or more.
(function () {
    'use strict';

    // Categorical slots 1-3, checked for colour-blind separation; slot 3 is below 3:1 contrast, so every chart also
    // has a legend and a data table.
    var palette = ['#2a78d6', '#eb6834', '#1baf7a'];
    // The rest follow the dark theme's tokens (site.css): text-3 for labels and targets, grid lines, and the card
    // surface for the ring around each point.
    var reference = '#8c9c98';
    var muted = '#8c9c98';
    var gridColor = '#243030';
    var surface = '#101616';

    var charts = new Map();

    // Sets the fonts and colours once; false when Chart.js isn't on the page.
    function ready() {
        if (!window.Chart) {
            return false;
        }

        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        Chart.defaults.color = muted;
        return true;
    }

    // data-datasets="1,2" draws only those datasets of the series (heart rate and blood pressure share one series).
    function pick(series, canvas) {
        if (!series || !canvas.dataset.datasets) {
            return series;
        }

        var indexes = canvas.dataset.datasets.split(',').map(Number);
        return {
            labels: series.labels,
            datasets: series.datasets.filter(function (dataset, index) { return indexes.indexOf(index) >= 0; })
        };
    }

    function isEmpty(series) {
        return !series || series.labels.length === 0 || series.datasets.every(function (dataset) {
            return dataset.data.every(function (value) { return value === null; });
        });
    }

    function card(canvas) {
        return canvas.closest('[data-chart-card]');
    }

    function showEmpty(canvas, message) {
        var box = card(canvas);
        var empty = box.querySelector('[data-chart-empty]');
        if (message) {
            empty.textContent = message;
        }
        empty.hidden = false;
        canvas.parentElement.hidden = true;
        box.querySelector('[data-chart-table]').hidden = true;
    }

    // Draws (or redraws) one chart. data-decimals sets the number format; data-label-header names the table's first
    // column ("Date" unless the labels are something else, such as clients or exercises).
    function draw(canvas, raw) {
        var series = pick(raw, canvas);
        var decimals = Number(canvas.dataset.decimals || 0);

        if (charts.has(canvas)) {
            charts.get(canvas).destroy();
            charts.delete(canvas);
        }

        if (isEmpty(series)) {
            showEmpty(canvas);
            return;
        }

        var box = card(canvas);
        box.querySelector('[data-chart-empty]').hidden = true;
        canvas.parentElement.hidden = false;
        box.querySelector('[data-chart-table]').hidden = false;

        charts.set(canvas, new Chart(canvas, config(series, decimals)));
        table(box.querySelector('[data-chart-table] .table-responsive'), series, decimals, canvas.dataset.labelHeader || 'Date');
    }

    // Draws every chart whose series the server embedded in data-series.
    function drawEmbedded() {
        // Razor renders data-* attributes even when the value is null, so the client page carries data-series="";
        // parsing that would throw and stop the page's fetched charts from loading.
        document.querySelectorAll('canvas[data-chart][data-series]:not([data-series=""])').forEach(function (canvas) {
            draw(canvas, JSON.parse(canvas.dataset.series));
        });
    }

    function config(series, decimals) {
        var slot = 0;
        var hasBars = series.datasets.some(function (dataset) { return dataset.type === 'bar'; });
        var datasets = series.datasets.map(function (dataset) {
            // Colour follows the series' place in the chart, never its value; targets are a neutral reference.
            var color = dataset.dashed ? reference : palette[slot++ % palette.length];
            if (dataset.type === 'bar') {
                return {
                    type: 'bar',
                    label: dataset.label,
                    data: dataset.data,
                    backgroundColor: color,
                    maxBarThickness: 24,
                    borderRadius: 4,
                    borderSkipped: 'start',
                    order: 2
                };
            }

            return {
                type: 'line',
                label: dataset.label,
                data: dataset.data,
                borderColor: color,
                backgroundColor: color,
                borderWidth: 2,
                borderDash: dataset.dashed ? [6, 4] : [],
                borderCapStyle: 'round',
                borderJoinStyle: 'round',
                pointRadius: dataset.dashed ? 0 : 4,
                pointHoverRadius: dataset.dashed ? 0 : 5,
                pointBackgroundColor: color,
                pointBorderColor: surface,
                pointBorderWidth: 2,
                spanGaps: true,
                tension: 0,
                order: 1
            };
        });

        return {
            type: hasBars ? 'bar' : 'line',
            data: { labels: series.labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        display: datasets.length > 1,
                        position: 'bottom',
                        labels: { usePointStyle: true, boxWidth: 8, boxHeight: 8, padding: 16 }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + format(context.parsed.y, decimals);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        border: { color: gridColor },
                        ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 6 }
                    },
                    y: {
                        beginAtZero: hasBars,
                        grid: { color: gridColor, lineWidth: 1 },
                        border: { display: false },
                        ticks: {
                            maxTicksLimit: 6,
                            precision: decimals === 0 ? 0 : undefined,
                            callback: function (value) { return compact(value); }
                        }
                    }
                }
            }
        };
    }

    // The same numbers as a table, for screen readers and anyone who can't tell the colours apart.
    function table(container, series, decimals, labelHeader) {
        var element = document.createElement('table');
        element.className = 'table table-sm mb-0';

        var head = element.createTHead().insertRow();
        appendCell(head, 'th', labelHeader);
        series.datasets.forEach(function (dataset) { appendCell(head, 'th', dataset.label); });

        var body = element.createTBody();
        series.labels.forEach(function (label, index) {
            var row = body.insertRow();
            appendCell(row, 'th', label).scope = 'row';
            series.datasets.forEach(function (dataset) {
                appendCell(row, 'td', format(dataset.data[index], decimals));
            });
        });

        container.replaceChildren(element);
    }

    function appendCell(row, tag, text) {
        var cell = document.createElement(tag);
        cell.textContent = text;
        row.appendChild(cell);
        return cell;
    }

    function format(value, decimals) {
        if (value === null || value === undefined) {
            return '–';
        }
        return Number(value).toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    }

    // Axis ticks: clean numbers, compact from 1,000 up (1K, 2K).
    function compact(value) {
        return Math.abs(value) >= 1000
            ? new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 }).format(value)
            : Number(value).toLocaleString(undefined, { maximumFractionDigits: 1 });
    }

    // Charts keep their size when the page is printed (reports' Print button).
    window.addEventListener('beforeprint', function () {
        charts.forEach(function (chart) { chart.resize(); });
    });

    window.HealthTrackCharts = { ready: ready, draw: draw, drawEmbedded: drawEmbedded, showEmpty: showEmpty };
})();
