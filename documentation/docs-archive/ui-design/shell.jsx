// DevHunt — shell, nav, tweaks panel, router

const NAV_ITEMS = [
  { id: 'home', label: 'Home', icon: 'home' },
  { id: 'projects', label: 'Projects', icon: 'folder' },
  { id: 'ai', label: 'AI Plan', icon: 'sparkle', badge: 'AI' },
  { id: 'community', label: 'Community', icon: 'community' },
  { id: 'chat', label: 'Messages', icon: 'chat' },
  { id: 'admin', label: 'Moderation', icon: 'shield' },
];

const Logo = ({ size = 20 }) => (
  <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
    <div style={{
      width: size + 4, height: size + 4,
      background: 'var(--accent)', color: 'var(--accent-fg)',
      borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-mono)', fontWeight: 700, fontSize: size - 6,
    }}>◢</div>
    <span style={{ fontWeight: 600, fontSize: size - 4, letterSpacing: -0.3 }}>DevHunt</span>
  </div>
);

function Sidebar({ route, setRoute, compact }) {
  return (
    <aside style={{
      background: 'var(--bg-subtle)',
      borderRight: '1px solid var(--border)',
      display: 'flex', flexDirection: 'column',
      padding: compact ? '16px 8px' : '18px 12px',
      overflow: 'hidden',
    }} className="hide-mobile">
      <div style={{ padding: compact ? '4px' : '4px 8px 16px' }}>
        {compact ? <Logo size={16} /> : <Logo />}
      </div>
      <nav style={{ display: 'flex', flexDirection: 'column', gap: 2, flex: 1, marginTop: 8 }}>
        {NAV_ITEMS.map(item => {
          const active = route.startsWith(item.id);
          return (
            <button key={item.id} onClick={() => setRoute(item.id)}
              title={compact ? item.label : ''}
              style={{
                display: 'flex', alignItems: 'center', gap: 10,
                padding: compact ? '10px' : '8px 10px',
                justifyContent: compact ? 'center' : 'flex-start',
                borderRadius: 'var(--r-md)',
                border: 'none',
                background: active ? 'var(--bg-elev)' : 'transparent',
                color: active ? 'var(--text)' : 'var(--text-muted)',
                fontSize: 13, fontWeight: active ? 500 : 400,
                textAlign: 'left',
                boxShadow: active ? 'var(--shadow-sm)' : 'none',
              }}>
              <Icon name={item.icon} size={16} />
              {!compact && <span style={{ flex: 1 }}>{item.label}</span>}
              {!compact && item.badge && (
                <span className="mono" style={{
                  fontSize: 9, padding: '2px 5px',
                  background: 'var(--accent-weak)', color: 'var(--accent)',
                  borderRadius: 4, letterSpacing: 0.05,
                }}>{item.badge}</span>
              )}
            </button>
          );
        })}
      </nav>
      {!compact && (
        <>
          <div style={{ margin: '16px 6px 8px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span className="caption">[My projects]</span>
            <button onClick={() => setRoute('projects')}
              style={{ background: 'transparent', border: 'none', padding: 0, cursor: 'pointer', color: 'var(--text-muted)', display: 'flex' }}
              title="All projects">
              <Icon name="plus" size={12} />
            </button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1, marginBottom: 12 }}>
            {MOCK.projects.slice(0, 4).map(p => {
              const active = route === 'projects/' + p.id || route === 'board/' + p.id;
              return (
                <button key={p.id} onClick={() => setRoute('projects/' + p.id)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 8,
                    padding: '6px 10px', border: 'none',
                    background: active ? 'var(--bg-elev)' : 'transparent',
                    borderRadius: 'var(--r-sm)', cursor: 'pointer', textAlign: 'left',
                    color: active ? 'var(--text)' : 'var(--text-muted)',
                    fontSize: 12,
                  }}
                  onMouseEnter={e => !active && (e.currentTarget.style.background = 'var(--bg-hover)')}
                  onMouseLeave={e => !active && (e.currentTarget.style.background = 'transparent')}>
                  <div style={{
                    width: 18, height: 18, borderRadius: 4,
                    background: `oklch(0.72 0.14 ${p.name.charCodeAt(0) * 7 % 360})`, color: '#fff',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600, flexShrink: 0,
                  }}>{p.name[0]}</div>
                  <span style={{ flex: 1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.name}</span>
                  {p.openRoles > 0 && (
                    <span className="mono" style={{ fontSize: 9, color: 'var(--warning)' }}>{p.openRoles}</span>
                  )}
                </button>
              );
            })}
          </div>

          <div style={{ margin: '4px 6px 8px' }}>
            <span className="caption">[AI match]</span>
          </div>
          <button onClick={() => setRoute('ai')}
            style={{
              display: 'flex', flexDirection: 'column', gap: 6,
              padding: 10, border: '1px solid var(--border)',
              borderRadius: 'var(--r-md)', background: 'var(--bg-elev)',
              cursor: 'pointer', textAlign: 'left', marginBottom: 10,
            }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Icon name="sparkle" size={12} style={{ color: 'var(--accent)' }} />
              <span style={{ fontSize: 11.5, fontWeight: 500 }}>3 new matches</span>
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', lineHeight: 1.45 }}>
              Rust backend role on <span style={{ color: 'var(--text)' }}>Helix</span> — 92% fit
            </div>
            <div style={{ display: 'flex', marginTop: 2 }}>
              {MOCK.users.slice(0, 3).map((u, i) => (
                <div key={u.id} style={{ marginLeft: i === 0 ? 0 : -6 }}>
                  <Avatar name={u.name} size={20} />
                </div>
              ))}
              <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginLeft: 8, alignSelf: 'center' }}>+ 2</span>
            </div>
          </button>

          <div style={{ margin: '0 6px 6px' }}>
            <span className="caption">[Upcoming]</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2, fontSize: 11.5 }}>
            {[
              ['Product Hunt launch', 'Apr 22'],
              ['Berlin Hack Night', 'Apr 23'],
            ].map(([t, d]) => (
              <div key={t} style={{ display: 'flex', justifyContent: 'space-between', gap: 6, padding: '4px 10px', color: 'var(--text-muted)' }}>
                <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{t}</span>
                <span className="mono" style={{ fontSize: 10, flexShrink: 0 }}>{d}</span>
              </div>
            ))}
          </div>
        </>
      )}
    </aside>
  );
}

