(function () {
    "use strict";

    // ---- CSRF/antiforgery for AJAX ----------------------------------------
    // Every $.post/$.ajax POST in this app targets an action guarded by
    // [ValidateAntiForgeryToken]. Those calls build their own request body
    // (an object or FormData) rather than serializing a Razor <form>, so
    // there's no hidden __RequestVerificationToken field to carry the token.
    // Attaching it as a header here (read once from the layout's meta tag)
    // covers every such call in the app in one place, matching the
    // X-CSRF-TOKEN header name Program.cs registers with AddAntiforgery.
    var csrfToken = document.querySelector('meta[name="request-verification-token"]');
    if (csrfToken) {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                xhr.setRequestHeader("X-CSRF-TOKEN", csrfToken.content);
            }
        });
    }

    // ---- Dark mode -----------------------------------------------------
    var root = document.documentElement;
    var toggleBtn = document.getElementById("darkModeToggle");
    var STORAGE_KEY = "atm-ticketing-theme";

    function applyTheme(theme) {
        root.setAttribute("data-bs-theme", theme);
        if (toggleBtn) {
            var icon = toggleBtn.querySelector("i");
            if (icon) {
                icon.className = theme === "dark" ? "bi bi-sun" : "bi bi-moon-stars";
            }
        }
    }

    try {
        var saved = localStorage.getItem(STORAGE_KEY);
        if (saved) {
            applyTheme(saved);
        }
    } catch (e) { /* storage unavailable, keep default */ }

    if (toggleBtn) {
        toggleBtn.addEventListener("click", function () {
            var next = root.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
            applyTheme(next);
            try { localStorage.setItem(STORAGE_KEY, next); } catch (e) { /* ignore */ }
        });
    }

    // ---- Mobile sidebar --------------------------------------------------
    var sidebarToggle = document.getElementById("sidebarToggle");
    var sidebar = document.getElementById("sidebar");
    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener("click", function () {
            sidebar.classList.toggle("show");
        });
    }

    // ---- Notification bell -----------------------------------------------
    function loadNotifications() {
        var list = document.getElementById("notificationList");
        var countBadge = document.getElementById("notificationCount");
        if (!list || !countBadge) return;

        fetch("/api/notifications/unread")
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) {
                if (!items || items.length === 0) {
                    countBadge.classList.add("d-none");
                    list.innerHTML = '<li class="text-muted small px-2">No new notifications</li>';
                    return;
                }
                countBadge.classList.remove("d-none");
                countBadge.textContent = items.length;
                list.innerHTML = items.map(function (n) {
                    var link = n.ticketId ? "/Ticket/Details/" + n.ticketId : "#";
                    return '<li><a class="dropdown-item small" href="' + link + '"><strong>' + n.subject + '</strong><br>' + n.message + '</a></li>';
                }).join("");
            })
            .catch(function () { /* silent — best effort */ });
    }

    if (document.getElementById("notificationBell")) {
        loadNotifications();
        setInterval(loadNotifications, 60000);
    }

    // ---- SLA countdown timers ---------------------------------------------
    function formatRemaining(ms) {
        if (ms <= 0) return "Breached";
        var totalMinutes = Math.floor(ms / 60000);
        var hours = Math.floor(totalMinutes / 60);
        var minutes = totalMinutes % 60;
        return hours > 0 ? (hours + "h " + minutes + "m") : (minutes + "m");
    }

    function tickSlaCountdowns() {
        document.querySelectorAll("[data-sla-due]").forEach(function (el) {
            var due = new Date(el.getAttribute("data-sla-due"));
            var remaining = due.getTime() - Date.now();
            el.textContent = formatRemaining(remaining);
            el.classList.remove("breached", "warning");
            if (remaining <= 0) {
                el.classList.add("breached");
            } else if (remaining < 30 * 60000) {
                el.classList.add("warning");
            }
        });
    }

    // Runs unconditionally (cheap no-op with zero matches) so it also catches rows added
    // later by DataTables AJAX, which don't exist yet at DOMContentLoaded.
    tickSlaCountdowns();
    setInterval(tickSlaCountdowns, 15000);

    // Expose a tiny helper other pages use to show ServiceResult toasts.
    window.atmTicketing = {
        showResult: function (result) {
            var message = result && result.message ? result.message : (result && result.succeeded ? "Done." : "Something went wrong.");
            if (result && result.errors && result.errors.length) {
                message = result.errors.join(" ");
            }
            alert(message);
        },

        // Server-side DataTables controllers here bind to a simplified DataTableRequest
        // (Draw/Start/Length/SearchValue/SortColumn/SortDirection) rather than DataTables'
        // own verbose default POST shape, so every AJAX-backed table remaps through this
        // before the request goes out. `extra` merges in page-specific filter fields.
        mapDtParams: function (d, extra) {
            var order = (d.order && d.order[0]) || { column: 0, dir: "desc" };
            var column = d.columns && d.columns[order.column];
            var mapped = {
                Draw: d.draw,
                Start: d.start,
                Length: d.length,
                SearchValue: d.search ? d.search.value : "",
                SortColumn: column ? column.data : null,
                SortDirection: order.dir
            };
            return Object.assign(mapped, extra || {});
        }
    };
})();
