// Progress charts (Views/Shared/_ProgressCharts.cshtml) for the client's and the trainer's progress pages.
// A canvas with data-url fetches its JSON (client page; changing the range re-fetches in place); a canvas with
// data-series reads the JSON the server embedded (trainer page; changing the range reloads the page).
// Marks follow one set of rules: one y-axis per chart, 2px lines, 8px points with a white ring, bars at most 24px wide,
// series colours in a fixed order, a grey dashed line for targets, and a legend whenever there are two series or more.
(function () {
    'use strict';

    // Categorical slots 1-3, checked for colour-blind separation; slot 3 is below 3:1 contrast, so every chart also
    // has a legend and a data table.
    var palette = ['#2a78d6', '#eb6834', '#1baf7a'];
    var reference = '#6c757d';
    var muted = '#6c757d';
    var gridColor = '#e9ecef';
    var surface = '#ffffff';

    var charts = new Map();
    var requests = {};

    document.addEventListener('DOMContentLoaded', function () {
        if (!window.Chart) {
            return;
        }

        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        Chart.defaults.color = muted;

        var canvases = Array.prototype.slice.call(document.querySelectorAll('canvas[data-chart]'));
        var form = document.getElementById('progress-range');
        var live = form && form.hasAttribute('data-live-range');

        canvases.filter(function (canvas) { return canvas.dataset.series; }).forEach(function (canvas) {
            draw(canvas, JSON.parse(canvas.dataset.series));
        });

        function query() {
            return new URLSearchParams(new FormData(form)).toString();
        }

        function refresh() {
            requests = {};
            var search = form ? query() : '';
            canvases.filter(function (canvas) { return canvas.dataset.url; }).forEach(function (canvas) {
                load(canvas.dataset.url + (search ? '?' + search : '')).then(function (data) {
                    draw(canvas, data[canvas.dataset.key]);
                    var text = form && form.querySelector('[data-range-text]');
                    if (text && data.period) {
                        text.textContent = data.period;
                    }
                }).catch(function () {
                    showEmpty(canvas, 'The chart could not be loaded. Reload the page to try again.');
                });
            });
        }

        if (form) {
            setUpRange(form, live ? function () {
                refresh();
                history.replaceState(null, '', '?' + query());
            } : function () {
                form.submit();
            });
        }

        refresh();
    });

    // The range picker: the custom dates show (and are sent) only for "Custom"; 4 weeks and 3 months apply at once.
    function setUpRange(form, apply) {
        var custom = form.querySelector('[data-custom-range]');

        form.addEventListener('change', function (event) {
            if (event.target.name !== 'range') {
                return;
            }

            var isCustom = event.target.value === 'custom';
            custom.hidden = !isCustom;
            custom.querySelectorAll('input').forEach(function (input) { input.disabled = !isCustom; });
            if (!isCustom) {
                apply();
            }
        });

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            apply();
        });
    }

    function load(url) {
        if (!requests[url]) {
            requests[url] = fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error(response.status);
                    }
                    return response.json();
                });
        }
        return requests[url];
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
        table(box.querySelector('[data-chart-table] .table-responsive'), series, decimals);
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
                            callback: function (value) { return compact(value); }
                        }
                    }
                }
            }
        };
    }

    // The same numbers as a table, for screen readers and anyone who can't tell the colours apart.
    function table(container, series, decimals) {
        var element = document.createElement('table');
        element.className = 'table table-sm mb-0';

        var head = element.createTHead().insertRow();
        appendCell(head, 'th', 'Date');
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
})();
