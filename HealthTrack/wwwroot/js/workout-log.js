// Workout log form (today's workout and free workouts): adds and removes exercise rows, and keeps the input names
// numbered Entries[0..n-1] so MVC model binding sees every row. Validation is re-parsed after each change.
(function ($) {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('workout-log-form');
        if (!form) {
            return;
        }

        var body = form.querySelector('#entry-table tbody');
        var addButton = document.getElementById('add-entry');

        function rows() {
            return body.querySelectorAll('tr.entry-row');
        }

        function renumber() {
            rows().forEach(function (row, index) {
                row.querySelectorAll('[name], [data-valmsg-for]').forEach(function (element) {
                    ['name', 'data-valmsg-for'].forEach(function (attribute) {
                        var value = element.getAttribute(attribute);
                        if (value) {
                            element.setAttribute(attribute, value.replace(/^Entries\[\d+\]/, 'Entries[' + index + ']'));
                        }
                    });
                });
                row.querySelectorAll('[id], label[for]').forEach(function (element) {
                    ['id', 'for'].forEach(function (attribute) {
                        var value = element.getAttribute(attribute);
                        if (value) {
                            element.setAttribute(attribute, value.replace(/^Entries_\d+__/, 'Entries_' + index + '__'));
                        }
                    });
                });
            });

            // jQuery Validation caches the form's rules; drop them and read the renamed inputs again.
            if ($ && $.validator && $.validator.unobtrusive) {
                var $form = $(form);
                $form.removeData('validator').removeData('unobtrusiveValidation');
                $.validator.unobtrusive.parse($form);
            }
        }

        function clear(row) {
            row.querySelectorAll('input').forEach(function (input) {
                if (input.type === 'checkbox') {
                    input.checked = true;
                } else {
                    input.value = '';
                }
                input.classList.remove('input-validation-error');
            });
            row.querySelectorAll('select').forEach(function (select) {
                select.selectedIndex = 0;
                select.classList.remove('input-validation-error');
            });
            row.querySelectorAll('.field-validation-error').forEach(function (message) {
                message.textContent = '';
                message.classList.replace('field-validation-error', 'field-validation-valid');
            });
            row.querySelectorAll('[data-planned]').forEach(function (planned) {
                planned.remove();
            });
        }

        if (addButton) {
            addButton.addEventListener('click', function () {
                var all = rows();
                var copy = all[all.length - 1].cloneNode(true);
                clear(copy);
                body.appendChild(copy);
                renumber();
                var select = copy.querySelector('select');
                if (select) {
                    select.focus();
                }
            });
        }

        body.addEventListener('click', function (event) {
            var button = event.target.closest('[data-remove-row]');
            if (!button) {
                return;
            }

            var row = button.closest('tr.entry-row');
            // The last row is cleared rather than removed, so there is always one to copy.
            if (rows().length > 1) {
                row.remove();
                renumber();
            } else {
                clear(row);
            }
        });
    });
})(window.jQuery);
