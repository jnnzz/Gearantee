(() => {
    const element = document.getElementById("availability-calendar");
    const form = document.getElementById("availability-filters");
    if (!element || !form) return;
    const grid = document.getElementById("availability-overview");
    const dateInput = document.getElementById("availability-date");
    const viewInput = document.getElementById("availability-view");
    const loading = document.getElementById("availability-loading");
    const error = document.getElementById("availability-error");
    const retry = document.getElementById("availability-retry");
    const dayString = date => date.toISOString().slice(0, 10);
    const addDays = (date, days) => new Date(date.getTime() + days * 86400000);
    const localDate = text => new Date(text + "T00:00:00Z");
    const midnight = date => Date.parse(dayString(date) + "T00:00:00+08:00");
    const dayView = viewInput.value === "timeGridDay";
    const selected = localDate(dateInput.value);
    const first = dayView ? selected : addDays(selected, -((selected.getUTCDay() + 6) % 7));
    const days = Array.from({length: dayView ? 1 : 7}, (_, index) => addDays(first, index));
    const shortDate = new Intl.DateTimeFormat("en-PH", {day: "numeric", month: "short", year: "numeric", timeZone: "UTC"});
    const dayLabel = new Intl.DateTimeFormat("en-PH", {weekday: "short", day: "numeric", timeZone: "UTC"});
    const eventTime = new Intl.DateTimeFormat("en-PH", {day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", timeZone: "Asia/Manila"});
    document.getElementById("calendar-period-label").textContent = dayView ? shortDate.format(first) : shortDate.format(first) + " – " + shortDate.format(days[6]);
    grid.style.setProperty("--calendar-days", days.length);
    grid.classList.toggle("calendar-day", dayView);
    for (const button of document.querySelectorAll("[data-calendar-view]")) {
        button.addEventListener("click", () => { viewInput.value = button.dataset.calendarView; form.requestSubmit(); });
    }
    for (const button of document.querySelectorAll("[data-period-step]")) {
        const step = Number(button.dataset.periodStep);
        const target = step ? addDays(first, step * days.length) : localDate(element.dataset.today);
        const value = dayString(target);
        button.disabled = value < dateInput.dataset.min || value > dateInput.dataset.max;
        button.addEventListener("click", () => { dateInput.value = value; form.requestSubmit(); });
    }
    function node(tag, className, text) {
        const value = document.createElement(tag);
        value.className = className;
        if (text !== undefined) value.textContent = text;
        return value;
    }
    function render(data) {
        const header = node("div", "calendar-row calendar-heading");
        header.append(node("div", "calendar-item", "Equipment item"));
        const headings = node("div", "calendar-track");
        days.forEach(day => headings.append(node("div", "calendar-date" + (dayString(day) === element.dataset.today ? " is-today" : ""), dayLabel.format(day))));
        header.append(headings); grid.append(header);
        if (!data.items.length) { grid.append(node("p", "calendar-empty", "No equipment matches your search.")); return; }
        const byItem = new Map();
        for (const event of data.events) {
            const key = String(event.equipmentId);
            if (!byItem.has(key)) byItem.set(key, []);
            byItem.get(key).push(event);
        }
        for (const item of data.items) {
            const row = node("div", "calendar-row");
            const label = node("div", "calendar-item");
            label.append(node("strong", "", item.itemName), node("span", "item-code", item.itemCode));
            row.append(label);
            const track = node("div", "calendar-track");
            const events = byItem.get(String(item.equipmentId)) || [];
            const lanes = [];
            for (let index = 0; index < days.length; index++) {
                const day = days[index];
                const blocked = events.some(event => Date.parse(event.start) < midnight(addDays(day, 1)) && Date.parse(event.end) > midnight(day));
                const allowed = element.dataset.canReserve === "True" && item.canBeReserved && !blocked && dayString(day) > element.dataset.today;
                const cell = node(allowed ? "a" : "div", "calendar-slot" + (dayString(day) === element.dataset.today ? " is-today" : ""));
                cell.style.gridColumn = index + 1;
                cell.style.gridRow = "1 / " + (Math.max(events.length, 1) + 1);
                cell.setAttribute("aria-label", item.itemName + ": " + shortDate.format(day) + (blocked ? ", booked" : item.canBeReserved ? ", available" : ", unavailable"));
                if (allowed) {
                    const url = new URL(element.dataset.reservationUrl, location.origin);
                    url.searchParams.set("equipmentId", item.equipmentId);
                    url.searchParams.set("from", dayString(day)); url.searchParams.set("to", dayString(day));
                    cell.href = url;
                    cell.title = "Request " + item.itemName + " for " + shortDate.format(day);
                }
                track.append(cell);
            }
            for (const event of events) {
                if (Date.parse(event.end) <= midnight(first)) continue;
                const left = days.findIndex(day => Date.parse(event.start) < midnight(addDays(day, 1)));
                if (left < 0) continue;
                // Events can overlap within a day; each gets its own visible lane.
                let right = left;
                while (right + 1 < days.length && Date.parse(event.end) > midnight(days[right + 1])) right++;
                const status = ["Reserved", "Borrowed", "Overdue"].includes(event.status) ? event.status : "Reserved";
                const bar = node("div", "calendar-bar calendar-" + status.toLowerCase(), status);
                bar.style.gridColumn = (left + 1) + " / " + (right + 2);
                bar.style.gridRow = lanes.length + 1;
                bar.title = status + ": " + eventTime.format(new Date(event.start)) + " – " + eventTime.format(new Date(event.end));
                bar.tabIndex = 0; bar.setAttribute("aria-label", bar.title);
                track.append(bar); lanes.push(event);
            }
            if (!item.canBeReserved) {
                const maintenance = node("div", "calendar-bar calendar-maintenance", item.operationalStatus === "UnderMaintenance" ? "Maintenance · completion date unknown" : "Unavailable");
                maintenance.style.gridColumn = "1 / -1";
                maintenance.style.gridRow = events.length + 1;
                track.append(maintenance);
            }
            row.append(track); grid.append(row);
        }
    }
    let request;
    async function load() {
        request?.abort();
        const controller = new AbortController(); request = controller;
        grid.replaceChildren(); loading.classList.remove("hidden"); error.classList.add("hidden"); retry.classList.add("hidden");
        const url = new URL(element.dataset.eventsUrl, location.origin);
        url.search = new URLSearchParams(new FormData(form)).toString();
        url.searchParams.set("start", dayString(first) + "T00:00:00+08:00");
        url.searchParams.set("end", dayString(addDays(first, days.length)) + "T00:00:00+08:00");
        try {
            const response = await fetch(url, {credentials: "same-origin", signal: controller.signal, headers: {Accept: "application/json"}});
            const data = await response.json();
            if (controller.signal.aborted) return;
            if (!response.ok) throw Error(data.message || "The calendar could not load.");
            render(data);
        } catch (failure) {
            if (controller.signal.aborted) return;
            grid.replaceChildren(); error.textContent = "Unable to load availability. " + failure.message;
            error.classList.remove("hidden"); retry.classList.remove("hidden");
        } finally { if (!controller.signal.aborted) loading.classList.add("hidden"); }
    }
    retry.addEventListener("click", load);
    load();
})();
