import { umbHttpClient as Y } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as G } from "@umbraco-cms/backoffice/resources";
const Q = {
  bodySerializer: (e) => JSON.stringify(e, (t, r) => typeof r == "bigint" ? r.toString() : r)
};
function X({
  onRequest: e,
  onSseError: t,
  onSseEvent: r,
  responseTransformer: n,
  responseValidator: i,
  sseDefaultRetryDelay: c,
  sseMaxRetryAttempts: s,
  sseMaxRetryDelay: o,
  sseSleepFn: a,
  url: m,
  ...f
}) {
  let l;
  const y = a ?? ((d) => new Promise((p) => setTimeout(p, d)));
  return { stream: async function* () {
    let d = c ?? 3e3, p = 0;
    const g = f.signal ?? new AbortController().signal;
    for (; !g.aborted; ) {
      p++;
      const k = f.headers instanceof Headers ? f.headers : new Headers(f.headers);
      l !== void 0 && k.set("Last-Event-ID", l);
      try {
        const T = {
          redirect: "follow",
          ...f,
          body: f.serializedBody,
          headers: k,
          signal: g
        };
        let j = new Request(m, T);
        e && (j = await e(m, T));
        const b = await (f.fetch ?? globalThis.fetch)(j);
        if (!b.ok) throw new Error(`SSE failed: ${b.status} ${b.statusText}`);
        if (!b.body) throw new Error("No body in SSE response");
        const S = b.body.pipeThrough(new TextDecoderStream()).getReader();
        let w = "";
        const I = () => {
          try {
            S.cancel();
          } catch {
          }
        };
        g.addEventListener("abort", I);
        try {
          for (; ; ) {
            const { done: M, value: V } = await S.read();
            if (M) break;
            w += V, w = w.replace(/\r\n?/g, `
`);
            const C = w.split(`

`);
            w = C.pop() ?? "";
            for (const F of C) {
              const J = F.split(`
`), x = [];
              let q;
              for (const E of J)
                if (E.startsWith("data:"))
                  x.push(E.replace(/^data:\s*/, ""));
                else if (E.startsWith("event:"))
                  q = E.replace(/^event:\s*/, "");
                else if (E.startsWith("id:"))
                  l = E.replace(/^id:\s*/, "");
                else if (E.startsWith("retry:")) {
                  const P = Number.parseInt(E.replace(/^retry:\s*/, ""), 10);
                  Number.isNaN(P) || (d = P);
                }
              let A, N = !1;
              if (x.length) {
                const E = x.join(`
`);
                try {
                  A = JSON.parse(E), N = !0;
                } catch {
                  A = E;
                }
              }
              N && (i && await i(A), n && (A = await n(A))), r?.({
                data: A,
                event: q,
                id: l,
                retry: d
              }), x.length && (yield A);
            }
          }
        } finally {
          g.removeEventListener("abort", I), S.releaseLock();
        }
        break;
      } catch (T) {
        if (t?.(T), s !== void 0 && p >= s)
          break;
        const j = Math.min(d * 2 ** (p - 1), o ?? 3e4);
        await y(j);
      }
    }
  }() };
}
const K = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, Z = (e) => {
  switch (e) {
    case "form":
      return ",";
    case "pipeDelimited":
      return "|";
    case "spaceDelimited":
      return "%20";
    default:
      return ",";
  }
}, ee = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, R = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: n,
  value: i
}) => {
  if (!t) {
    const o = (e ? i : i.map((a) => encodeURIComponent(a))).join(Z(n));
    switch (n) {
      case "label":
        return `.${o}`;
      case "matrix":
        return `;${r}=${o}`;
      case "simple":
        return o;
      default:
        return `${r}=${o}`;
    }
  }
  const c = K(n), s = i.map((o) => n === "label" || n === "simple" ? e ? o : encodeURIComponent(o) : v({
    allowReserved: e,
    name: r,
    value: o
  })).join(c);
  return n === "label" || n === "matrix" ? c + s : s;
}, v = ({
  allowReserved: e,
  name: t,
  value: r
}) => {
  if (r == null)
    return "";
  if (typeof r == "object")
    throw new Error(
      "Deeply-nested arrays/objects aren’t supported. Provide your own `querySerializer()` to handle these."
    );
  return `${t}=${e ? r : encodeURIComponent(r)}`;
}, D = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: n,
  value: i,
  valueOnly: c
}) => {
  if (i instanceof Date)
    return c ? i.toISOString() : `${r}=${i.toISOString()}`;
  if (n !== "deepObject" && !t) {
    let a = [];
    Object.entries(i).forEach(([f, l]) => {
      a = [...a, f, e ? l : encodeURIComponent(l)];
    });
    const m = a.join(",");
    switch (n) {
      case "form":
        return `${r}=${m}`;
      case "label":
        return `.${m}`;
      case "matrix":
        return `;${r}=${m}`;
      default:
        return m;
    }
  }
  const s = ee(n), o = Object.entries(i).map(
    ([a, m]) => v({
      allowReserved: e,
      name: n === "deepObject" ? `${r}[${a}]` : a,
      value: m
    })
  ).join(s);
  return n === "label" || n === "matrix" ? s + o : o;
}, te = /\{[^{}]+\}/g, re = ({ path: e, url: t }) => {
  let r = t;
  const n = t.match(te);
  if (n)
    for (const i of n) {
      let c = !1, s = i.substring(1, i.length - 1), o = "simple";
      s.endsWith("*") && (c = !0, s = s.substring(0, s.length - 1)), s.startsWith(".") ? (s = s.substring(1), o = "label") : s.startsWith(";") && (s = s.substring(1), o = "matrix");
      const a = e[s];
      if (a == null)
        continue;
      if (Array.isArray(a)) {
        r = r.replace(i, R({ explode: c, name: s, style: o, value: a }));
        continue;
      }
      if (typeof a == "object") {
        r = r.replace(
          i,
          D({
            explode: c,
            name: s,
            style: o,
            value: a,
            valueOnly: !0
          })
        );
        continue;
      }
      if (o === "matrix") {
        r = r.replace(
          i,
          `;${v({
            name: s,
            value: a
          })}`
        );
        continue;
      }
      const m = encodeURIComponent(
        o === "label" ? `.${a}` : a
      );
      r = r.replace(i, m);
    }
  return r;
}, ne = ({
  baseUrl: e,
  path: t,
  query: r,
  querySerializer: n,
  url: i
}) => {
  const c = i.startsWith("/") ? i : `/${i}`;
  let s = (e ?? "") + c;
  t && (s = re({ path: t, url: s }));
  let o = r ? n(r) : "";
  return o.startsWith("?") && (o = o.substring(1)), o && (s += `?${o}`), s;
};
function $(e) {
  const t = e.body !== void 0;
  if (t && e.bodySerializer)
    return "serializedBody" in e ? e.serializedBody !== void 0 && e.serializedBody !== "" ? e.serializedBody : null : e.body !== "" ? e.body : null;
  if (t)
    return e.body;
}
const se = async (e, t) => {
  const r = typeof t == "function" ? await t(e) : t;
  if (r)
    return e.scheme === "bearer" ? `Bearer ${r}` : e.scheme === "basic" ? `Basic ${btoa(r)}` : r;
}, _ = ({
  parameters: e = {},
  ...t
} = {}) => (n) => {
  const i = [];
  if (n && typeof n == "object")
    for (const c in n) {
      const s = n[c];
      if (s == null)
        continue;
      const o = e[c] || t;
      if (Array.isArray(s)) {
        const a = R({
          allowReserved: o.allowReserved,
          explode: !0,
          name: c,
          style: "form",
          value: s,
          ...o.array
        });
        a && i.push(a);
      } else if (typeof s == "object") {
        const a = D({
          allowReserved: o.allowReserved,
          explode: !0,
          name: c,
          style: "deepObject",
          value: s,
          ...o.object
        });
        a && i.push(a);
      } else {
        const a = v({
          allowReserved: o.allowReserved,
          name: c,
          value: s
        });
        a && i.push(a);
      }
    }
  return i.join("&");
}, ae = (e) => {
  if (!e)
    return "stream";
  const t = e.split(";")[0]?.trim();
  if (t) {
    if (t.startsWith("application/json") || t.endsWith("+json"))
      return "json";
    if (t === "multipart/form-data")
      return "formData";
    if (["application/", "audio/", "image/", "video/"].some((r) => t.startsWith(r)))
      return "blob";
    if (t.startsWith("text/"))
      return "text";
  }
}, oe = (e, t) => t ? !!(e.headers.has(t) || e.query?.[t] || e.headers.get("Cookie")?.includes(`${t}=`)) : !1;
async function ie(e) {
  for (const t of e.security ?? []) {
    if (oe(e, t.name))
      continue;
    const r = await se(t, e.auth);
    if (!r)
      continue;
    const n = t.name ?? "Authorization";
    switch (t.in) {
      case "query":
        e.query || (e.query = {}), e.query[n] = r;
        break;
      case "cookie":
        e.headers.append("Cookie", `${n}=${r}`);
        break;
      default:
        e.headers.set(n, r);
        break;
    }
  }
}
const B = (e) => ne({
  baseUrl: e.baseUrl,
  path: e.path,
  query: e.query,
  querySerializer: typeof e.querySerializer == "function" ? e.querySerializer : _(e.querySerializer),
  url: e.url
}), U = (e, t) => {
  const r = { ...e, ...t };
  return r.baseUrl?.endsWith("/") && (r.baseUrl = r.baseUrl.substring(0, r.baseUrl.length - 1)), r.headers = H(e.headers, t.headers), r;
}, ce = (e) => {
  const t = [];
  return e.forEach((r, n) => {
    t.push([n, r]);
  }), t;
}, H = (...e) => {
  const t = new Headers();
  for (const r of e) {
    if (!r)
      continue;
    const n = r instanceof Headers ? ce(r) : Object.entries(r);
    for (const [i, c] of n)
      if (c === null)
        t.delete(i);
      else if (Array.isArray(c))
        for (const s of c)
          t.append(i, s);
      else c !== void 0 && t.set(
        i,
        typeof c == "object" ? JSON.stringify(c) : c
      );
  }
  return t;
};
class z {
  constructor() {
    this.fns = [];
  }
  clear() {
    this.fns = [];
  }
  eject(t) {
    const r = this.getInterceptorIndex(t);
    this.fns[r] && (this.fns[r] = null);
  }
  exists(t) {
    const r = this.getInterceptorIndex(t);
    return !!this.fns[r];
  }
  getInterceptorIndex(t) {
    return typeof t == "number" ? this.fns[t] ? t : -1 : this.fns.indexOf(t);
  }
  update(t, r) {
    const n = this.getInterceptorIndex(t);
    return this.fns[n] ? (this.fns[n] = r, t) : !1;
  }
  use(t) {
    return this.fns.push(t), this.fns.length - 1;
  }
}
const le = () => ({
  error: new z(),
  request: new z(),
  response: new z()
}), fe = _({
  allowReserved: !1,
  array: {
    explode: !0,
    style: "form"
  },
  object: {
    explode: !0,
    style: "deepObject"
  }
}), de = {
  "Content-Type": "application/json"
}, W = (e = {}) => ({
  ...Q,
  headers: de,
  parseAs: "auto",
  querySerializer: fe,
  ...e
}), ue = (e = {}) => {
  let t = U(W(), e);
  const r = () => ({ ...t }), n = (f) => (t = U(t, f), r()), i = le(), c = async (f) => {
    const l = {
      ...t,
      ...f,
      fetch: f.fetch ?? t.fetch ?? globalThis.fetch,
      headers: H(t.headers, f.headers),
      serializedBody: void 0
    };
    l.security && await ie(l), l.requestValidator && await l.requestValidator(l), l.body !== void 0 && l.bodySerializer && (l.serializedBody = l.bodySerializer(l.body)), (l.body === void 0 || l.serializedBody === "") && l.headers.delete("Content-Type");
    const y = l, h = B(y);
    return { opts: y, url: h };
  }, s = async (f) => {
    const l = f.throwOnError ?? t.throwOnError, y = f.responseStyle ?? t.responseStyle;
    let h, u;
    try {
      const { opts: d, url: p } = await c(f), g = {
        redirect: "follow",
        ...d,
        body: $(d)
      };
      h = new Request(p, g);
      for (const b of i.request.fns)
        b && (h = await b(h, d));
      const k = d.fetch;
      u = await k(h);
      for (const b of i.response.fns)
        b && (u = await b(u, h, d));
      const T = {
        request: h,
        response: u
      };
      if (u.ok) {
        const b = (d.parseAs === "auto" ? ae(u.headers.get("Content-Type")) : d.parseAs) ?? "json";
        if (u.status === 204 || u.headers.get("Content-Length") === "0") {
          let w;
          switch (b) {
            case "arrayBuffer":
            case "blob":
            case "text":
              w = await u[b]();
              break;
            case "formData":
              w = new FormData();
              break;
            case "stream":
              w = u.body;
              break;
            default:
              w = {};
              break;
          }
          return d.responseStyle === "data" ? w : {
            data: w,
            ...T
          };
        }
        let S;
        switch (b) {
          case "arrayBuffer":
          case "blob":
          case "formData":
          case "text":
            S = await u[b]();
            break;
          case "json": {
            const w = await u.text();
            S = w ? JSON.parse(w) : {};
            break;
          }
          case "stream":
            return d.responseStyle === "data" ? u.body : {
              data: u.body,
              ...T
            };
        }
        return b === "json" && (d.responseValidator && await d.responseValidator(S), d.responseTransformer && (S = await d.responseTransformer(S))), d.responseStyle === "data" ? S : {
          data: S,
          ...T
        };
      }
      const j = await u.text();
      let O;
      try {
        O = JSON.parse(j);
      } catch {
      }
      throw O ?? j;
    } catch (d) {
      let p = d;
      for (const g of i.error.fns)
        g && (p = await g(p, u, h, f));
      if (p = p || {}, l)
        throw p;
      return y === "data" ? void 0 : {
        error: p,
        request: h,
        response: u
      };
    }
  }, o = (f) => (l) => s({ ...l, method: f }), a = (f) => async (l) => {
    const { opts: y, url: h } = await c(l);
    return X({
      ...y,
      body: y.body,
      method: f,
      onRequest: async (u, d) => {
        let p = new Request(u, d);
        for (const g of i.request.fns)
          g && (p = await g(p, y));
        return p;
      },
      serializedBody: $(y),
      url: h
    });
  };
  return {
    buildUrl: (f) => B({ ...t, ...f }),
    connect: o("CONNECT"),
    delete: o("DELETE"),
    get: o("GET"),
    getConfig: r,
    head: o("HEAD"),
    interceptors: i,
    options: o("OPTIONS"),
    patch: o("PATCH"),
    post: o("POST"),
    put: o("PUT"),
    request: s,
    setConfig: n,
    sse: {
      connect: a("CONNECT"),
      delete: a("DELETE"),
      get: a("GET"),
      head: a("HEAD"),
      options: a("OPTIONS"),
      patch: a("PATCH"),
      post: a("POST"),
      put: a("PUT"),
      trace: a("TRACE")
    },
    trace: o("TRACE")
  };
}, he = ue(W({ baseUrl: "https://localhost:44331/" })), ye = (e) => (e?.client ?? he).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/backoffice-tidy/hidden",
  ...e
}), pe = "BackofficeTidy: could not load rules; nothing hidden.", be = (e) => `BackofficeTidy: no extension with alias "${e}" (typo, or the package is not installed).`, me = 100, we = 50, ge = 1e4, Se = /* @__PURE__ */ new Set(["backofficeEntryPoint", "entryPoint"]), Ee = async (e) => typeof e == "function" ? e() : import(
  /* @vite-ignore */
  String(e)
), L = (e) => typeof requestIdleCallback == "function" ? requestIdleCallback(() => e()) : void setTimeout(e, 0);
function Te(e, t, r, n, i = L) {
  if (t.length === 0) return;
  const c = r ? t.filter((s) => !e.isRegistered(s)) : [];
  for (const s of t) e.exclude(s);
  c.length > 0 && i(() => {
    for (const s of c) n(be(s));
  });
}
function je(e) {
  if (typeof e != "object" || e === null) return;
  const { aliases: t, warnUnknownAliases: r } = e;
  if (!(!Array.isArray(t) || !t.every((n) => typeof n == "string")))
    return { aliases: t, warnUnknownAliases: r !== !1 };
}
async function Ae({
  load: e,
  registry: t,
  warn: r,
  now: n = () => performance.now(),
  sleep: i = (o) => new Promise((a) => setTimeout(a, o)),
  idle: c = L,
  importModule: s = Ee
}) {
  const o = n();
  let a;
  try {
    a = je(await e());
  } catch {
    a = void 0;
  }
  if (!a) {
    r(pe);
    return;
  }
  const m = me - (n() - o);
  m > 0 && await i(m);
  const f = (y) => {
    const h = t.getByAlias?.(y);
    return h !== void 0 && Se.has(h.type ?? "") && h.js !== void 0;
  }, l = a.aliases.filter(f);
  Te(
    t,
    a.aliases.filter((y) => !l.includes(y)),
    a.warnUnknownAliases,
    r,
    c
  ), await Promise.all(
    l.map(async (y) => {
      const h = t.getByAlias?.(y)?.js;
      let u = !1;
      await Promise.race([
        Promise.resolve().then(() => s(h)).catch(() => {
        }),
        i(ge).then(() => {
          u = !0;
        })
      ]), u || await i(we), t.exclude(y);
    })
  );
}
const ve = (e, t) => {
  Ae({
    load: async () => {
      const { data: r, error: n } = await G(e, ye({ client: Y }), {
        disableNotifications: !0
      });
      if (n) throw n;
      return r;
    },
    registry: t,
    warn: (r) => console.warn(r)
  });
}, Oe = () => {
};
export {
  ve as onInit,
  Oe as onUnload
};
