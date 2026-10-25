// Site-wide script, loaded on every page by _Layout. The notification bell polling is added here later.
(function () {
    'use strict';

    // Destructive actions confirm first: a form with data-confirm="..." asks before it is sent.
    // (A Content-Security-Policy rules out onclick attributes, so this one listener serves every page.)
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        var message = form.getAttribute('data-confirm');
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // Progress bars: data-progress-width="40" sets the width to 40%. Set through the style property rather than a
    // style attribute in the markup, which a Content-Security-Policy without 'unsafe-inline' styles would block.
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-progress-width]').forEach(function (bar) {
            var percent = Math.max(0, Math.min(100, Number(bar.getAttribute('data-progress-width')) || 0));
            bar.style.width = percent + '%';
        });
    });
})();