function Topbar({ route, setRoute, theme, setTheme, setMobileOpen }) {
  const pageName = NAV_ITEMS.find(n => route.startsWith(n.id))?.label || 'Home';
  return (
    <header style={{
      display: 'flex', alignItems: 'center', gap: 12,
      padding: '10px 18px',
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)',
      minHeight: 56,
    }}>
      <button className="btn-ghost show-mobile" onClick={() => setMobileOpen(v => !v)}
        style={{ padding: 8, border: 'none', background: 'transparent' }}>
        <Icon name="menu" size={18} />
      </button>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span className="caption">[{String(NAV_ITEMS.findIndex(n=>n.id===route.split('/')[0])+1).padStart(2,'0')}]</span>
        <span style={{ fontSize: 14, fontWeight: 500 }}>{pageName}</span>
      </div>
      <div style={{ flex: 1 }} />
      <button onClick={() => setRoute('search')} className="hide-mobile"
        style={{
          position: 'relative', width: 280, height: 34, padding: '0 10px 0 32px',
          background: 'var(--bg-subtle)', border: '1px solid var(--border)',
          borderRadius: 'var(--r-md)', cursor: 'pointer', textAlign: 'left',
          color: 'var(--text-muted)', fontSize: 12, fontFamily: 'inherit',
          display: 'flex', alignItems: 'center',
        }}>
        <Icon name="search" size={14} style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
        Search projects, people, tasks…
        <kbd className="mono" style={{
          position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)',
          fontSize: 10, padding: '2px 5px',
          background: 'var(--bg)', border: '1px solid var(--border)',
          borderRadius: 4, color: 'var(--text-muted)',
        }}>⌘K</kbd>
      </button>
      <button className="btn-ghost" onClick={() => setTheme(t => t === 'dark' ? 'light' : 'dark')}
        style={{ padding: 8, border: 'none', background: 'transparent' }} title="Toggle theme">
        <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={16} />
      </button>
      <button className="btn-ghost" onClick={() => setRoute('notifications')}
        style={{ padding: 8, border: 'none', background: 'transparent', position: 'relative' }}>
        <Icon name="bell" size={16} />
        <span style={{
          position: 'absolute', top: 6, right: 6,
          width: 6, height: 6, borderRadius: 999,
          background: 'var(--accent)',
        }} />
      </button>
      <button className="btn btn-primary btn-sm hide-mobile">
        <Icon name="plus" size={13} /> New project
      </button>
      <AvatarMenu setRoute={setRoute} />
    </header>
  );
}

