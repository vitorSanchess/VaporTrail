(() => {
  "use strict";

  const THEME_STORAGE_KEY = "vaportrail-theme";
  const RECENT_STORAGE_KEY = "vaportrail-recent";
  const MAX_RECENT = 5;
  const CHART_COLORS = [
    "var(--chart-1)",
    "var(--chart-2)",
    "var(--chart-3)",
    "var(--chart-4)",
    "var(--chart-5)",
    "var(--chart-6)"
  ];

  const el = {
    themeToggle: document.getElementById("theme-toggle"),
    modeBadge: document.getElementById("mode-badge"),
    modeLabel: document.getElementById("mode-label"),
    apiKeyHelp: document.getElementById("api-key-help"),
    helpDrawerTitle: document.getElementById("help-drawer-title"),
    apiKeySetupBlock: document.getElementById("api-key-setup-block"),
    closeHelpBtn: document.getElementById("close-help-btn"),
    catalogStatusText: document.getElementById("catalog-status-text"),
    syncCatalogBtn: document.getElementById("sync-catalog-btn"),

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

    standingSection: document.getElementById("standing-section"),
    standingPill: document.getElementById("standing-pill"),
    standingDetail: document.getElementById("standing-detail"),
    socialCounters: document.getElementById("social-counters"),
    metaBadges: document.getElementById("meta-badges"),
    metaFriends: document.getElementById("meta-friends"),
    progressionCol: document.getElementById("progression-col"),
    xpLevelLabel: document.getElementById("xp-level-label"),
    xpRemaining: document.getElementById("xp-remaining"),
    xpTrack: document.querySelector(".xp-track"),
    xpFill: document.getElementById("xp-fill"),
    xpTotal: document.getElementById("xp-total"),
    xpPercent: document.getElementById("xp-percent"),

    engagementSection: document.getElementById("engagement-section"),
    engagementSummary: document.getElementById("engagement-summary"),
    engUnplayedPct: document.getElementById("eng-unplayed-pct"),
    engUnplayedCount: document.getElementById("eng-unplayed-count"),
    engTop3Share: document.getElementById("eng-top3-share"),
    engTop3Hours: document.getElementById("eng-top3-hours"),
    engMedianHours: document.getElementById("eng-median-hours"),
    depthTrack: document.getElementById("depth-track"),
    depthLegend: document.getElementById("depth-legend"),
    backlogHours: document.getElementById("backlog-hours"),
    backlogSub: document.getElementById("backlog-sub"),
    backlogPicksWrap: document.getElementById("backlog-picks-wrap"),
    backlogPicks: document.getElementById("backlog-picks"),

    breakdownSection: document.getElementById("breakdown-section"),
    breakdownPlaytimeBtn: document.getElementById("breakdown-playtime"),
    breakdownCountBtn: document.getElementById("breakdown-count"),
    breakdownCoverage: document.getElementById("breakdown-coverage"),
    genreDonut: document.getElementById("genre-donut"),
    donutCenterValue: document.getElementById("donut-center-value"),
    donutCenterLabel: document.getElementById("donut-center-label"),
    genreList: document.getElementById("genre-list"),
    developersCard: document.getElementById("developers-card"),
    developersList: document.getElementById("developers-list"),
    tagsCard: document.getElementById("tags-card"),
    tagsList: document.getElementById("tags-list"),

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
  let breakdownMetric = "playtime";

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
  let canToggleHelpDrawer = false;

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

      const canSync = Boolean(status.allowManualCatalogSync);
      canToggleHelpDrawer = Boolean(status.isDemoMode || canSync);

      el.apiKeySetupBlock.classList.toggle("hidden", !status.isDemoMode);
      el.helpDrawerTitle.textContent = status.isDemoMode ? "Connect a Steam API key" : "Local catalog";
      el.syncCatalogBtn.classList.toggle("hidden", !canSync);

      if (!canToggleHelpDrawer) {
        el.apiKeyHelp.classList.add("hidden");
        el.modeBadge.disabled = true;
        el.modeBadge.classList.add("is-static");
        el.modeBadge.removeAttribute("aria-expanded");
        el.modeBadge.title = "Connected to Steam Web API";
      } else {
        el.modeBadge.disabled = false;
        el.modeBadge.classList.remove("is-static");
        el.modeBadge.setAttribute("aria-expanded", String(!el.apiKeyHelp.classList.contains("hidden")));
        el.modeBadge.title = status.isDemoMode ? "Steam API key setup" : "Local catalog status";
      }

      renderCatalogStatus(status.catalog);

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

  function renderCatalogStatus(catalog) {
    const syncVisible = !el.syncCatalogBtn.classList.contains("hidden");

    if (!catalog) {
      el.catalogStatusText.textContent = "Catalog: using built-in seed";
      return;
    }

    if (catalog.isSyncing) {
      el.catalogStatusText.textContent = "Catalog: syncing steamdb dump...";
      el.syncCatalogBtn.disabled = true;
      return;
    }

    el.syncCatalogBtn.disabled = !syncVisible;
    if (catalog.isReady && catalog.gameCount > 0) {
      el.catalogStatusText.textContent = `Catalog: ${formatNumber(catalog.gameCount)} games cached`;
    } else if (catalog.lastError) {
      el.catalogStatusText.textContent = `Catalog: using built-in seed (${catalog.lastError})`;
    } else {
      el.catalogStatusText.textContent = "Catalog: using built-in seed";
    }
  }

  async function syncCatalog() {
    el.syncCatalogBtn.disabled = true;
    el.catalogStatusText.textContent = "Catalog: syncing steamdb dump...";
    try {
      const res = await fetch("/api/catalog/sync?force=true", { method: "POST" });
      if (!res.ok) throw new Error("Sync failed");
      const catalog = await res.json();
      renderCatalogStatus(catalog);
      if (currentQuery) {
        runLookup(currentQuery, { refresh: true });
      }
    } catch {
      el.catalogStatusText.textContent = "Catalog sync failed. Try again later.";
      el.syncCatalogBtn.disabled = false;
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

    // Standing & Progression
    renderStanding(p);
    renderEngagement(p);

    // Private notice vs Breakdown & Games showcase
    const topGames = Array.isArray(p.topPlayedGames) ? p.topPlayedGames : [];
    const recentGames = Array.isArray(p.recentlyPlayed) ? p.recentlyPlayed : [];

    el.privateNotice.classList.toggle("hidden", !p.gamesArePrivate);

    renderBreakdown(p);

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

  function renderStanding(p) {
    const standing = p.standing;
    const prog = p.progression;

    if (!standing && !prog) {
      el.standingSection.classList.add("hidden");
      return;
    }

    el.standingSection.classList.remove("hidden");

    if (standing) {
      if (standing.isGoodStanding) {
        el.standingPill.textContent = "Good standing";
        el.standingPill.className = "standing-pill is-clean";
        el.standingDetail.textContent = "";
        el.standingDetail.classList.add("hidden");
      } else {
        const flags = [];
        if (standing.vacBanCount > 0 || standing.vacBanned) {
          flags.push(`${standing.vacBanCount || 1} VAC ban${standing.vacBanCount === 1 ? "" : "s"}`);
        }
        if (standing.gameBanCount > 0) {
          flags.push(`${standing.gameBanCount} game ban${standing.gameBanCount === 1 ? "" : "s"}`);
        }
        if (standing.communityBanned) {
          flags.push("Community banned");
        }
        if (standing.economyBan && standing.economyBan.toLowerCase() !== "none") {
          flags.push(`Economy: ${standing.economyBan}`);
        }
        el.standingPill.textContent = flags.join(", ") || "Restricted";
        el.standingPill.className = "standing-pill is-flagged";

        if (typeof standing.daysSinceLastBan === "number") {
          el.standingDetail.textContent = `${formatNumber(standing.daysSinceLastBan)}d since last ban`;
          el.standingDetail.classList.remove("hidden");
        } else {
          el.standingDetail.textContent = "";
          el.standingDetail.classList.add("hidden");
        }
      }
    } else {
      el.standingPill.textContent = "Standing unavailable";
      el.standingPill.className = "standing-pill";
      el.standingDetail.classList.add("hidden");
    }

    let hasSocial = false;
    if (typeof prog?.badgeCount === "number") {
      el.metaBadges.textContent = `${formatNumber(prog.badgeCount)} badges`;
      el.metaBadges.classList.remove("hidden");
      hasSocial = true;
    } else {
      el.metaBadges.classList.add("hidden");
    }

    if (typeof prog?.friendCount === "number") {
      el.metaFriends.textContent = `${formatNumber(prog.friendCount)} friends`;
      el.metaFriends.classList.remove("hidden");
      hasSocial = true;
    } else {
      el.metaFriends.classList.add("hidden");
    }
    el.socialCounters.classList.toggle("hidden", !hasSocial);

    if (prog && typeof prog.playerXp === "number") {
      const level = typeof p.steamLevel === "number" ? p.steamLevel : null;
      el.xpLevelLabel.textContent = level !== null ? `Level ${level} to ${level + 1}` : "XP progress";
      el.xpRemaining.textContent = typeof prog.xpNeededToLevelUp === "number"
        ? `${formatNumber(prog.xpNeededToLevelUp)} XP needed`
        : "";

      const pct = typeof prog.levelProgressPercent === "number"
        ? Math.max(0, Math.min(100, prog.levelProgressPercent))
        : 0;
      el.xpFill.style.width = `${pct}%`;
      el.xpTrack.setAttribute("aria-valuenow", String(pct));
      el.xpTotal.textContent = `${formatNumber(prog.playerXp)} XP`;
      el.xpPercent.textContent = `${pct}%`;
      el.progressionCol.classList.remove("hidden");
    } else {
      el.progressionCol.classList.add("hidden");
    }
  }

  function renderEngagement(p) {
    const eng = p.engagement;
    if (!eng || p.gamesArePrivate) {
      el.engagementSection.classList.add("hidden");
      return;
    }

    el.engagementSection.classList.remove("hidden");
    el.engagementSummary.textContent = `${formatNumber(eng.unplayedGamesCount)} of ${formatNumber(eng.totalGamesCount)} games unplayed`;

    el.engUnplayedPct.textContent = `${eng.unplayedPercent}%`;
    el.engUnplayedCount.textContent = `${formatNumber(eng.unplayedGamesCount)} games`;

    el.engTop3Share.textContent = `${eng.top3PlaytimeSharePercent}%`;
    el.engTop3Hours.textContent = `${formatHours(eng.top3PlaytimeHours)}h combined`;

    el.engMedianHours.textContent = typeof eng.medianPlayedHours === "number"
      ? `${formatHours(eng.medianPlayedHours)}h`
      : "--";

    const depthColors = {
      unplayed: "var(--depth-unplayed)",
      sampled: "var(--depth-sampled)",
      played: "var(--depth-played)",
      dedicated: "var(--depth-dedicated)"
    };

    el.depthTrack.innerHTML = "";
    el.depthLegend.innerHTML = "";

    (eng.depthBuckets || []).forEach((bucket) => {
      const color = depthColors[bucket.key] || "var(--accent)";

      if (bucket.gameCount > 0 && bucket.sharePercent > 0) {
        const seg = document.createElement("div");
        seg.className = "depth-seg";
        seg.style.width = `${bucket.sharePercent}%`;
        seg.style.backgroundColor = color;
        seg.title = `${bucket.label}: ${formatNumber(bucket.gameCount)} (${bucket.sharePercent}%)`;
        el.depthTrack.appendChild(seg);
      }

      const item = document.createElement("span");
      item.className = "depth-legend-item";

      const swatch = document.createElement("span");
      swatch.className = "depth-swatch";
      swatch.style.backgroundColor = color;

      const label = document.createElement("span");
      label.textContent = bucket.label;

      const count = document.createElement("span");
      count.className = "depth-legend-count";
      count.textContent = formatNumber(bucket.gameCount);

      item.appendChild(swatch);
      item.appendChild(label);
      item.appendChild(count);
      el.depthLegend.appendChild(item);
    });

    if (eng.unplayedGamesCount === 0) {
      el.backlogHours.textContent = "0h";
      el.backlogSub.textContent = "All owned games played";
    } else if (typeof eng.estimatedBacklogHours === "number") {
      el.backlogHours.textContent = `${formatHours(eng.estimatedBacklogHours)}h`;
      el.backlogSub.textContent = eng.backlogMatchedGamesCount < eng.unplayedGamesCount
        ? `Main story across ${formatNumber(eng.backlogMatchedGamesCount)} of ${formatNumber(eng.unplayedGamesCount)} unplayed games`
        : `Main story across ${formatNumber(eng.backlogMatchedGamesCount)} unplayed games`;
    } else {
      el.backlogHours.textContent = "--";
      el.backlogSub.textContent = "No HowLongToBeat times matched";
    }

    el.backlogPicks.innerHTML = "";
    const picks = Array.isArray(eng.quickestToBeat) ? eng.quickestToBeat : [];
    if (picks.length === 0) {
      el.backlogPicksWrap.classList.add("hidden");
    } else {
      el.backlogPicksWrap.classList.remove("hidden");
      picks.forEach((pick) => {
        const link = document.createElement("a");
        link.className = "backlog-pick";
        link.href = pick.storeUrl || `https://store.steampowered.com/app/${pick.appId}`;
        link.target = "_blank";
        link.rel = "noopener noreferrer";

        const name = document.createElement("span");
        name.className = "backlog-pick-name";
        name.textContent = pick.name;

        const hours = document.createElement("span");
        hours.className = "backlog-pick-hours";
        hours.textContent = `${formatHours(pick.hltbMainHours)}h`;

        link.appendChild(name);
        link.appendChild(hours);
        el.backlogPicks.appendChild(link);
      });
    }
  }

  function renderBreakdown(p) {
    const bd = p.breakdown;
    if (!bd || p.gamesArePrivate) {
      el.breakdownSection.classList.add("hidden");
      return;
    }

    const slices = breakdownMetric === "playtime" ? bd.genresByPlaytime : bd.genresByCount;
    if (!Array.isArray(slices) || slices.length === 0) {
      el.breakdownSection.classList.add("hidden");
      return;
    }

    el.breakdownSection.classList.remove("hidden");

    const isPlaytime = breakdownMetric === "playtime";
    el.breakdownPlaytimeBtn.classList.toggle("active", isPlaytime);
    el.breakdownPlaytimeBtn.setAttribute("aria-selected", String(isPlaytime));
    el.breakdownCountBtn.classList.toggle("active", !isPlaytime);
    el.breakdownCountBtn.setAttribute("aria-selected", String(!isPlaytime));

    if (bd.matchedGamesCount < bd.analyzedGamesCount) {
      el.breakdownCoverage.textContent = `${formatNumber(bd.matchedGamesCount)} of ${formatNumber(bd.analyzedGamesCount)} games matched`;
      el.breakdownCoverage.classList.remove("hidden");
    } else {
      el.breakdownCoverage.textContent = "";
      el.breakdownCoverage.classList.add("hidden");
    }

    renderGenreDonut(slices, breakdownMetric);
    const developers = isPlaytime
      ? (bd.topDevelopers || [])
      : (bd.topDevelopersByCount || bd.topDevelopers || []);
    const tags = isPlaytime
      ? (bd.topTags || [])
      : (bd.topTagsByCount || bd.topTags || []);
    renderDevelopers(developers, breakdownMetric);
    renderTags(tags, breakdownMetric);
  }

  function renderGenreDonut(slices, mode) {
    const svgNs = "http://www.w3.org/2000/svg";
    el.genreDonut.innerHTML = "";
    el.genreDonut.classList.remove("has-focus");
    el.genreList.innerHTML = "";

    const radius = 74;
    const circumference = 2 * Math.PI * radius;
    const gap = slices.length > 1 ? 2.5 : 0;

    const totalShare = slices.reduce((sum, s) => sum + (s.sharePercent || 0), 0) || 100;
    const totalHours = slices.reduce((sum, s) => sum + (s.playtimeHours || 0), 0);
    const totalCount = slices.reduce((sum, s) => sum + (s.gameCount || 0), 0);

    const defaultValue = mode === "playtime"
      ? `${formatHours(totalHours)}h`
      : formatNumber(totalCount);
    const defaultLabel = mode === "playtime" ? "genre hours" : "genre tags";

    el.donutCenterValue.textContent = defaultValue;
    el.donutCenterLabel.textContent = defaultLabel;

    let offset = 0;
    const sliceEls = [];
    const rowEls = [];

    function setActive(index) {
      const hasFocus = index >= 0;
      el.genreDonut.classList.toggle("has-focus", hasFocus);
      sliceEls.forEach((s, i) => s.classList.toggle("is-active", i === index));
      rowEls.forEach((r, i) => r.classList.toggle("is-active", i === index));

      if (hasFocus) {
        const s = slices[index];
        el.donutCenterValue.textContent = `${s.sharePercent}%`;
        el.donutCenterLabel.textContent = s.name;
      } else {
        el.donutCenterValue.textContent = defaultValue;
        el.donutCenterLabel.textContent = defaultLabel;
      }
    }

    slices.forEach((slice, idx) => {
      const color = CHART_COLORS[idx % CHART_COLORS.length];
      const fraction = (slice.sharePercent || 0) / totalShare;
      const rawLength = fraction * circumference;
      const visibleLength = Math.max(0, rawLength - gap);

      const circle = document.createElementNS(svgNs, "circle");
      circle.setAttribute("class", "donut-slice");
      circle.setAttribute("cx", "100");
      circle.setAttribute("cy", "100");
      circle.setAttribute("r", String(radius));
      circle.setAttribute("stroke", color);
      circle.setAttribute("stroke-dasharray", `${visibleLength} ${circumference - visibleLength}`);
      circle.setAttribute("stroke-dashoffset", String(-offset));
      circle.setAttribute("transform", "rotate(-90 100 100)");
      circle.setAttribute("tabindex", "0");
      circle.setAttribute("aria-label", `${slice.name}: ${slice.sharePercent}%`);

      circle.addEventListener("mouseenter", () => setActive(idx));
      circle.addEventListener("mouseleave", () => setActive(-1));
      circle.addEventListener("focus", () => setActive(idx));
      circle.addEventListener("blur", () => setActive(-1));

      el.genreDonut.appendChild(circle);
      sliceEls.push(circle);
      offset += rawLength;

      const row = document.createElement("button");
      row.type = "button";
      row.className = "legend-row";

      const swatch = document.createElement("span");
      swatch.className = "legend-swatch";
      swatch.style.backgroundColor = color;

      const name = document.createElement("span");
      name.className = "legend-name";
      name.textContent = slice.name;

      const metric = document.createElement("span");
      metric.className = "legend-metric";
      metric.textContent = mode === "playtime"
        ? `${formatHours(slice.playtimeHours)}h`
        : `${formatNumber(slice.gameCount)}g`;

      const share = document.createElement("span");
      share.className = "legend-share";
      share.textContent = `${slice.sharePercent}%`;

      row.appendChild(swatch);
      row.appendChild(name);
      row.appendChild(metric);
      row.appendChild(share);

      row.addEventListener("mouseenter", () => setActive(idx));
      row.addEventListener("mouseleave", () => setActive(-1));
      row.addEventListener("focus", () => setActive(idx));
      row.addEventListener("blur", () => setActive(-1));

      el.genreList.appendChild(row);
      rowEls.push(row);
    });
  }

  function renderDevelopers(developers, mode) {
    el.developersList.innerHTML = "";
    if (!Array.isArray(developers) || developers.length === 0) {
      el.developersCard.classList.add("hidden");
      return;
    }

    el.developersCard.classList.remove("hidden");
    const maxHours = Math.max(...developers.map((d) => d.playtimeHours || 0));
    const maxCount = Math.max(...developers.map((d) => d.gameCount || 0));
    const useHours = mode === "playtime" && maxHours > 0;
    const maxVal = useHours ? maxHours : Math.max(1, maxCount);

    developers.forEach((dev) => {
      const row = document.createElement("div");
      row.className = "dev-row";

      const top = document.createElement("div");
      top.className = "dev-row-top";

      const name = document.createElement("span");
      name.className = "dev-name";
      name.textContent = dev.name;

      const metric = document.createElement("span");
      metric.className = "dev-metric";
      metric.textContent = useHours
        ? `${formatHours(dev.playtimeHours)}h`
        : `${formatNumber(dev.gameCount)} ${dev.gameCount === 1 ? "game" : "games"}`;

      top.appendChild(name);
      top.appendChild(metric);

      const track = document.createElement("div");
      track.className = "bar-track";

      const fill = document.createElement("div");
      fill.className = "bar-fill";
      const val = useHours ? dev.playtimeHours : dev.gameCount;
      fill.style.width = `${Math.max(4, Math.round((val / maxVal) * 100))}%`;

      track.appendChild(fill);
      row.appendChild(top);
      row.appendChild(track);
      el.developersList.appendChild(row);
    });
  }

  function renderTags(tags, mode) {
    el.tagsList.innerHTML = "";
    if (!Array.isArray(tags) || tags.length === 0) {
      el.tagsCard.classList.add("hidden");
      return;
    }

    el.tagsCard.classList.remove("hidden");
    tags.forEach((tag) => {
      const chip = document.createElement("span");
      chip.className = "tag-chip";

      const label = document.createElement("span");
      label.textContent = tag.name;

      const metric = document.createElement("span");
      metric.className = "tag-chip-metric";
      if (mode === "playtime" && tag.playtimeHours > 0) {
        metric.textContent = `${formatHours(tag.playtimeHours)}h`;
      } else {
        metric.textContent = formatNumber(tag.gameCount);
        chip.title = `${formatNumber(tag.gameCount)} ${tag.gameCount === 1 ? "game" : "games"}`;
      }

      chip.appendChild(label);
      chip.appendChild(metric);
      el.tagsList.appendChild(chip);
    });
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
    if (!canToggleHelpDrawer) return;
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

  el.breakdownPlaytimeBtn.addEventListener("click", () => {
    breakdownMetric = "playtime";
    if (currentProfile) renderBreakdown(currentProfile);
  });

  el.breakdownCountBtn.addEventListener("click", () => {
    breakdownMetric = "count";
    if (currentProfile) renderBreakdown(currentProfile);
  });

  el.syncCatalogBtn.addEventListener("click", syncCatalog);

  // ========== Boot ==========
  syncThemeSwitch();
  renderRecentChips();
  loadServerStatus();
})();
