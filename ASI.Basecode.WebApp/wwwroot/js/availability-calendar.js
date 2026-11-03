(() => {
    const element = document.getElementById("availability-calendar");
    const form = document.getElementById("availability-filters");
    if (!element || !form || !window.FullCalendar) return;

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

    function buildEquipmentList(items) {
        currentItems = new Map(items.map(item => [String(item.equipmentId), item]));
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
            const query = new URLSearchParams(new FormData(form));
            query.set("EquipmentId", item.equipmentId);
            link.href = `${form.action}?${query.toString()}`;
            link.className = "mt-2 inline-flex min-h-9 items-center font-display text-xs font-semibold uppercase tracking-wide text-primary-strong underline underline-offset-4";
            link.textContent = "View this item’s schedule";
            row.append(link);
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
        validRange: { start: element.dataset.today },
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
        allDaySlot: false,
        selectable: canReserve,
        selectMirror: true,
        selectMinDistance: 4,
        events: async (info, success, failure) => {
            clearError();
            const url = new URL(element.dataset.eventsUrl, window.location.origin);
            url.searchParams.set("start", info.startStr);
            url.searchParams.set("end", info.endStr);
            if (category.value) url.searchParams.set("categoryId", category.value);
            if (equipment.value) url.searchParams.set("equipmentId", equipment.value);
            if (location.value) url.searchParams.set("location", location.value);

            try {
                const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
                const data = await response.json();
                if (!response.ok) throw new Error(data.message || "The calendar could not load. Try again.");
                currentEvents = data.events || [];
                buildEquipmentList(data.items || []);
                success(currentEvents.map(event => ({ ...event, ...eventColor(event.status) })));
            } catch (error) {
                buildEquipmentList([]);
                setError(error.message);
                failure(error);
            }
        },
        selectAllow: info => {
            if (!canReserve || info.start < new Date()) return false;
            const item = currentItems.get(equipment.value);
            if (!item?.canBeReserved) return false;
            return !currentEvents.some(event =>
                String(event.equipmentId) === equipment.value &&
                info.start.getTime() < new Date(event.end).getTime() &&
                info.end.getTime() > new Date(event.start).getTime());
        },
        select: info => {
            if (!canReserve || !equipment.value) return;
            const destination = new URL(element.dataset.reservationUrl, window.location.origin);
            destination.searchParams.set("equipmentId", equipment.value);
            destination.searchParams.set("from", info.startStr.replace(/(?:Z|[+-]\d\d:\d\d)$/, "").slice(0, 16));
            destination.searchParams.set("to", info.endStr.replace(/(?:Z|[+-]\d\d:\d\d)$/, "").slice(0, 16));
            window.location.assign(destination);
        },
        datesSet: info => {
            dateInput.value = info.startStr.slice(0, 10);
            viewInput.value = info.view.type;
            const url = new URL(window.location.href);
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

    updateEquipmentOptions();
    calendar.render();
})();
