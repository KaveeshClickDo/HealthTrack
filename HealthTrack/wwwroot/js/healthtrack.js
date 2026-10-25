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
})();
