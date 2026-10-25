// Booking form: when the professional, day or length changes, fetches that day's free start times from
// AppointmentsController.AvailableSlots (JSON) and refills the time list, so only free times are offered (BR-15).
// Choosing a professional also pre-selects their usual appointment type until the client picks one themselves.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('book-form');
        if (!form) {
            return;
        }

        var url = form.getAttribute('data-slots-url');
        var date = form.querySelector('#Date');
        var minutes = form.querySelector('#DurationMinutes');
        var type = form.querySelector('#AppointmentType');
        var slots = form.querySelector('#StartTime');
        var help = document.getElementById('slot-help');
        var defaultHelp = help.textContent;
        var typeChosenByClient = false;
        var request = 0;

        function trainerId() {
            var checked = form.querySelector('input[name="TrainerId"]:checked');
            return checked ? checked.value : '';
        }

        function setOptions(placeholder, times) {
            var chosen = slots.value;
            slots.replaceChildren(new Option(placeholder, ''));
            times.forEach(function (time) {
                slots.appendChild(new Option(time, time, false, time === chosen));
            });
        }

        function refresh() {
            if (!trainerId() || !date.value || !minutes.value) {
                setOptions('Choose a professional and a day first', []);
                help.textContent = defaultHelp;
                return;
            }

            // Only the latest request may fill the list, in case answers arrive out of order.
            var thisRequest = ++request;
            slots.disabled = true;
            setOptions('Finding free times…', []);
            var query = new URLSearchParams({ trainerId: trainerId(), date: date.value, minutes: minutes.value });
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
                        setOptions('No free times that day', []);
                        help.textContent = 'Nothing is free that day for this length. Try another day or a shorter appointment.';
                    } else {
                        setOptions('Choose a time…', data.slots);
                        help.textContent = data.slots.length + (data.slots.length === 1 ? ' free time.' : ' free times.') + ' ' + defaultHelp;
                    }
                })
                .catch(function () {
                    if (thisRequest === request) {
                        setOptions('The free times could not be loaded', []);
                        help.textContent = 'Reload the page to try again.';
                    }
                })
                .finally(function () {
                    if (thisRequest === request) {
                        slots.disabled = false;
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
            } else if (target === date || target === minutes) {
                refresh();
            } else if (target === type) {
                typeChosenByClient = true;
            }
        });

        // A form shown again after an error already lists the day's times; otherwise fill them now.
        if (slots.options.length <= 1) {
            refresh();
        }
    });
})();
