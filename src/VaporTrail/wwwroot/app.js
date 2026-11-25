(() => {
  "use strict";

  const THEME_STORAGE_KEY = "vaportrail-theme";
  const RECENT_STORAGE_KEY = "vaportrail-recent";
  const MAX_RECENT = 5;

  const el = {
    themeToggle: document.getElementById("theme-toggle"),
    modeBadge: document.getElementById("mode-badge"),
    modeLabel: document.getElementById("mode-label"),
    apiKeyHelp: document.getElementById("api-key-help"),
    closeHelpBtn: document.getElementById("close-help-btn"),

    searchForm: document.getElementById("search-form"),
    queryInput: document.getElementById("steam-query"),
    lookupBtn: document.getElementById("lookup-btn"),

    sampleRow: document.getElementById("sample-row"),
    sampleChips: document.getElementById("sample-chips"),
    recentRow: document.getElementById("recent-row"),
    recentChips: document.getElementById("recent-chips"),
    clearRecentBtn: document.getElementById("clear-recent-btn"),

    errorBanner: document.getElementById("error-banner"),
    errorMessage: document.getElementById("error-message"),
    loadingState: document.getElementById("loading-state"),
    emptyState: document.getElementById("empty-state"),
    profileView: document.getElementById("profile-view"),

    avatar: document.getElementById("profile-avatar"),
    avatarStatusDot: document.getElementById("avatar-status-dot"),
    personaName: document.getElementById("profile-name"),
    levelBadge: document.getElementById("level-badge"),
    levelValue: document.getElementById("profile-level"),
    realName: document.getElementById("profile-realname"),
    statusIndicator: document.getElementById("status-indicator"),
    currentGame: document.getElementById("current-game"),
    metaCountry: document.getElementById("meta-country"),
    metaMember: document.getElementById("meta-member"),
    metaSteamId: document.getElementById("meta-steamid"),
    copySteamIdBtn: document.getElementById("copy-steamid-btn"),
    steamExternalLink: document.getElementById("steam-external-link"),
    cacheBadge: document.getElementById("cache-badge"),
    refreshBtn: document.getElementById("refresh-btn"),

    statGames: document.getElementById("stat-games"),
    statPlaytime: document.getElementById("stat-playtime"),
    statVisibility: document.getElementById("stat-visibility"),
    statLastSeen: document.getElementById("stat-lastseen"),

    privateNotice: document.getElementById("private-games-notice"),
    gamesSection: document.getElementById("games-section"),
    tabTop: document.getElementById("tab-top"),
    tabRecent: document.getElementById("tab-recent"),
    tabTopCount: document.getElementById("tab-top-count"),
    tabRecentCount: document.getElementById("tab-recent-count"),
    gamesCaption: document.getElementById("games-caption"),
    gamesGrid: document.getElementById("games-grid"),
    gamesEmpty: document.getElementById("games-empty")
  };

  let currentProfile = null;
  let currentQuery = "";
  let activeGameTab = "top";

  const regionNames = typeof Intl !== "undefined" && Intl.DisplayNames
    ? new Intl.DisplayNames([navigator.language || "en"], { type: "region" })
    : null;

  // ========== Theme Management ==========
  function syncThemeSwitch() {
    const isDark = document.documentElement.getAttribute("data-theme") === "dark";
    el.themeToggle.setAttribute("aria-checked", String(isDark));
    el.themeToggle.title = isDark ? "Switch to Light Neumorphism" : "Switch to Dark Neumorphism";
  }

  function toggleTheme() {
    const current = document.documentElement.getAttribute("data-theme");
    const next = current === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    localStorage.setItem(THEME_STORAGE_KEY, next);
    syncThemeSwitch();
  }

  // ========== Recent Lookups ==========
  function loadRecent() {
    try {
      const raw = localStorage.getItem(RECENT_STORAGE_KEY);
      const parsed = raw ? JSON.parse(raw) : [];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  function saveRecent(steamId, personaName) {
    const items = loadRecent().filter((item) => item.steamId !== steamId);
    items.unshift({ steamId, personaName });
    localStorage.setItem(RECENT_STORAGE_KEY, JSON.stringify(items.slice(0, MAX_RECENT)));
    renderRecentChips();
  }

  function renderRecentChips() {
    const items = loadRecent();
    el.recentChips.innerHTML = "";

    if (items.length === 0) {
      el.recentRow.classList.add("hidden");
      return;
    }

    el.recentRow.classList.remove("hidden");
    items.forEach((item) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "chip-btn";
      btn.textContent = item.personaName || item.steamId;
      btn.title = `SteamID: ${item.steamId}`;
      btn.addEventListener("click", () => runLookup(item.steamId));
      el.recentChips.appendChild(btn);
    });
  }

  // ========== Server Status & Sample Chips ==========
  async function loadServerStatus() {
    try {
      const res = await fetch("/api/status");
      if (!res.ok) throw new Error("Status unavailable");
      const status = await res.json();

      if (status.isDemoMode) {
        el.modeBadge.classList.remove("is-live");
        el.modeLabel.textContent = "Demo mode";
      } else {
        el.modeBadge.classList.add("is-live");
        el.modeLabel.textContent = "Steam API";
      }

      if (Array.isArray(status.sampleProfiles) && status.sampleProfiles.length > 0) {
        el.sampleChips.innerHTML = "";
        status.sampleProfiles.forEach((sample) => {
          const btn = document.createElement("button");
          btn.type = "button";
          btn.className = "chip-btn";
          btn.textContent = sample.label;
          btn.title = sample.description || sample.query;
          btn.addEventListener("click", () => runLookup(sample.query));
          el.sampleChips.appendChild(btn);
        });
        el.sampleRow.classList.remove("hidden");
      }
    } catch {
      el.modeLabel.textContent = "Status unavailable";
    }
  }

  // ========== Profile Lookup ==========
  async function runLookup(query, { refresh = false } = {}) {
    const trimmed = (query || "").trim();
    if (!trimmed) {
      showError("Enter a SteamID64, vanity name, or Steam profile URL.");
      return;
    }

    currentQuery = trimmed;
    el.queryInput.value = trimmed;
    hideError();
    setLoading(true);

    try {
      const params = new URLSearchParams({ query: trimmed });
      if (refresh) params.set("refresh", "true");

      const response = await fetch(`/api/steam/profile?${params.toString()}`);
      const payload = await response.json().catch(() => null);

      if (!response.ok) {
        const message = payload?.detail || payload?.title || `Request failed (HTTP ${response.status}).`;
        showError(message);
        if (!currentProfile) {
          el.emptyState.classList.remove("hidden");
        }
        return;
      }

      currentProfile = payload;
      saveRecent(payload.steamId, payload.personaName);
      renderProfile(payload);
    } catch {
      showError("Could not reach the server. Check that it is running and try again.");
    } finally {
      setLoading(false);
    }
  }

  function setLoading(isLoading) {
    el.lookupBtn.disabled = isLoading;
    el.lookupBtn.querySelector(".btn-label").textContent = isLoading ? "Looking up..." : "Look up profile";
    el.loadingState.classList.toggle("hidden", !isLoading);

    if (isLoading) {
      el.emptyState.classList.add("hidden");
      el.profileView.classList.add("hidden");
    }
  }

  function showError(message) {
    el.errorMessage.textContent = message;
    el.errorBanner.classList.remove("hidden");
  }

  function hideError() {
    el.errorBanner.classList.add("hidden");
  }

  // ========== Rendering ==========
  function renderProfile(p) {
    el.emptyState.classList.add("hidden");
    el.profileView.classList.remove("hidden");

    // Avatar + fallback
    el.avatar.src = p.avatarUrl || buildFallbackAvatar(p.personaName);
    el.avatar.alt = `${p.personaName}'s Steam avatar`;
    el.avatar.onerror = () => {
      el.avatar.onerror = null;
      el.avatar.src = buildFallbackAvatar(p.personaName);
    };

    const statusClass = `status-${p.statusKind || "offline"}`;
    el.avatarStatusDot.className = `avatar-status-dot ${statusClass}`;
    el.avatarStatusDot.title = p.statusText;

    el.personaName.textContent = p.personaName;

    if (typeof p.steamLevel === "number") {
      el.levelValue.textContent = String(p.steamLevel);
      el.levelBadge.classList.remove("hidden");
    } else {
      el.levelBadge.classList.add("hidden");
    }

    if (p.realName) {
      el.realName.textContent = p.realName;
      el.realName.classList.remove("hidden");
    } else {
      el.realName.classList.add("hidden");
    }

    el.statusIndicator.textContent = p.statusText;
    el.statusIndicator.className = `status-pill ${statusClass}`;

    if (p.currentGameName) {
      el.currentGame.textContent = p.currentGameName;
      el.currentGame.classList.remove("hidden");
    } else {
      el.currentGame.classList.add("hidden");
    }

    // Metadata line
    if (p.countryCode) {
      const countryLabel = resolveCountryName(p.countryCode);
      el.metaCountry.textContent = countryLabel;
      el.metaCountry.classList.remove("hidden");
    } else {
      el.metaCountry.classList.add("hidden");
    }

    if (p.memberSinceUtc) {
      const d = new Date(p.memberSinceUtc);
      el.metaMember.textContent = `Member since ${d.toLocaleDateString(undefined, { month: "short", year: "numeric" })}`;
      el.metaMember.classList.remove("hidden");
    } else {
      el.metaMember.classList.add("hidden");
    }

    el.metaSteamId.textContent = p.steamId;
    el.steamExternalLink.href = p.profileUrl || `https://steamcommunity.com/profiles/${p.steamId}`;

    el.cacheBadge.textContent = p.fromCache ? "Cached" : "Fresh";

    // Stats strip
    if (p.gamesArePrivate) {
      el.statGames.textContent = "Private";
      el.statPlaytime.textContent = "Private";
    } else {
      el.statGames.textContent = formatNumber(p.totalOwnedGames ?? 0);
      el.statPlaytime.textContent = `${formatHours(p.totalPlaytimeHours ?? 0)}h`;
    }

    el.statVisibility.textContent = p.isPublic ? "Public" : "Private";
    if (p.statusKind === "ingame" || p.statusKind === "online") {
      el.statLastSeen.textContent = "Currently active";
      el.statLastSeen.classList.remove("hidden");
    } else if (p.lastLogoffUtc) {
      el.statLastSeen.textContent = `Last seen ${formatRelativeTime(p.lastLogoffUtc)}`;
      el.statLastSeen.classList.remove("hidden");
    } else {
      el.statLastSeen.textContent = "";
      el.statLastSeen.classList.add("hidden");
    }

    // Private notice vs Games showcase
    const topGames = Array.isArray(p.topPlayedGames) ? p.topPlayedGames : [];
    const recentGames = Array.isArray(p.recentlyPlayed) ? p.recentlyPlayed : [];

    el.privateNotice.classList.toggle("hidden", !p.gamesArePrivate);

    if (p.gamesArePrivate && recentGames.length === 0) {
      el.gamesSection.classList.add("hidden");
    } else {
      el.gamesSection.classList.remove("hidden");
      el.tabTopCount.textContent = String(topGames.length);
      el.tabRecentCount.textContent = String(recentGames.length);

      // Default to whichever tab has content (preferring Top Played).
      activeGameTab = topGames.length > 0 ? "top" : "recent";
      renderGameTab();
    }
  }

  function renderGameTab() {
    if (!currentProfile) return;

    const isTop = activeGameTab === "top";
    el.tabTop.classList.toggle("active", isTop);
    el.tabTop.setAttribute("aria-selected", String(isTop));
    el.tabRecent.classList.toggle("active", !isTop);
    el.tabRecent.setAttribute("aria-selected", String(!isTop));

    const games = isTop
      ? (currentProfile.topPlayedGames || [])
      : (currentProfile.recentlyPlayed || []);

    if (isTop) {
      el.gamesCaption.textContent = "";
      el.gamesCaption.classList.add("hidden");
    } else {
      el.gamesCaption.textContent = "Past 2 weeks";
      el.gamesCaption.classList.remove("hidden");
    }

    el.gamesGrid.innerHTML = "";

    if (games.length === 0) {
      el.gamesEmpty.textContent = isTop
        ? "No owned games visible for this account."
        : "No games played in the past 2 weeks.";
      el.gamesEmpty.classList.remove("hidden");
      return;
    }

    el.gamesEmpty.classList.add("hidden");

    games.forEach((game) => {
      const card = document.createElement("a");
      card.className = "game-card";
      card.href = game.storeUrl || `https://store.steampowered.com/app/${game.appId}`;
      card.target = "_blank";
      card.rel = "noopener noreferrer";

      const bannerWrap = document.createElement("div");
      bannerWrap.className = "game-banner-wrap";

      const img = document.createElement("img");
      img.className = "game-banner";
      img.loading = "lazy";
      img.alt = game.name;
      img.src = game.headerImageUrl;
      img.onerror = () => {
        img.onerror = null;
        img.src = buildFallbackGameBanner(game.name, game.appId);
      };
      bannerWrap.appendChild(img);

      const body = document.createElement("div");
      body.className = "game-body";

      const title = document.createElement("div");
      title.className = "game-title";
      title.textContent = game.name;

      const hoursRow = document.createElement("div");
      hoursRow.className = "game-hours-row";

      const totalSpan = document.createElement("span");
      totalSpan.textContent = `${formatHours(game.playtimeHours)}h total`;
      hoursRow.appendChild(totalSpan);

      if (typeof game.recentPlaytimeHours === "number" && game.recentPlaytimeHours > 0) {
        const recentPill = document.createElement("span");
        recentPill.className = "recent-pill";
        recentPill.textContent = `${formatHours(game.recentPlaytimeHours)}h / 2w`;
        hoursRow.appendChild(recentPill);
      }

      body.appendChild(title);
      body.appendChild(hoursRow);
      card.appendChild(bannerWrap);
      card.appendChild(body);
      el.gamesGrid.appendChild(card);
    });
  }

  // ========== Formatting & Fallbacks ==========
  function resolveCountryName(code) {
    try {
      return regionNames ? `${regionNames.of(code.toUpperCase())} (${code.toUpperCase()})` : code.toUpperCase();
    } catch {
      return code.toUpperCase();
    }
  }

  function formatNumber(n) {
    return new Intl.NumberFormat().format(n);
  }

  function formatHours(h) {
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(h);
  }

  function formatRelativeTime(isoString) {
    const diffMs = Date.now() - new Date(isoString).getTime();
    const mins = Math.max(1, Math.round(diffMs / 60000));
    if (mins < 60) return `${mins}m ago`;
    const hours = Math.round(mins / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.round(hours / 24);
    if (days < 30) return `${days}d ago`;
    return new Date(isoString).toLocaleDateString();
  }

  function buildFallbackAvatar(name) {
    const initials = (name || "ST").replace(/[^a-zA-Z0-9]/g, "").slice(0, 2).toUpperCase() || "ST";
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
      <rect width="128" height="128" fill="#1e293b"/>
      <text x="64" y="76" text-anchor="middle" fill="#f8fafc" font-family="system-ui,sans-serif" font-size="44" font-weight="700">${initials}</text>
    </svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  function buildFallbackGameBanner(title, appId) {
    const fills = ["#1e293b", "#1e3a5f", "#1f3a3d", "#334155", "#27303f"];
    const bg = fills[Math.abs(appId || 0) % fills.length];
    const safeTitle = (title || "Steam Game").replace(/[<>&"]/g, "").slice(0, 24);
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 460 215">
      <rect width="460" height="215" fill="${bg}"/>
      <text x="28" y="122" fill="#f8fafc" font-family="system-ui,sans-serif" font-size="26" font-weight="700">${safeTitle}</text>
      <text x="28" y="156" fill="rgba(248,250,252,0.72)" font-family="ui-monospace,monospace" font-size="14">STEAM APP #${appId || ""}</text>
    </svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  // ========== Event Wiring ==========
  el.themeToggle.addEventListener("click", toggleTheme);

  el.modeBadge.addEventListener("click", () => {
    const isHidden = el.apiKeyHelp.classList.toggle("hidden");
    el.modeBadge.setAttribute("aria-expanded", String(!isHidden));
  });

  el.closeHelpBtn.addEventListener("click", () => {
    el.apiKeyHelp.classList.add("hidden");
    el.modeBadge.setAttribute("aria-expanded", "false");
  });

  el.searchForm.addEventListener("submit", (e) => {
    e.preventDefault();
    runLookup(el.queryInput.value);
  });

  el.clearRecentBtn.addEventListener("click", () => {
    localStorage.removeItem(RECENT_STORAGE_KEY);
    renderRecentChips();
  });

  el.refreshBtn.addEventListener("click", () => {
    if (currentQuery) {
      runLookup(currentQuery, { refresh: true });
    }
  });

  el.copySteamIdBtn.addEventListener("click", async () => {
    if (!currentProfile?.steamId) return;
    try {
      await navigator.clipboard.writeText(currentProfile.steamId);
      const original = el.metaSteamId.textContent;
      el.metaSteamId.textContent = "Copied";
      setTimeout(() => {
        el.metaSteamId.textContent = original;
      }, 1200);
    } catch {
      // Clipboard API may be unavailable in non-HTTPS contexts.
    }
  });

  el.tabTop.addEventListener("click", () => {
    activeGameTab = "top";
    renderGameTab();
  });

  el.tabRecent.addEventListener("click", () => {
    activeGameTab = "recent";
    renderGameTab();
  });

  // ========== Boot ==========
  syncThemeSwitch();
  renderRecentChips();
  loadServerStatus();
})();
