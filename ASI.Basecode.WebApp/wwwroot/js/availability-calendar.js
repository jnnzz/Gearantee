(() => {
    const element = document.getElementById("availability-calendar");
    const form = document.getElementById("availability-filters");
    if (!element || !form) return;

    const errorElement = document.getElementById("availability-error");
    const itemList = document.getElementById("availability-items");
    const emptyElement = document.getElementById("availability-empty");
    const countElement = document.getElementById("availability-item-count");
    const category = document.getElementById("availability-category");
    const equipment = document.getElementById("availability-equipment");
    const location = document.getElementById("availability-location");
    const dateInput = document.getElementById("availability-date");
    const viewInput = document.getElementById("availability-view");
    const canReserve = element.dataset.canReserve === "True";
    const overview = document.getElementById("availability-overview");
    const loading = document.getElementById("availability-loading");
    const retry = document.getElementById("availability-retry");
    let request;
    let currentItems = new Map();
    let currentEvents = [];

    function setError(message) {
        errorElement.textContent = message || "The calendar could not load. Try again.";
        errorElement.classList.remove("hidden");
    }

    function clearError() {
        errorElement.textContent = "";
        errorElement.classList.add("hidden");
    }

    function updateEquipmentOptions() {
        for (const option of equipment.options) {
            if (!option.value) continue;
            option.hidden = (category.value && option.dataset.category !== category.value) ||
                (location.value && option.dataset.location !== location.value);
        }

        const selected = equipment.selectedOptions[0];
        if (selected?.hidden) equipment.value = "";
    }

    updateEquipmentOptions();
    const applied = new URLSearchParams(new FormData(form));
    const selectedEquipment = applied.get("EquipmentId");
    const manilaTime = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });

    async function loadPeriod(start, end, render, onError = () => {}) {
        request?.abort();
        const controller = new AbortController();
        request = controller;
        clearError();
        retry.classList.add("hidden");
        loading.classList.remove("hidden");
        currentItems.clear();
        currentEvents = [];
        const url = new URL(element.dataset.eventsUrl, window.location.origin);
        url.search = applied.toString();
        url.searchParams.set("start", start);
        url.searchParams.set("end", end);

        try {
            const response = await fetch(url, { credentials: "same-origin", signal: controller.signal, headers: { Accept: "application/json" } });
            const data = await response.json();
            if (controller.signal.aborted) return;
            if (!response.ok) throw new Error(data.message || "The calendar could not load. Try again.");
            currentEvents = data.events;
            currentItems = new Map(data.items.map(item => [String(item.equipmentId), item]));
            render(data);
        } catch (error) {
            if (controller.signal.aborted) return;
            currentItems.clear();
            currentEvents = [];
            setError(error.message);
            retry.classList.remove("hidden");
            onError(error);
        } finally {
            if (!controller.signal.aborted) loading.classList.add("hidden");
        }
    }

    function scheduleLink(item, date) {
        const url = new URL(form.action);
        url.search = applied.toString();
        url.searchParams.set("EquipmentId", item.equipmentId);
        url.searchParams.set("Date", date);
        url.searchParams.set("View", "timeGridDay");
        const link = document.createElement("a");
        link.href = url;
        link.className = "text-xs font-semibold text-primary-strong";
        link.textContent = "View time details";
        link.setAttribute("aria-label", `View time details for ${item.itemCode}`);
        return link;
    }

    function addCell(row, tag, text, scope) {
        const cell = document.createElement(tag);
        cell.textContent = text;
        if (scope) cell.scope = scope;
        row.append(cell);
        return cell;
    }

    function renderWeek(days, data) {
        const header = overview.createTHead().insertRow();
        addCell(header, "th", "Equipment", "col");
        const dayLabel = new Intl.DateTimeFormat("en-PH", { weekday: "short", month: "short", day: "numeric", timeZone: "UTC" });
        for (const day of days) addCell(header, "th", dayLabel.format(day), "col");
        const body = overview.createTBody();
        if (!data.items.length) {
            addCell(body.insertRow(), "td", "No equipment matches these filters.").colSpan = 8;
            return;
        }
        const byEquipment = new Map();
        for (const event of data.events) {
            if (!byEquipment.has(event.equipmentId)) byEquipment.set(event.equipmentId, []);
            byEquipment.get(event.equipmentId).push(event);
        }
        for (const item of data.items) {
            const row = body.insertRow();
            const name = addCell(row, "th", `${item.itemCode} · ${item.itemName}`, "row");
            const status = document.createElement("p");
            status.textContent = item.operationalStatus.replace(/([a-z])([A-Z])/g, "$1 $2");
            name.append(status, scheduleLink(item, applied.get("Date")));
            for (const day of days) {
                const cell = row.insertCell();
                const start = new Date(`${dayString(day)}T00:00:00+08:00`).getTime();
                const end = start + 86400000;
                for (const event of byEquipment.get(item.equipmentId) || []) {
                    if (Date.parse(event.start) >= end || Date.parse(event.end) <= start) continue;
                    const detail = document.createElement("details");
                    const label = document.createElement("summary");
                    label.className = `status-badge status-${event.status === "Overdue" ? "danger" : "info"}`;
                    label.textContent = event.status;
                    const time = document.createElement("p");
                    time.textContent = `${manilaTime.format(new Date(event.start))} – ${manilaTime.format(new Date(event.end))}`;
                    detail.append(label, time);
                    cell.append(detail);
                }
            }
        }
    }

    const dayString = day => day.toISOString().slice(0, 10);
    const addDays = (day, count) => new Date(day.getTime() + count * 86400000);
    const minDate = new Date(`${dateInput.min}T00:00:00Z`);
    const maxDate = new Date(`${dateInput.max}T00:00:00Z`);

    if (overview) {
        const date = new Date(`${dateInput.value}T00:00:00Z`);
        const monday = addDays(date, -((date.getUTCDay() + 6) % 7));
        const days = Array.from({ length: 7 }, (_, index) => addDays(monday, index));
        function loadWeek() {
            overview.replaceChildren();
            loadPeriod(`${dayString(monday)}T00:00:00+08:00`, `${dayString(addDays(monday, 7))}T00:00:00+08:00`,
                data => renderWeek(days, data));
        }
        for (const button of document.querySelectorAll("[data-week-step]")) {
            const step = Number(button.dataset.weekStep);
            const target = step ? addDays(monday, step) : new Date(`${element.dataset.today}T00:00:00Z`);
            button.disabled = target < minDate || target > maxDate;
            button.addEventListener("click", () => {
                dateInput.value = dayString(target);
                form.requestSubmit();
            });
        }
        retry.addEventListener("click", loadWeek);
        loadWeek();
        return;
    }

    if (!window.FullCalendar) {
        loading.classList.add("hidden");
        setError("The time grid could not load. Refresh the page to try again.");
        return;
    }

    let activeScheduleDate = element.dataset.initialDate;
    let activeScheduleView = element.dataset.initialView;

    function updateItemScheduleLink(link) {
        const query = new URLSearchParams(applied);
        query.set("Date", activeScheduleDate);
        query.set("View", activeScheduleView);
        query.set("EquipmentId", link.dataset.scheduleEquipment);
        link.href = `${form.action}?${query.toString()}`;
    }

    function buildEquipmentList(items) {
        itemList.replaceChildren();
        emptyElement.classList.toggle("hidden", items.length !== 0);
        countElement.textContent = `${items.length} item${items.length === 1 ? "" : "s"}`;

        for (const item of items) {
            const row = document.createElement("li");
            row.className = "availability-item py-3";

            const heading = document.createElement("div");
            heading.className = "flex items-start justify-between gap-2";
            const name = document.createElement("span");
            name.className = "text-sm font-semibold text-ink";
            name.textContent = `${item.itemCode} · ${item.itemName}`;
            heading.append(name);

            const badge = document.createElement("span");
            badge.className = `availability-status ${item.canBeReserved ? "availability-status-open" : "availability-status-unavailable"}`;
            badge.textContent = item.canBeReserved ? "Reservable" : item.operationalStatus.replace(/([a-z])([A-Z])/g, "$1 $2");
            heading.append(badge);

            const details = document.createElement("p");
            details.className = "mt-1 text-xs text-gray-500";
            details.textContent = `${item.categoryName} · ${item.location || "No location recorded"}`;

            const summary = document.createElement("p");
            summary.className = "mt-1 text-xs text-gray-700";
            summary.textContent = item.availabilitySummary;

            row.append(heading, details, summary);

            const link = document.createElement("a");
            link.dataset.scheduleEquipment = item.equipmentId;
            updateItemScheduleLink(link);
            link.className = "mt-2 inline-flex min-h-9 items-center font-display text-xs font-semibold uppercase tracking-wide text-primary-strong underline underline-offset-4";
            link.textContent = "View this item’s schedule";
            row.append(link);
            if (canReserve && item.canBeReserved) {
                const reserve = document.createElement("a");
                const destination = new URL(element.dataset.reservationUrl, window.location.origin);
                destination.searchParams.set("equipmentId", item.equipmentId);
                reserve.href = destination;
                reserve.className = "ml-3 text-xs font-semibold text-primary-strong";
                reserve.textContent = "Enter reservation times";
                row.append(reserve);
            }
            itemList.append(row);
        }
    }

    function eventColor(status) {
        if (status === "Overdue") return { backgroundColor: "#B42318", borderColor: "#B42318" };
        if (status === "Borrowed") return { backgroundColor: "#315B84", borderColor: "#315B84" };
        return { backgroundColor: "#4A6F94", borderColor: "#4A6F94" };
    }

    const calendar = new FullCalendar.Calendar(element, {
        timeZone: "Asia/Manila",
        initialView: element.dataset.initialView,
        initialDate: element.dataset.initialDate,
        validRange: { start: dateInput.min, end: dayString(addDays(maxDate, 1)) },
        headerToolbar: {
            left: "prev,next today",
            center: "title",
            right: "timeGridWeek,timeGridDay"
        },
        buttonText: { today: "Today", timeGridWeek: "Week", timeGridDay: "Day" },
        // UI appearance only: FullCalendar 7 uses eventClass/eventInnerClass.
        // Scheduling, event data and selection handlers below remain unchanged.
        eventClass: info => `availability-event availability-event-${String(info.event.extendedProps.status || "reserved").toLowerCase()}`,
        eventInnerClass: () => "availability-event-inner",
        height: "auto",
        nowIndicator: true,
        // Equipment summaries need counts for the visible range, even inside a cached week.
        lazyFetching: false,
        allDaySlot: false,
        selectable: canReserve,
        selectMirror: true,
        selectMinDistance: 4,
        events: (info, success, failure) => loadPeriod(info.startStr, info.endStr, data => {
            buildEquipmentList(data.items);
            success(data.events.map(event => ({ ...event, ...eventColor(event.status) })));
        }, error => {
            buildEquipmentList([]);
            failure(error);
        }),
        selectAllow: info => {
            if (!canReserve || equipment.value !== selectedEquipment || info.start < new Date()) return false;
            const item = currentItems.get(selectedEquipment);
            if (!item?.canBeReserved) return false;
            return !currentEvents.some(event =>
                String(event.equipmentId) === selectedEquipment &&
                info.start.getTime() < new Date(event.end).getTime() &&
                info.end.getTime() > new Date(event.start).getTime());
        },
        select: info => {
            if (!canReserve || !selectedEquipment) return;
            const destination = new URL(element.dataset.reservationUrl, window.location.origin);
            destination.searchParams.set("equipmentId", selectedEquipment);
            destination.searchParams.set("from", info.startStr.replace(/(?:Z|[+-]\d\d:\d\d)$/, "").slice(0, 16));
            destination.searchParams.set("to", info.endStr.replace(/(?:Z|[+-]\d\d:\d\d)$/, "").slice(0, 16));
            window.location.assign(destination);
        },
        datesSet: info => {
            activeScheduleDate = dateInput.value = info.startStr.slice(0, 10);
            activeScheduleView = viewInput.value = info.view.type;
            itemList.querySelectorAll("[data-schedule-equipment]").forEach(updateItemScheduleLink);
            const url = new URL(window.location.href);
            url.searchParams.delete("Date");
            url.searchParams.delete("View");
            url.searchParams.set("date", dateInput.value);
            url.searchParams.set("view", viewInput.value);
            history.replaceState(null, "", url);
        }
    });

    category.addEventListener("change", updateEquipmentOptions);
    location.addEventListener("change", updateEquipmentOptions);
    form.addEventListener("submit", () => {
        dateInput.value = dateInput.value || element.dataset.today;
        viewInput.value = viewInput.value || element.dataset.initialView;
    });

    retry.addEventListener("click", () => calendar.refetchEvents());
    calendar.render();
})();
