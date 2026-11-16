// Workout log form (today's workout and free workouts): adds and removes exercise rows, and keeps the input names
// numbered Entries[0..n-1] so MVC model binding sees every row. Validation is re-parsed after each change. Choosing an
// exercise shows its photo (each option carries it in data-photo).
(function ($) {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('workout-log-form');
        if (!form) {
            return;
        }

        var body = form.querySelector('[data-entry-list]');
        var addButton = document.getElementById('add-entry');

        function rows() {
            return body.querySelectorAll('.entry-row');
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
                // A free row's hidden Include stays true.
                if (input.type === 'hidden' && /\.Include$/.test(input.name)) {
                    return;
                }

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

            // A copy of a planned row becomes a free row: no planned name, and the exercise dropdown in sight.
            row.querySelectorAll('[data-planned]').forEach(function (planned) {
                planned.remove();
            });
            row.querySelectorAll('[data-free-label]').forEach(function (label) {
                label.removeAttribute('hidden');
            });
            row.querySelectorAll('[data-exercise-select]').forEach(function (select) {
                select.classList.remove('visually-hidden');
            });
            row.querySelectorAll('[data-thumb]').forEach(function (thumb) {
                thumb.removeAttribute('src');
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

        body.addEventListener('change', function (event) {
            var select = event.target.closest('[data-exercise-select]');
            if (!select) {
                return;
            }

            var thumb = select.closest('.entry-row').querySelector('[data-thumb]');
            var option = select.options[select.selectedIndex];
            var photo = option ? option.getAttribute('data-photo') : null;
            if (thumb && photo) {
                thumb.setAttribute('src', photo);
            } else if (thumb) {
                thumb.removeAttribute('src');
            }
        });

        body.addEventListener('click', function (event) {
            var button = event.target.closest('[data-remove-row]');
            if (!button) {
                return;
            }

            var row = button.closest('.entry-row');
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
