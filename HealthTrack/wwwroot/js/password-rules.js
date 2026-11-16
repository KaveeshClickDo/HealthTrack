// Change password: ticks each password rule (the Identity options in Program.cs) as the new password meets it.
// Only a guide while typing; Identity checks the same rules on the server.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var input = document.querySelector('[data-password-input]');
        var list = document.querySelector('[data-password-rules]');
        if (!input || !list) {
            return;
        }

        var rules = {
            length: function (value) { return value.length >= 8; },
            'case': function (value) { return /[a-z]/.test(value) && /[A-Z]/.test(value); },
            digit: function (value) { return /[0-9]/.test(value); },
            symbol: function (value) { return /[^a-zA-Z0-9]/.test(value); }
        };

        function update() {
            list.querySelectorAll('[data-rule]').forEach(function (item) {
                var rule = rules[item.getAttribute('data-rule')];
                item.classList.toggle('is-met', !!rule && rule(input.value));
            });
        }

        input.addEventListener('input', update);
        update();
    });
})();
