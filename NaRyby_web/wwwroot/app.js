// NaRyby – wersja webowa. Czysty JS bez bibliotek; dane z API serwera (/api/...).
(function () {
  'use strict';

  var REFRESH_MS = 30 * 60 * 1000;
  var $ = function (id) { return document.getElementById(id); };
  var view = $('view');
  var selCity = $('selCity');
  var selSpecies = $('selSpecies');

  var state = {
    cities: [],
    species: [],
    city: localStorage.getItem('naryby.city') || '',
    sp: localStorage.getItem('naryby.species') || '',
    forecast: null,
    loadedAt: 0,
    loading: null,
    hour: null // wybrana godzina na wykresie
  };

  // ---------- Pomocnicze ----------

  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function num(v, digits) {
    digits = digits || 0;
    var f = Math.pow(10, digits);
    var r = Math.round(v * f) / f;
    if (r === 0) r = 0; // bez „-0”
    return r.toLocaleString('pl-PL', { minimumFractionDigits: digits, maximumFractionDigits: digits });
  }

  function num1(v) { // do jednego miejsca, bez zbędnego „,0”
    var r = Math.round(v * 10) / 10;
    if (r === 0) r = 0;
    return r.toLocaleString('pl-PL', { maximumFractionDigits: 1 });
  }

  function api(url, options) {
    return fetch(url, options).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (data) {
        if (!r.ok || (data && data.error)) throw new Error((data && data.error) || ('Błąd serwera (' + r.status + ')'));
        return data;
      });
    });
  }

  function q(params) {
    return Object.keys(params).map(function (k) {
      return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]);
    }).join('&');
  }

  var toastTimer;
  function toast(text) {
    var t = $('toast');
    t.textContent = text;
    t.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.hidden = true; }, 3200);
  }

  function setStatus(text) { $('status').textContent = text || ''; }

  function badge(score, color, small) {
    return '<span class="score-badge' + (small ? ' small' : '') + '" style="background:' + esc(color) + '">' + score + '</span>';
  }

  function loading(text) { return '<div class="loading">' + esc(text || 'Ładowanie…') + '</div>'; }
  function errorBox(text) { return '<div class="card error">' + esc(text) + '</div>'; }

  // ---------- Listy wyboru ----------

  function fillCities() {
    if (!state.cities.some(function (c) { return c.name === state.city; }))
      state.city = state.cities.length ? state.cities[0].name : '';
    selCity.innerHTML = state.cities.map(function (c) {
      return '<option' + (c.name === state.city ? ' selected' : '') + '>' + esc(c.name) + '</option>';
    }).join('');
  }

  function fillSpecies() {
    if (!state.species.some(function (s) { return s.name === state.sp; }))
      state.sp = state.species.length ? state.species[0].name : '';
    selSpecies.innerHTML = state.species.map(function (s) {
      return '<option value="' + esc(s.name) + '"' + (s.name === state.sp ? ' selected' : '') + '>' +
        esc(s.name + (s.closedToday ? ' (ochrona)' : '')) + '</option>';
    }).join('');
  }

  selCity.addEventListener('change', function () {
    state.city = selCity.value;
    localStorage.setItem('naryby.city', state.city);
    loadForecast(true);
  });

  selSpecies.addEventListener('change', function () {
    state.sp = selSpecies.value;
    localStorage.setItem('naryby.species', state.sp);
    loadForecast(true);
  });

  // ---------- Prognoza ----------

  function loadForecast(show) {
    if (!state.city) return Promise.resolve();
    var key = state.city + '|' + state.sp;
    state.forecast = state.forecast && state.forecast.key === key ? state.forecast : null;
    if (show !== false) render();
    $('btnRefresh').classList.add('spin');
    setStatus('Pobieram prognozę…');
    var p = api('api/forecast?' + q({ city: state.city, species: state.sp }))
      .then(function (f) {
        if (key !== state.city + '|' + state.sp) return; // w międzyczasie zmieniono wybór
        f.key = key;
        state.forecast = f;
        state.loadedAt = Date.now();
        state.error = null;
        setStatus('Zaktualizowano: ' + f.fetchedAt + ' · strefa czasowa: ' + f.timeZone);
      })
      .catch(function (e) {
        state.error = e.message;
        setStatus('Nie udało się pobrać prognozy.');
      })
      .then(function () {
        $('btnRefresh').classList.remove('spin');
        if (route().name === 'home' || route().name === 'day') render();
      });
    state.loading = p;
    return p;
  }

  function renderHome() {
    var f = state.forecast;
    if (!f) {
      view.innerHTML = state.error ? errorBox('Nie udało się pobrać prognozy pogody. Sprawdź połączenie z internetem.\n' + state.error)
        : loading('Pobieram prognozę dla: ' + state.city + '…');
      return;
    }
    var c = f.current;
    var h = [];
    h.push('<div class="banner" style="background:' + esc(darken(f.recommendationColor, 0.2)) + '"><strong>' +
      esc(f.recommendation) + '</strong>' + (f.recommendationReason ? '<div>' + esc(f.recommendationReason) + '</div>' : '') + '</div>');

    h.push('<section class="card"><h2>Warunki teraz</h2><div class="current"><div><div class="place">' + esc(f.city) +
      '</div><div class="when">' + esc(f.nowText) + '</div><div>' + esc(c.weather) + ', ' + num(c.temperature, 1) + ' °C</div></div>');
    if (c.score != null)
      h.push('<div class="now-score">' + badge(c.score, c.color) + '<div class="small dim">brania teraz</div></div>');
    h.push('</div><div class="facts">');
    if (c.water != null) h.push('<div><span>Woda:</span> ok. ' + num(c.water) + ' °C</div>');
    h.push('<div><span>Ciśnienie:</span> ' + num(c.pressure) + ' hPa ' + esc(c.pressureTrend.split(' ')[0] || '') + '</div>');
    h.push('<div><span>Wiatr:</span> ' + num(c.wind) + ' km/h, ' + esc(c.windDir) + '</div>');
    h.push('<div><span>Wilgotność:</span> ' + num(c.humidity) + '%</div>');
    h.push('<div><span>Zachmurzenie:</span> ' + num(c.cloud) + '%</div>');
    h.push('<div><span>Opady:</span> ' + num(c.precipitation, 1) + ' mm</div>');
    if (c.sunrise) h.push('<div><span>Słońce:</span> ' + esc(c.sunrise) + '–' + esc(c.sunset) + '</div>');
    h.push('<div><span>Księżyc:</span> ' + esc(c.moon) + '</div>');
    h.push('</div>');
    if (c.pressureTrend) h.push('<div class="small dim" style="margin-top:6px">Trend ciśnienia: ' + esc(c.pressureTrend) + '</div>');
    if (c.score != null) h.push('<div class="small" style="margin-top:4px">Brania teraz (' + esc(f.species.toLowerCase()) + '): <span class="stars">' + esc(c.stars) + '</span> ' + esc(c.rating) + '</div>');
    h.push('</section>');

    h.push(hydroCard(f.hydro));

    h.push('<h2 class="dim" style="font-size:15px;margin:16px 2px 8px;text-transform:uppercase;letter-spacing:.04em">Prognoza brań na 7 dni</h2><div class="days">');
    f.days.forEach(function (d, i) {
      h.push('<a class="day' + (d.isBest ? ' best' : '') + '" href="#/day/' + i + '">' + badge(d.score, d.color) +
        '<div><div class="title">' + esc(d.name) + (d.isBest ? '<span class="tag">NAJLEPSZY</span>' : '') + '</div>' +
        '<div class="line"><span class="stars">' + esc(d.stars) + '</span> ' + esc(d.rating) + '</div>' +
        '<div class="line">Najlepiej: <b>' + esc(d.bestWindow) + '</b></div></div><span class="chev">›</span>' +
        '<div class="meta"><span>🎣 ' + esc(d.topSpecies.join(', ')) + '</span>' +
        '<span>🌡 ' + num(d.tempMin) + '°/' + num(d.tempMax) + '°C</span>' +
        '<span>💧 woda ok. ' + num(d.water) + '°C</span>' +
        '<span>💨 ' + num(d.windMax) + ' km/h</span>' +
        '<span>☔ ' + num1(d.precipitation) + ' mm</span>' +
        '<span>' + num(d.pressure) + ' hPa ' + esc(d.pressureArrow) + '</span></div></a>');
    });
    h.push('</div>');
    view.innerHTML = h.join('');
  }

  function hydroCard(hy) {
    if (!hy) return '';
    return '<section class="card hydro' + (hy.isAlarm ? ' alarm' : hy.isWarning ? ' warn' : '') + '"><h2>Stan wody (' + esc(hy.source) + ')</h2>' +
      '<div><b>' + esc(hy.name) + '</b></div>' +
      '<div>' + (hy.isWarning ? '⚠ ' : '') + hy.level + ' cm – ' + esc(hy.status) + '</div>' +
      '<div class="small dim">pomiar ' + esc(hy.time) + '</div>' +
      (hy.waterTemperature != null ? '<div>Woda (pomiar na stacji): ' + num(hy.waterTemperature, 1) + ' °C</div>' : '') +
      '</section>';
  }

  function darken(hex, amount) {
    var n = parseInt(hex.slice(1), 16);
    var r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
    var k = 1 - amount;
    return 'rgb(' + Math.round(r * k) + ',' + Math.round(g * k) + ',' + Math.round(b * k) + ')';
  }

  // ---------- Ekran dnia ----------

  function renderDay(index) {
    var f = state.forecast;
    if (!f) { renderHome(); return; }
    var d = f.days[index];
    if (!d) { location.hash = '#/'; return; }
    var s = f.speciesInfo;
    var h = [];
    h.push('<a class="back" href="#/">‹ Wszystkie dni</a>');
    h.push('<section class="card"><div class="day-head">' + badge(d.score, d.color) + '<div><h1>' + esc(d.name) + '</h1>' +
      '<div><span class="stars">' + esc(d.stars) + '</span> ' + esc(d.rating) + ' · ' + esc(f.species) + '</div>' +
      '<div class="small dim">Najlepiej ' + esc(d.bestWindow) + ' (' + d.bestScore + '/100) · wschód ' + esc(d.sunrise) + ', zachód ' + esc(d.sunset) + '</div></div></div></section>');

    h.push('<section class="card chart"><h2>Prognoza godzinowa</h2>' + chartSvg(d) +
      '<div class="legend"><span><i style="background:#2ea043"></i>ocena brań</span><span><i style="background:var(--best)"></i>najlepsze okno</span>' +
      '<span><i style="background:var(--night)"></i>noc</span><span><i class="line" style="background:var(--pressure)"></i>ciśnienie</span></div>' +
      '<div class="hour-info" id="hourInfo"></div></section>');

    h.push('<section class="card"><h2>Ocena dnia</h2><ul class="reasons">' +
      d.reasons.map(function (r) { return '<li>' + esc(r) + '</li>'; }).join('') + '</ul></section>');

    h.push('<section class="card"><h2>Na co łowić tego dnia</h2>' + d.ranking.map(function (r) {
      return '<div class="rank"><span>' + esc(r.name) + (r.closed ? ' 🚫' : '') + '</span><span class="bar"><i style="width:' + r.score + '%;background:' + esc(r.color) + '"></i></span><b>' + r.score + '</b></div>';
    }).join('') + (d.ranking.some(function (r) { return r.closed; }) ? '<div class="small dim">🚫 – okres ochronny</div>' : '') + '</section>');

    h.push('<section class="card"><h2>' + (s.isGeneral ? 'Porady' : 'Porady: ' + esc(s.name.toLowerCase())) + '</h2>');
    if (!s.isGeneral) {
      h.push('<div>Optymalna temperatura wody: <b>' + esc(s.waterOpt) + '</b></div>');
      h.push('<div>' + esc(s.closed) + '</div>');
      h.push('<div>Wymiar ochronny: ' + esc(s.minLength) + '</div>');
      if (s.bestDay) h.push('<div>' + esc(s.bestDay) + '</div>');
    }
    h.push('<h3>' + esc(d.name) + ' (woda ok. ' + num(d.water) + ' °C)</h3><p style="margin:0">' + esc(d.waterTip) + '</p>');
    h.push('<h3>Wskazówki</h3><ul class="tips">' + s.tips.map(function (t) { return '<li>' + esc(t) + '</li>'; }).join('') + '</ul></section>');

    view.innerHTML = h.join('');

    // Domyślnie: bieżąca godzina dziś, w inne dni początek najlepszego okna.
    var start = 0;
    var nowIdx = -1, bestIdx = -1;
    d.hours.forEach(function (x, i) { if (!x.past && nowIdx < 0) nowIdx = i; if (x.best && bestIdx < 0) bestIdx = i; });
    start = index === 0 && nowIdx >= 0 ? nowIdx : Math.max(0, bestIdx);
    bindChart(d, start);
  }

  var CH = { w: 360, h: 220, l: 26, r: 34, t: 10, b: 24 };

  function chartSvg(d) {
    var hours = d.hours;
    var n = hours.length;
    var aw = CH.w - CH.l - CH.r, ah = CH.h - CH.t - CH.b;
    var cw = aw / n;
    var y = function (score) { return CH.t + ah - score / 100 * ah; };
    var s = [];
    s.push('<svg id="chartSvg" viewBox="0 0 ' + CH.w + ' ' + CH.h + '" role="img" aria-label="Wykres oceny brań i ciśnienia w kolejnych godzinach">');
    // Tła: najlepsze okno i noc.
    hours.forEach(function (x, i) {
      if (x.best || x.night)
        s.push('<rect x="' + (CH.l + i * cw).toFixed(2) + '" y="' + CH.t + '" width="' + (cw + 0.3).toFixed(2) + '" height="' + ah + '" fill="var(' + (x.best ? '--best' : '--night') + ')"/>');
    });
    // Siatka i opisy osi ocen.
    [0, 25, 50, 75, 100].forEach(function (v) {
      s.push('<line x1="' + CH.l + '" x2="' + (CH.w - CH.r) + '" y1="' + y(v) + '" y2="' + y(v) + '" stroke="var(--grid)" stroke-width="1"/>');
      s.push('<text x="' + (CH.l - 4) + '" y="' + (y(v) + 3.5) + '" font-size="10" text-anchor="end" fill="var(--dim)">' + v + '</text>');
    });
    // Słupki ocen.
    hours.forEach(function (x, i) {
      var bh = Math.max(1, x.score / 100 * ah);
      s.push('<rect x="' + (CH.l + i * cw + cw * 0.15).toFixed(2) + '" y="' + (CH.t + ah - bh).toFixed(2) + '" width="' + (cw * 0.7).toFixed(2) +
        '" height="' + bh.toFixed(2) + '" rx="1.5" fill="' + x.color + '"' + (x.past ? ' opacity="0.45"' : '') + '/>');
    });
    // Ciśnienie.
    var ps = hours.map(function (x) { return x.pressure; });
    var pMin = Math.floor(Math.min.apply(null, ps)) - 1, pMax = Math.ceil(Math.max.apply(null, ps)) + 1;
    var py = function (p) { return CH.t + ah - (p - pMin) / (pMax - pMin) * ah; };
    var pts = hours.map(function (x, i) { return (CH.l + (i + 0.5) * cw).toFixed(1) + ',' + py(x.pressure).toFixed(1); }).join(' ');
    s.push('<polyline points="' + pts + '" fill="none" stroke="var(--pressure)" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>');
    var rx = CH.w - CH.r + 4;
    s.push('<text x="' + rx + '" y="' + (CH.t + 9) + '" font-size="10" fill="var(--pressure)">' + pMax + '</text>');
    s.push('<text x="' + rx + '" y="' + (CH.t + ah) + '" font-size="10" fill="var(--pressure)">' + pMin + '</text>');
    s.push('<text x="' + rx + '" y="' + (CH.t + ah / 2 + 3) + '" font-size="10" fill="var(--pressure)">hPa</text>');
    // Godziny.
    hours.forEach(function (x, i) {
      if (x.hour % 3 === 0)
        s.push('<text x="' + (CH.l + (i + 0.5) * cw).toFixed(1) + '" y="' + (CH.h - 8) + '" font-size="10" text-anchor="middle" fill="var(--dim)">' + x.hour + '</text>');
    });
    s.push('<rect id="hourSel" x="0" y="' + (CH.t - 2) + '" width="' + cw.toFixed(2) + '" height="' + (ah + 4) + '" rx="3" fill="none" stroke="var(--text)" stroke-width="1.5" visibility="hidden"/>');
    s.push('<rect id="chartHit" x="' + CH.l + '" y="0" width="' + aw + '" height="' + CH.h + '" fill="transparent"/>');
    s.push('</svg>');
    return s.join('');
  }

  function bindChart(d, start) {
    var svg = $('chartSvg');
    var n = d.hours.length;
    var aw = CH.w - CH.l - CH.r, cw = aw / n;

    function select(i) {
      i = Math.max(0, Math.min(n - 1, i));
      var x = d.hours[i];
      var sel = $('hourSel');
      sel.setAttribute('x', (CH.l + i * cw).toFixed(2));
      sel.setAttribute('visibility', 'visible');
      var hh = (x.hour < 10 ? '0' : '') + x.hour;
      $('hourInfo').innerHTML = '<b>' + hh + ':00</b> – ocena <b>' + x.score + '/100</b>' + (x.best ? ' · najlepsze okno' : '') + (x.night ? ' · noc' : '') + '<br>' +
        esc(x.weather) + ', ' + num(x.temperature, 1) + ' °C, woda ok. ' + num(x.water) + ' °C<br>' +
        'Ciśnienie ' + num(x.pressure) + ' hPa · wiatr ' + num(x.wind) + ' km/h ' + esc(x.windDir) + ' (porywy ' + num(x.gusts) + ')<br>' +
        'Opady ' + num1(x.precipitation) + ' mm · zachmurzenie ' + num(x.cloud) + '%';
    }

    function fromEvent(e) {
      var rect = svg.getBoundingClientRect();
      var px = (e.clientX - rect.left) / rect.width * CH.w;
      select(Math.floor((px - CH.l) / cw));
    }

    svg.addEventListener('pointerdown', fromEvent);
    svg.addEventListener('pointermove', function (e) { if (e.pointerType === 'mouse' || e.buttons) fromEvent(e); });
    select(start);
  }

  // ---------- Porównanie łowisk ----------

  function renderCompare() {
    view.innerHTML = '<section class="card"><h2>Porównanie łowisk – ' + esc(state.sp.toLowerCase()) + '</h2>' +
      loading('Pobieram prognozy dla ' + state.cities.length + ' miejscowości…') + '</section>';
    var sp = state.sp;
    api('api/compare?' + q({ species: sp })).then(function (r) {
      if (route().name !== 'compare' || sp !== state.sp) return;
      var h = ['<section class="card"><h2>Porównanie łowisk – ' + esc(sp.toLowerCase()) + '</h2>',
        '<p style="margin:0 0 8px">' + esc(r.summary) + '</p>',
        '<p class="small dim" style="margin:0 0 8px">Ocena brań (0–100) w kolejnych dniach. Stuknij miejscowość, aby otworzyć jej prognozę.</p>',
        '<div class="table-wrap"><table class="cmp"><thead><tr><th>Miejscowość</th>'];
      r.days.forEach(function (d) { h.push('<th style="text-align:center">' + esc(d.shortName) + '</th>'); });
      h.push('<th>Najlepiej</th></tr></thead><tbody>');
      r.rows.forEach(function (row) {
        h.push('<tr><td class="city" data-city="' + esc(row.city) + '">' + esc(row.city) + '</td>');
        row.scores.forEach(function (s) {
          h.push(s ? '<td class="s"><span title="' + esc(s.title) + '" class="' + (s.isBest ? 'top' : '') + '" style="background:' + esc(s.color) + '">' + s.score + '</span></td>' : '<td class="s">–</td>');
        });
        h.push('<td' + (row.error ? ' class="error" title="' + esc(row.error) + '"' : '') + '>' + esc(row.best) + '</td></tr>');
      });
      h.push('</tbody></table></div></section>');
      view.innerHTML = h.join('');
      Array.prototype.forEach.call(view.querySelectorAll('td.city'), function (td) {
        td.addEventListener('click', function () {
          state.city = td.getAttribute('data-city');
          localStorage.setItem('naryby.city', state.city);
          fillCities();
          location.hash = '#/';
          loadForecast(true);
        });
      });
    }).catch(function (e) {
      if (route().name === 'compare') view.innerHTML = errorBox(e.message);
    });
  }

  // ---------- Okresy ochronne ----------

  function renderProtection() {
    view.innerHTML = loading();
    api('api/protection').then(function (r) {
      if (route().name !== 'protection') return;
      var h = ['<section class="card"><h2>Okresy i wymiary ochronne</h2><div class="table-wrap"><table class="prot"><thead><tr>',
        '<th>Gatunek</th><th>Okres ochronny</th><th>Wymiar</th><th>Dziś</th></tr></thead><tbody>'];
      r.rows.forEach(function (s) {
        h.push('<tr class="' + (s.isClosed ? 'closed' : s.soon ? 'soon' : '') + '"><td><b>' + esc(s.name) + '</b></td><td>' + esc(s.closed) +
          '</td><td>' + esc(s.minLength) + '</td><td style="white-space:normal">' + esc(s.status) + '</td></tr>');
      });
      h.push('</tbody></table></div><p class="small dim">' + esc(r.note) + '</p></section>');
      view.innerHTML = h.join('');
    }).catch(function (e) { view.innerHTML = errorBox(e.message); });
  }

  // ---------- Miejscowości ----------

  function renderCities() {
    var h = ['<section class="card"><h2>Dodaj miejscowość</h2>',
      '<form class="search" id="searchForm"><input id="searchInput" type="search" placeholder="Jezioro, wieś lub miasto" autocomplete="off" enterkeyhint="search">',
      '<button class="btn primary" type="submit">Szukaj</button></form><ul class="list" id="searchResults"></ul></section>',
      '<section class="card"><h2>Zapisane łowiska</h2><ul class="list">'];
    state.cities.forEach(function (c) {
      h.push('<li><span class="name">' + esc(c.name) + '<small>' + num(c.latitude, 4) + ', ' + num(c.longitude, 4) + '</small></span>' +
        '<button class="btn" type="button" data-open="' + esc(c.name) + '">Pokaż</button>' +
        '<button class="btn danger" type="button" data-remove="' + esc(c.name) + '" aria-label="Usuń ' + esc(c.name) + '">Usuń</button></li>');
    });
    h.push('</ul><p class="small dim">Lista jest zapisana na serwerze i wspólna dla wszystkich urządzeń.</p></section>');
    view.innerHTML = h.join('');

    $('searchForm').addEventListener('submit', function (e) {
      e.preventDefault();
      var query = $('searchInput').value.trim();
      if (!query) return;
      $('searchInput').blur();
      var out = $('searchResults');
      out.innerHTML = '<li class="dim">Szukam miejscowości „' + esc(query) + '”…</li>';
      api('api/search?' + q({ q: query })).then(function (list) {
        if (!list.length) { out.innerHTML = '<li class="dim">Nie znaleziono miejscowości „' + esc(query) + '”.</li>'; return; }
        out.innerHTML = list.map(function (c, i) {
          return '<li><span class="name">' + esc(c.name) + '<small>' + esc(c.description) + '</small></span><button class="btn primary" type="button" data-add="' + i + '">Dodaj</button></li>';
        }).join('');
        Array.prototype.forEach.call(out.querySelectorAll('[data-add]'), function (b) {
          b.addEventListener('click', function () {
            b.disabled = true;
            var c = list[+b.getAttribute('data-add')];
            api('api/cities', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(c) })
              .then(function (added) {
                return loadCities().then(function () {
                  state.city = added.name;
                  localStorage.setItem('naryby.city', state.city);
                  fillCities();
                  toast('Dodano: ' + added.name);
                  location.hash = '#/';
                  loadForecast(true);
                });
              })
              .catch(function (e) { b.disabled = false; toast(e.message); });
          });
        });
      }).catch(function (e) { out.innerHTML = '<li class="error">' + esc(e.message) + '</li>'; });
    });

    Array.prototype.forEach.call(view.querySelectorAll('[data-open]'), function (b) {
      b.addEventListener('click', function () {
        state.city = b.getAttribute('data-open');
        localStorage.setItem('naryby.city', state.city);
        fillCities();
        location.hash = '#/';
        loadForecast(true);
      });
    });

    Array.prototype.forEach.call(view.querySelectorAll('[data-remove]'), function (b) {
      b.addEventListener('click', function () {
        var name = b.getAttribute('data-remove');
        if (!confirm('Usunąć „' + name + '” z listy?')) return;
        api('api/cities?' + q({ name: name }), { method: 'DELETE' })
          .then(function (list) {
            state.cities = list;
            var changed = state.city === name;
            fillCities();
            renderCities();
            toast('Usunięto: ' + name);
            if (changed) loadForecast(false);
          })
          .catch(function (e) { toast(e.message); });
      });
    });
  }

  function loadCities() {
    return api('api/cities').then(function (list) { state.cities = list; fillCities(); });
  }

  // ---------- Udostępnianie ----------

  $('btnShare').addEventListener('click', function () {
    var f = state.forecast;
    if (!f) { toast('Najpierw poczekaj na prognozę.'); return; }
    var text = f.shareText;
    if (navigator.share) {
      navigator.share({ title: 'NaRyby – ' + f.city, text: text }).catch(function (e) {
        if (e && e.name !== 'AbortError') copyText(text);
      });
      return;
    }
    copyText(text);
  });

  function copyText(text) {
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(function () {
        toast('Skopiowano prognozę – wklej ją w komunikatorze.');
      }, function () { showShareDialog(text); });
      return;
    }
    // Strona po zwykłym http (np. przez ZeroTier) nie ma dostępu do Clipboard API ani navigator.share –
    // pokazujemy tekst z przyciskiem „Kopiuj” (execCommand działa też bez https).
    showShareDialog(text);
  }

  function showShareDialog(text) {
    var dlg = $('shareDialog');
    $('shareText').value = text;
    if (dlg.showModal) dlg.showModal(); else dlg.setAttribute('open', '');
  }

  $('btnCopy').addEventListener('click', function () {
    var ta = $('shareText');
    ta.focus();
    ta.select();
    ta.setSelectionRange(0, ta.value.length);
    var ok = false;
    try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
    toast(ok ? 'Skopiowano prognozę – wklej ją w komunikatorze.' : 'Zaznacz tekst i wybierz „Kopiuj”.');
    if (ok) $('shareDialog').close();
  });

  // ---------- Nawigacja ----------

  function route() {
    var parts = (location.hash || '#/').replace(/^#\/?/, '').split('/');
    var name = parts[0] || 'home';
    if (name === 'day') return { name: 'day', index: parseInt(parts[1], 10) || 0 };
    if (['compare', 'protection', 'cities'].indexOf(name) >= 0) return { name: name };
    return { name: 'home' };
  }

  function render() {
    var r = route();
    Array.prototype.forEach.call(document.querySelectorAll('.tabs a'), function (a) {
      var v = a.getAttribute('data-view');
      a.classList.toggle('active', v === r.name || (v === 'home' && r.name === 'day'));
    });
    if (r.name === 'day') renderDay(r.index);
    else if (r.name === 'compare') renderCompare();
    else if (r.name === 'protection') renderProtection();
    else if (r.name === 'cities') renderCities();
    else renderHome();
  }

  window.addEventListener('hashchange', function () { render(); window.scrollTo(0, 0); });

  $('btnRefresh').addEventListener('click', function () {
    var r = route().name;
    if (r === 'compare' || r === 'protection' || r === 'cities') { render(); return; }
    loadForecast(false);
  });

  // Automatyczne odświeżanie co 30 minut, także po powrocie do karty/aplikacji.
  setInterval(function () { if (!document.hidden) loadForecast(false); }, REFRESH_MS);
  document.addEventListener('visibilitychange', function () {
    if (!document.hidden && state.loadedAt && Date.now() - state.loadedAt > REFRESH_MS) loadForecast(false);
  });

  // ---------- Start ----------

  view.innerHTML = loading();
  Promise.all([loadCities(), api('api/species').then(function (s) { state.species = s; fillSpecies(); })])
    .then(function () { return loadForecast(true); })
    .catch(function (e) { view.innerHTML = errorBox('Nie udało się połączyć z serwerem NaRyby. ' + e.message); });
})();
