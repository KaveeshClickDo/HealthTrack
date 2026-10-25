// Report pages: draws the report's chart from its data-series (through charts.js), and a button with data-print opens
// the browser's print dialog (a listener rather than an onclick attribute, which a Content-Security-Policy blocks).
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        if (window.HealthTrackCharts && HealthTrackCharts.ready()) {
            HealthTrackCharts.drawEmbedded();
        }

        document.querySelectorAll('[data-print]').forEach(function (button) {
            button.addEventListener('click', function () {
                window.print();
            });
        });
    });
})();
