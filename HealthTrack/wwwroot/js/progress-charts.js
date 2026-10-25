// Progress charts (Views/Shared/_ProgressCharts.cshtml) for the client's and the trainer's progress pages; drawing is in
// charts.js. A canvas with data-url fetches its JSON (client page; changing the range re-fetches in place); a canvas with
// data-series reads the JSON the server embedded (trainer page; changing the range reloads the page).
(function () {
    'use strict';

    var requests = {};

    document.addEventListener('DOMContentLoaded', function () {
        if (!window.HealthTrackCharts || !HealthTrackCharts.ready()) {
            return;
        }

        var canvases = Array.prototype.slice.call(document.querySelectorAll('canvas[data-chart][data-url]'));
        var form = document.getElementById('progress-range');
        var live = form && form.hasAttribute('data-live-range');

        HealthTrackCharts.drawEmbedded();

        function query() {
            return new URLSearchParams(new FormData(form)).toString();
        }

        function refresh() {
            requests = {};
            var search = form ? query() : '';
            canvases.forEach(function (canvas) {
                load(canvas.dataset.url + (search ? '?' + search : '')).then(function (data) {
                    HealthTrackCharts.draw(canvas, data[canvas.dataset.key]);
                    var text = form && form.querySelector('[data-range-text]');
                    if (text && data.period) {
                        text.textContent = data.period;
                    }
                }).catch(function () {
                    HealthTrackCharts.showEmpty(canvas, 'The chart could not be loaded. Reload the page to try again.');
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

    // Several charts share one JSON action (weight and BMI, the two vitals charts), so each URL is fetched once.
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
})();
