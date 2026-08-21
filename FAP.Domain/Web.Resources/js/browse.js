(() => {
  const STORAGE_KEY = "fap-guest-show-open";
  const HELP_SEEN_KEY = "fap-guest-optin-help-seen";
  const optIn = document.getElementById("fap-optin");
  const optInHelp = document.getElementById("fap-optin-help");
  const optInWhats = document.getElementById("fap-optin-whats");
  const handoffStatus = document.getElementById("fap-handoff-status");

  const showOptInHelp = () => {
    if (optInHelp) optInHelp.hidden = false;
    if (optIn) optIn.setAttribute("aria-describedby", "fap-optin-help");
    if (optInWhats) optInWhats.hidden = true;
  };

  const hideOptInHelp = () => {
    if (optInHelp) optInHelp.hidden = true;
    if (optIn) optIn.removeAttribute("aria-describedby");
    if (optInWhats) optInWhats.hidden = false;
  };

  const markOptInHelpSeen = () => {
    try {
      localStorage.setItem(HELP_SEEN_KEY, "1");
    } catch {
      /* ignore */
    }
    hideOptInHelp();
  };

  const setFapVisible = (on) => {
    document.documentElement.classList.toggle("show-fap-links", on);
    if (optIn) optIn.checked = on;
    try {
      localStorage.setItem(STORAGE_KEY, on ? "1" : "0");
    } catch {
      /* ignore */
    }
  };

  let preferred = false;
  let helpSeen = false;
  try {
    preferred = localStorage.getItem(STORAGE_KEY) === "1";
    helpSeen = localStorage.getItem(HELP_SEEN_KEY) === "1";
  } catch {
    preferred = false;
    helpSeen = false;
  }
  setFapVisible(preferred);
  if (helpSeen) hideOptInHelp();
  else showOptInHelp();

  if (optIn) {
    optIn.addEventListener("change", () => {
      setFapVisible(optIn.checked);
      markOptInHelpSeen();
    });
  }

  if (optInWhats) {
    optInWhats.addEventListener("click", () => {
      showOptInHelp();
    });
  }

  let handoffTimer = 0;
  let dismissBound = false;
  let handoffName = null;

  const clearNameHighlight = () => {
    if (!handoffName) return;
    handoffName.classList.remove("handoff-target");
    handoffName = null;
  };

  const unbindHandoffDismiss = () => {
    if (!dismissBound) return;
    window.removeEventListener("pointerdown", onHandoffDismiss, true);
    window.removeEventListener("keydown", onHandoffDismiss, true);
    dismissBound = false;
  };

  const onHandoffDismiss = (event) => {
    // Keep Trying / Didn't open? until the user acts outside the status strip.
    if (event?.target?.closest?.("#fap-handoff-status")) return;
    if (event?.target?.closest?.("a[data-fap-handoff]")) return;
    clearHandoff();
  };

  const bindHandoffDismiss = () => {
    if (dismissBound) return;
    dismissBound = true;
    window.setTimeout(() => {
      if (!dismissBound) return;
      window.addEventListener("pointerdown", onHandoffDismiss, true);
      window.addEventListener("keydown", onHandoffDismiss, true);
    }, 0);
  };

  const clearHandoff = () => {
    if (handoffTimer) {
      window.clearTimeout(handoffTimer);
      handoffTimer = 0;
    }
    unbindHandoffDismiss();
    clearNameHighlight();
    if (!handoffStatus) return;
    handoffStatus.hidden = true;
    handoffStatus.replaceChildren();
  };

  const focusNameLink = (nameLink) => {
    if (!nameLink) return;
    handoffName = nameLink;
    nameLink.classList.add("handoff-target");
    try {
      nameLink.focus({ preventScroll: false });
    } catch {
      nameLink.focus();
    }
  };

  const showBrowserFallbackTip = (nameLink) => {
    if (handoffTimer) {
      window.clearTimeout(handoffTimer);
      handoffTimer = 0;
    }
    if (!handoffStatus) return;
    handoffStatus.hidden = false;
    handoffStatus.replaceChildren();
    const msg = document.createElement("span");
    msg.textContent =
      "Use the highlighted file name to download in your browser instead.";
    handoffStatus.appendChild(msg);
    focusNameLink(nameLink);
    bindHandoffDismiss();
  };

  const showHandoffTrying = (nameLink) => {
    if (!handoffStatus) return;
    handoffStatus.hidden = false;
    handoffStatus.replaceChildren();

    const msg = document.createElement("span");
    msg.textContent = "Trying the FAP desktop client… ";

    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "handoff-fallback";
    btn.textContent = "Didn't open?";
    btn.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      showBrowserFallbackTip(nameLink);
    });

    const hint = document.createElement("span");
    hint.className = "handoff-hint";
    hint.textContent = "Or use the file name below.";

    handoffStatus.append(msg, btn, hint);
    bindHandoffDismiss();
  };

  // Soft tip only via "Didn't open?" — keep Trying UI until user acts (OS may steal focus).
  document.querySelectorAll("a[data-fap-handoff]").forEach((link) => {
    link.addEventListener("click", () => {
      clearHandoff();
      const nameLink =
        link.closest(".name-row")?.querySelector("a.file-name") || null;
      showHandoffTrying(nameLink);
    });
  });

  const table = document.getElementById("files");
  if (!table) return;
  const tbody = table.tBodies[0];
  if (!tbody) return;

  let sortKey = "name";
  let ascending = true;

  const compare = (a, b) => {
    let av;
    let bv;
    switch (sortKey) {
      case "size":
        av = Number(a.dataset.size || 0);
        bv = Number(b.dataset.size || 0);
        break;
      case "modified":
        av = Number(a.dataset.modified || 0);
        bv = Number(b.dataset.modified || 0);
        break;
      case "icon":
        av = a.dataset.icon || "";
        bv = b.dataset.icon || "";
        break;
      default:
        av = (a.dataset.name || "").toLowerCase();
        bv = (b.dataset.name || "").toLowerCase();
    }
    if (av < bv) return ascending ? -1 : 1;
    if (av > bv) return ascending ? 1 : -1;
    return 0;
  };

  const headers = Array.from(table.querySelectorAll("th[data-sort]"));

  const updateSortUi = () => {
    headers.forEach((th) => {
      const key = th.getAttribute("data-sort") || "";
      const btn = th.querySelector("button.sort-btn");
      if (key === sortKey) {
        th.setAttribute("aria-sort", ascending ? "ascending" : "descending");
        if (btn) {
          btn.setAttribute(
            "aria-label",
            `Sort by ${btn.textContent.trim()}, currently ${ascending ? "ascending" : "descending"}`
          );
        }
      } else {
        th.removeAttribute("aria-sort");
        if (btn) {
          btn.setAttribute("aria-label", `Sort by ${btn.textContent.trim()}`);
        }
      }
    });
  };

  const applySort = () => {
    const rows = Array.from(tbody.querySelectorAll("tr"));
    rows.sort(compare);
    rows.forEach((row) => tbody.appendChild(row));
    updateSortUi();
  };

  headers.forEach((th) => {
    const btn = th.querySelector("button.sort-btn") || th;
    btn.addEventListener("click", () => {
      const key = th.getAttribute("data-sort") || "name";
      if (sortKey === key) ascending = !ascending;
      else {
        sortKey = key;
        ascending = true;
      }
      applySort();
    });
  });

  updateSortUi();
})();