function AvatarMenu({ setRoute }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef();
  React.useEffect(() => {
    if (!open) return;
    const onDoc = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);
  const go = (r) => { setRoute(r); setOpen(false); };
  const restart = () => {
    try {
      localStorage.setItem('dh:entered', 'false');
      localStorage.setItem('dh:authed', 'false');
    } catch {}
    location.reload();
  };
  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button onClick={() => setOpen(v => !v)}
        style={{
          display: 'flex', alignItems: 'center', gap: 6,
          padding: 2, border: '1px solid transparent',
          borderRadius: 999, background: open ? 'var(--bg-hover)' : 'transparent',
          cursor: 'pointer',
        }}>
        <Avatar name={MOCK.me.name} size={30} />
      </button>
      {open && (
        <div className="fade-in" style={{
          position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 150,
          width: 240, background: 'var(--bg-elev)',
          border: '1px solid var(--border)', borderRadius: 'var(--r-lg)',
          boxShadow: 'var(--shadow-lg)', overflow: 'hidden',
        }}>
          <button onClick={() => go('profile')}
            style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 10, padding: '12px 14px', border: 'none', background: 'transparent', cursor: 'pointer', borderBottom: '1px solid var(--border)', textAlign: 'left' }}>
            <Avatar name={MOCK.me.name} size={36} />
            <div style={{ minWidth: 0 }}>
              <div style={{ fontSize: 13, fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{MOCK.me.name}</div>
              <div style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>@{MOCK.me.username}</div>
            </div>
          </button>
          {[
            ['profile', 'user', 'View profile'],
            ['profile/edit', 'edit', 'Edit profile'],
            ['settings', 'settings', 'Settings & privacy'],
          ].map(([r, ic, label], i) => (
            <button key={label} onClick={() => go(r)}
              style={{
                width: '100%', display: 'flex', alignItems: 'center', gap: 10,
                padding: '9px 14px', border: 'none', background: 'transparent', cursor: 'pointer',
                textAlign: 'left', fontSize: 13, color: 'var(--text)',
              }}
              onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
              onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
              <Icon name={ic} size={14} style={{ color: 'var(--text-muted)' }} />
              <span>{label}</span>
            </button>
          ))}
          <div style={{ borderTop: '1px solid var(--border)' }}>
            <button onClick={restart}
              style={{
                width: '100%', display: 'flex', alignItems: 'center', gap: 10,
                padding: '9px 14px', border: 'none', background: 'transparent', cursor: 'pointer',
                textAlign: 'left', fontSize: 13, color: 'var(--text)',
              }}
              onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
              onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
              <Icon name="logout" size={14} style={{ color: 'var(--text-muted)' }} />
              <span>Sign out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function MobileNav({ route, setRoute, open, setOpen }) {
  if (!open) return null;
  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 100,
      background: 'var(--overlay)',
    }} onClick={() => setOpen(false)}>
      <div style={{
        position: 'absolute', top: 0, left: 0, bottom: 0,
        width: 260, background: 'var(--bg)',
        padding: 18, display: 'flex', flexDirection: 'column', gap: 4,
      }} onClick={e => e.stopPropagation()}>
        <div style={{ marginBottom: 12 }}><Logo /></div>
        {NAV_ITEMS.map(item => (
          <button key={item.id} onClick={() => { setRoute(item.id); setOpen(false); }}
            style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '10px 12px', border: 'none',
              background: route.startsWith(item.id) ? 'var(--bg-subtle)' : 'transparent',
              borderRadius: 'var(--r-md)', color: 'var(--text)', fontSize: 14, textAlign: 'left',
            }}>
            <Icon name={item.icon} size={16} />{item.label}
          </button>
        ))}
      </div>
    </div>
  );
}

// ── Tweaks panel ─────────────────────────────────────────────────
const TWEAKS_DEFAULT = /*EDITMODE-BEGIN*/{
  "accentHue": 275,
  "density": 1,
  "layout": "sidebar",
  "cardStyle": "outline",
  "font": "geist"
}/*EDITMODE-END*/;

