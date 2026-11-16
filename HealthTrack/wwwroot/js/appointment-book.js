// Booking form: when the professional, day or length changes, fetches that day's free start times from
// AppointmentsController.AvailableSlots (JSON) and rebuilds the time pills (StartTime radios, grouped into morning,
// afternoon and evening), so only free times are offered (BR-15). Choosing a professional also pre-selects their usual
// appointment type until the client picks one themselves. The "Your request" box follows every choice.
(function ($) {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('book-form');
        if (!form) {
            return;
        }

        var url = form.getAttribute('data-slots-url');
        var date = form.querySelector('#Date');
        var type = form.querySelector('#AppointmentType');
        var list = form.querySelector('[data-slot-list]');
        var heading = form.querySelector('[data-slot-heading]');
        var help = document.getElementById('slot-help');
        var defaultHelp = help.textContent;
        var typeChosenByClient = false;
        var request = 0;

        function checkedValue(name) {
            var checked = form.querySelector('input[name="' + name + '"]:checked');
            return checked ? checked.value : '';
        }

        function summary(key, text) {
            var element = form.querySelector('[data-summary="' + key + '"]');
            if (element) {
                element.textContent = text;
            }
        }

        // The chosen date as "Mon 2 Nov 2026", read from the yyyy-MM-dd value without time-zone surprises.
        function dayText(long) {
            if (!date.value) {
                return '';
            }
            var parts = date.value.split('-');
            var day = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
            var options = long
                ? { weekday: 'long', day: 'numeric', month: 'long' }
                : { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' };
            return day.toLocaleDateString('en-GB', options).replace(/,/g, '');
        }

        function addMinutes(time, minutes) {
            var parts = time.split(':');
            var total = Number(parts[0]) * 60 + Number(parts[1]) + minutes;
            var pad = function (n) { return (n < 10 ? '0' : '') + n; };
            return pad(Math.floor(total / 60) % 24) + ':' + pad(total % 60);
        }

        function updateSummary() {
            var pro = form.querySelector('input[name="TrainerId"]:checked');
            var box = form.querySelector('[data-summary-pro]');
            if (box) {
                box.hidden = !pro;
            }
            if (pro) {
                var name = pro.getAttribute('data-name') || '';
                summary('name', name);
                summary('profession', pro.getAttribute('data-profession') || '');
                summary('initials', name.split(' ').filter(Boolean).slice(0, 2).map(function (w) { return w[0].toUpperCase(); }).join(''));
            }
            summary('type', type.selectedIndex > 0 ? type.options[type.selectedIndex].text : '–');
            var mode = form.querySelector('input[name="Mode"]:checked');
            summary('mode', mode ? mode.getAttribute('data-label') : '–');
            summary('day', dayText(false) || '–');
            var start = checkedValue('StartTime');
            var minutes = Number(checkedValue('DurationMinutes'));
            summary('time', start && minutes ? start + ' – ' + addMinutes(start, minutes) : '–');
        }

        // jQuery Validation caches the form's rules; read the rebuilt time pills again.
        function reparse() {
            if ($ && $.validator && $.validator.unobtrusive) {
                var $form = $(form);
                $form.removeData('validator').removeData('unobtrusiveValidation');
                $.validator.unobtrusive.parse($form);
            }
        }

        function message(text) {
            var p = document.createElement('p');
            p.className = 'small muted';
            p.textContent = text;
            list.replaceChildren(p);
        }

        function partOfDay(time) {
            var hour = Number(time.split(':')[0]);
            return hour < 12 ? 'Morning' : hour < 17 ? 'Afternoon' : 'Evening';
        }

        function showSlots(times) {
            var chosen = checkedValue('StartTime');
            var groups = {};
            var order = [];
            times.forEach(function (time) {
                var part = partOfDay(time);
                if (!groups[part]) {
                    groups[part] = [];
                    order.push(part);
                }
                groups[part].push(time);
            });

            list.replaceChildren();
            order.forEach(function (part) {
                var group = document.createElement('div');
                group.className = 'slot-group';
                var title = document.createElement('span');
                title.className = 'eyebrow';
                title.textContent = part;
                var grid = document.createElement('div');
                grid.className = 'slot-grid';
                groups[part].forEach(function (time) {
                    var id = 'slot-' + time.replace(':', '');
                    var pill = document.createElement('span');
                    pill.className = 'pill';
                    var input = document.createElement('input');
                    input.type = 'radio';
                    input.name = 'StartTime';
                    input.id = id;
                    input.value = time;
                    input.checked = time === chosen;
                    input.setAttribute('data-val', 'true');
                    input.setAttribute('data-val-required', 'Choose one of the free times.');
                    var label = document.createElement('label');
                    label.htmlFor = id;
                    label.textContent = time;
                    pill.append(input, label);
                    grid.appendChild(pill);
                });
                group.append(title, grid);
                list.appendChild(group);
            });
        }

        function refresh() {
            var minutes = checkedValue('DurationMinutes');
            heading.textContent = date.value ? 'Free times on ' + dayText(true) : 'Free times';
            if (!checkedValue('TrainerId') || !date.value || !minutes) {
                message('Choose a professional and a day first.');
                help.textContent = defaultHelp;
                reparse();
                updateSummary();
                return;
            }

            // Only the latest request may fill the list, in case answers arrive out of order.
            var thisRequest = ++request;
            message('Finding free times…');
            var query = new URLSearchParams({ trainerId: checkedValue('TrainerId'), date: date.value, minutes: minutes });
            fetch(url + '?' + query.toString(), { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error(response.status);
                    }
                    return response.json();
                })
                .then(function (data) {
                    if (thisRequest !== request) {
                        return;
                    }
                    if (data.slots.length === 0) {
                        message('No free times that day.');
                        help.textContent = 'Nothing is free that day for this length. Try another day or a shorter appointment.';
                    } else {
                        showSlots(data.slots);
                        help.textContent = data.slots.length + (data.slots.length === 1 ? ' free time. ' : ' free times. ') + defaultHelp;
                    }
                    reparse();
                    updateSummary();
                })
                .catch(function () {
                    if (thisRequest === request) {
                        message('The free times could not be loaded. Reload the page to try again.');
                    }
                });
        }

        form.addEventListener('change', function (event) {
            var target = event.target;
            if (target.name === 'TrainerId') {
                var suggested = target.getAttribute('data-suggested-type');
                if (!typeChosenByClient && suggested) {
                    type.value = suggested;
                }
                refresh();
            } else if (target === date || target.name === 'DurationMinutes') {
                refresh();
            } else if (target === type) {
                typeChosenByClient = true;
            }
            updateSummary();
        });

        // A form shown again after an error already lists the day's times; otherwise fill them now.
        if (!list.querySelector('input[name="StartTime"]')) {
            refresh();
        } else {
            updateSummary();
        }
    });
})(window.jQuery);
