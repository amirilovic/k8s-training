// Small slide engine shared by the three day decks.
// Keys: arrows / space / PageUp / PageDown to move, Home / End, N notes, O overview,
// T theme, F fullscreen, ? help. The slide number is kept in the URL hash (#12).
(() => {
  const stage = document.querySelector(".stage");
  const slides = [...stage.querySelectorAll(".slide")];
  const day = document.body.dataset.day || "";
  let index = 0;

  // Status line on top of each slide: day, clock time and block type from data-* attributes.
  slides.forEach((s) => {
    if (s.classList.contains("cover")) return;
    const bar = document.createElement("div");
    bar.className = "status";
    const kind = s.dataset.kind || "";
    bar.innerHTML =
      `<span>${day}</span>` +
      (s.dataset.time ? `<span>${s.dataset.time}</span>` : "") +
      (kind ? `<span class="kind-${kind}">${kind}</span>` : "") +
      (s.dataset.ref ? `<span>${s.dataset.ref}</span>` : "");
    s.prepend(bar);
  });

  const progress = document.createElement("div");
  progress.className = "progress";
  const counter = document.createElement("div");
  counter.className = "counter";
  const notes = document.createElement("div");
  notes.className = "notes-panel";
  notes.hidden = true;
  document.body.append(progress, counter, notes);

  function fit() {
    if (document.body.classList.contains("overview")) return;
    const scale = Math.min(window.innerWidth / 1600, window.innerHeight / 900);
    stage.style.transform = `translate(-50%, -50%) scale(${scale})`;
  }

  function show(i, push = true) {
    index = Math.max(0, Math.min(slides.length - 1, i));
    slides.forEach((s, n) => s.classList.toggle("active", n === index));
    progress.style.width = `${((index + 1) / slides.length) * 100}%`;
    counter.textContent = `${index + 1} / ${slides.length}`;
    const aside = slides[index].querySelector("aside.notes");
    notes.innerHTML = `<h4>Notes, slide ${index + 1}</h4>` + (aside ? aside.innerHTML : "<p>No notes.</p>");
    if (push) history.replaceState(null, "", `#${index + 1}`);
  }

  function toggleOverview() {
    const on = document.body.classList.toggle("overview");
    if (on) {
      stage.style.transform = "none";
      slides[index].scrollIntoView({ block: "center" });
    } else {
      fit();
    }
  }

  function toggleTheme() {
    const root = document.documentElement;
    const dark = root.dataset.theme === "dark";
    root.dataset.theme = dark ? "light" : "dark";
    try { localStorage.setItem("deck-theme", root.dataset.theme); } catch (e) { /* storage blocked */ }
  }

  let help = null;
  function toggleHelp() {
    if (help) { help.remove(); help = null; return; }
    help = document.createElement("div");
    help.className = "help";
    help.innerHTML = `<div class="card"><table>
      <tr><td><kbd>→</kbd> <kbd>Space</kbd></td><td>Next slide</td></tr>
      <tr><td><kbd>←</kbd></td><td>Previous slide</td></tr>
      <tr><td><kbd>Home</kbd> <kbd>End</kbd></td><td>First, last slide</td></tr>
      <tr><td><kbd>N</kbd></td><td>Speaker notes</td></tr>
      <tr><td><kbd>O</kbd></td><td>Overview of all slides</td></tr>
      <tr><td><kbd>T</kbd></td><td>Light or dark theme</td></tr>
      <tr><td><kbd>F</kbd></td><td>Fullscreen</td></tr>
      <tr><td><kbd>?</kbd></td><td>This help</td></tr></table></div>`;
    help.addEventListener("click", toggleHelp);
    document.body.append(help);
  }

  document.addEventListener("keydown", (e) => {
    if (e.metaKey || e.ctrlKey || e.altKey) return;
    const k = e.key;
    if (["ArrowRight", "ArrowDown", "PageDown", " "].includes(k)) { e.preventDefault(); show(index + 1); }
    else if (["ArrowLeft", "ArrowUp", "PageUp"].includes(k)) { e.preventDefault(); show(index - 1); }
    else if (k === "Home") show(0);
    else if (k === "End") show(slides.length - 1);
    else if (k === "n" || k === "N") notes.hidden = !notes.hidden;
    else if (k === "o" || k === "O" || k === "Escape" && document.body.classList.contains("overview")) toggleOverview();
    else if (k === "t" || k === "T") toggleTheme();
    else if (k === "f" || k === "F") {
      if (document.fullscreenElement) document.exitFullscreen?.();
      else document.documentElement.requestFullscreen?.().catch(() => {});
    }
    else if (k === "?") toggleHelp();
  });

  stage.addEventListener("click", (e) => {
    const slide = e.target.closest(".slide");
    if (document.body.classList.contains("overview")) {
      if (slide) { show(slides.indexOf(slide)); toggleOverview(); }
      return;
    }
    if (e.target.closest("a, pre, code, button, .info") || getSelection().toString()) return;
    show(e.clientX > window.innerWidth / 2 ? index + 1 : index - 1);
  });

  // Clickable diagrams: a [data-info] button shows the matching [data-for] block in its slide's .info panel.
  stage.querySelectorAll("[data-info]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const slide = btn.closest(".slide");
      slide.querySelectorAll("[data-info]").forEach((b) => b.classList.toggle("selected", b === btn));
      slide.querySelectorAll(".info [data-for]").forEach((p) => { p.hidden = p.dataset.for !== btn.dataset.info; });
    });
  });

  let touchX = null;
  stage.addEventListener("touchstart", (e) => { touchX = e.touches[0].clientX; }, { passive: true });
  stage.addEventListener("touchend", (e) => {
    if (touchX === null) return;
    const dx = e.changedTouches[0].clientX - touchX;
    if (Math.abs(dx) > 50) show(dx < 0 ? index + 1 : index - 1);
    touchX = null;
  });

  try {
    const saved = localStorage.getItem("deck-theme");
    if (saved) document.documentElement.dataset.theme = saved;
  } catch (e) { /* storage blocked */ }

  // Shrink code blocks that are wider than their column, then scale a slide's whole content
  // down when it is taller than the frame, so nothing is clipped on the projector.
  slides.forEach((s) => {
    const body = document.createElement("div");
    body.className = "body";
    [...s.childNodes].forEach((n) => {
      if (n.nodeType === 1 && (n.classList.contains("status") || n.matches("aside.notes"))) return;
      body.append(n);
    });
    s.append(body);
  });

  function shrinkToFit() {
    slides.forEach((s) => {
      const wasActive = s.classList.contains("active");
      s.classList.add("active");
      const body = s.querySelector(":scope > .body");
      body.style.transform = "";
      body.style.width = "";
      s.querySelectorAll("pre").forEach((pre) => {
        let size = parseFloat(getComputedStyle(pre).fontSize);
        while (pre.scrollWidth > pre.clientWidth + 1 && size > 16) {
          size -= 1;
          pre.style.fontSize = size + "px";
        }
      });
      const available = s.clientHeight - parseFloat(getComputedStyle(s).paddingTop) - parseFloat(getComputedStyle(s).paddingBottom);
      const needed = body.scrollHeight;
      if (needed > available) {
        const scale = available / needed;
        body.style.transform = `scale(${scale})`;
        body.style.width = `${100 / scale}%`;
      }
      if (!wasActive) s.classList.remove("active");
    });
  }
  (document.fonts ? document.fonts.ready : Promise.resolve()).then(shrinkToFit);

  window.addEventListener("resize", fit);
  window.addEventListener("hashchange", () => show(parseInt(location.hash.slice(1), 10) - 1 || 0, false));
  fit();
  show((parseInt(location.hash.slice(1), 10) || 1) - 1, false);
})();