const FONT_STACKS = {
  geist: { sans: '"Geist", ui-sans-serif, system-ui, sans-serif', serif: '"Instrument Serif", Georgia, serif', mono: '"Geist Mono", ui-monospace, monospace' },
  inter: { sans: '"Inter", system-ui, sans-serif', serif: '"Fraunces", Georgia, serif', mono: '"JetBrains Mono", monospace' },
  serif: { sans: '"Instrument Serif", Georgia, serif', serif: '"Instrument Serif", Georgia, serif', mono: '"Geist Mono", monospace' },
  mono: { sans: '"Geist Mono", ui-monospace, monospace', serif: '"Instrument Serif", Georgia, serif', mono: '"Geist Mono", monospace' },
};

function TweaksPanel({ tweaks, setTweaks, onClose }) {
  const set = (k, v) => setTweaks(t => ({ ...t, [k]: v }));
  return (
    <div style={{
      position: 'fixed', bottom: 20, right: 20, zIndex: 200,
      width: 320, padding: 16,
      background: 'var(--bg-elev)', border: '1px solid var(--border)',
      borderRadius: 'var(--r-lg)', boxShadow: 'var(--shadow-lg)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Icon name="settings" size={14} />
          <span style={{ fontSize: 13, fontWeight: 600 }}>Tweaks</span>
        </div>
        <button onClick={onClose} style={{ background: 'transparent', border: 'none', padding: 4, color: 'var(--text-muted)' }}>
          <Icon name="x" size={14} />
        </button>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        <TweakRow label="Accent hue">
          <input type="range" min="0" max="360" step="5" value={tweaks.accentHue}
            onChange={e => set('accentHue', +e.target.value)}
            style={{ flex: 1, accentColor: `oklch(0.58 0.22 ${tweaks.accentHue})` }} />
          <div style={{ width: 24, height: 20, borderRadius: 4, background: `oklch(0.58 0.22 ${tweaks.accentHue})` }} />
        </TweakRow>
        <TweakRow label="Density">
          <SegButtons value={tweaks.density} onChange={v => set('density', v)}
            options={[[0.85, 'Compact'], [1, 'Normal'], [1.15, 'Roomy']]} />
        </TweakRow>
        <TweakRow label="Layout">
          <SegButtons value={tweaks.layout} onChange={v => set('layout', v)}
            options={[['sidebar', 'Sidebar'], ['compact', 'Rail'], ['topbar', 'Topbar']]} />
        </TweakRow>
        <TweakRow label="Card style">
          <SegButtons value={tweaks.cardStyle} onChange={v => set('cardStyle', v)}
            options={[['outline', 'Outline'], ['filled', 'Filled'], ['ghost', 'Ghost']]} />
        </TweakRow>
        <TweakRow label="Typography">
          <SegButtons value={tweaks.font} onChange={v => set('font', v)}
            options={[['geist','Geist'],['inter','Inter'],['serif','Serif'],['mono','Mono']]} />
        </TweakRow>
      </div>
    </div>
  );
}

const TweakRow = ({ label, children }) => (
  <div>
    <div className="caption" style={{ marginBottom: 6 }}>{label}</div>
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>{children}</div>
  </div>
);
const SegButtons = ({ value, onChange, options }) => (
  <div style={{ display: 'flex', background: 'var(--bg-subtle)', padding: 2, borderRadius: 'var(--r-md)', gap: 2, flex: 1 }}>
    {options.map(([v, l]) => (
      <button key={String(v)} onClick={() => onChange(v)} style={{
        flex: 1, padding: '5px 6px',
        borderRadius: 6, border: 'none',
        background: value === v ? 'var(--bg-elev)' : 'transparent',
        boxShadow: value === v ? 'var(--shadow-sm)' : 'none',
        color: value === v ? 'var(--text)' : 'var(--text-muted)',
        fontSize: 11, fontWeight: 500,
      }}>{l}</button>
    ))}
  </div>
);

Object.assign(window, { Sidebar, Topbar, MobileNav, Logo, TweaksPanel, TWEAKS_DEFAULT, FONT_STACKS, NAV_ITEMS, AvatarMenu });
