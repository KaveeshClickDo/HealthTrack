// Dashboard charts (the administrator's logins per day and appointments by status): each canvas carries its series in
// data-series, and charts.js draws it.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        if (window.HealthTrackCharts && HealthTrackCharts.ready()) {
            HealthTrackCharts.drawEmbedded();
        }
    });
})();
