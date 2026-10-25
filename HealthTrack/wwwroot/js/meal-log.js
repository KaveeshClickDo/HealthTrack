// Meal log form: adds and removes food rows and keeps the input names numbered Items[0..n-1] so MVC model binding
// sees every row; filters the food lists from the search box; and shows a live kcal estimate (servings × kcal per
// serving from the option's data-kcal). The saved values are always worked out on the server.
(function ($) {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('meal-log-form');
        if (!form) {
            return;
        }

        var body = form.querySelector('#item-table tbody');
        var total = form.querySelector('[data-meal-total]');
        var search = document.getElementById('food-search');
        var addButton = document.getElementById('add-item');

        function rows() {
            return body.querySelectorAll('tr.item-row');
        }

        function renumber() {
            rows().forEach(function (row, index) {
                row.querySelectorAll('[name], [data-valmsg-for]').forEach(function (element) {
                    ['name', 'data-valmsg-for'].forEach(function (attribute) {
                        var value = element.getAttribute(attribute);
                        if (value) {
                            element.setAttribute(attribute, value.replace(/^Items\[\d+\]/, 'Items[' + index + ']'));
                        }
                    });
                });
                row.querySelectorAll('[id]').forEach(function (element) {
                    element.id = element.id.replace(/^Items_\d+__/, 'Items_' + index + '__');
                });
            });

            // jQuery Validation caches the form's rules; drop them and read the renamed inputs again.
            if ($ && $.validator && $.validator.unobtrusive) {
                var $form = $(form);
                $form.removeData('validator').removeData('unobtrusiveValidation');
                $.validator.unobtrusive.parse($form);
            }
        }

        function kcalOf(row) {
            var select = row.querySelector('[data-food-select]');
            var option = select.options[select.selectedIndex];
            var perServing = option ? Number(option.getAttribute('data-kcal')) : NaN;
            var servings = Number(row.querySelector('[data-quantity]').value);
            return isFinite(perServing) && servings > 0 ? perServing * servings : null;
        }

        function recalculate() {
            var sum = 0;
            rows().forEach(function (row) {
                var kcal = kcalOf(row);
                row.querySelector('[data-row-kcal]').textContent = kcal === null ? '–' : Math.round(kcal).toLocaleString();
                sum += kcal || 0;
            });
            total.textContent = Math.round(sum).toLocaleString();
        }

        // Hides the foods that don't match; a row's chosen food always stays visible.
        function filter() {
            var term = search.value.trim().toLowerCase();
            body.querySelectorAll('[data-food-select]').forEach(function (select) {
                select.querySelectorAll('optgroup').forEach(function (group) {
                    var visible = 0;
                    group.querySelectorAll('option').forEach(function (option) {
                        var show = !term || option.selected || option.textContent.toLowerCase().indexOf(term) >= 0;
                        option.hidden = !show;
                        visible += show ? 1 : 0;
                    });
                    group.hidden = visible === 0;
                });
            });
        }

        function clear(row) {
            row.querySelectorAll('select').forEach(function (select) {
                select.selectedIndex = 0;
                select.classList.remove('input-validation-error');
            });
            row.querySelectorAll('input').forEach(function (input) {
                input.value = input.hasAttribute('data-quantity') ? '1' : '';
                input.classList.remove('input-validation-error');
            });
            row.querySelectorAll('.field-validation-error').forEach(function (message) {
                message.textContent = '';
                message.classList.replace('field-validation-error', 'field-validation-valid');
            });
        }

        addButton.addEventListener('click', function () {
            var all = rows();
            var copy = all[all.length - 1].cloneNode(true);
            clear(copy);
            body.appendChild(copy);
            renumber();
            filter();
            recalculate();
            copy.querySelector('select').focus();
        });

        body.addEventListener('click', function (event) {
            var button = event.target.closest('[data-remove-row]');
            if (!button) {
                return;
            }

            // The last row is cleared rather than removed, so there is always one to copy.
            var row = button.closest('tr.item-row');
            if (rows().length > 1) {
                row.remove();
                renumber();
            } else {
                clear(row);
            }
            recalculate();
        });

        body.addEventListener('change', recalculate);
        body.addEventListener('input', recalculate);
        search.addEventListener('input', filter);
        recalculate();
    });
})(window.jQuery);
