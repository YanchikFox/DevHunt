// DevHunt — Search page (global)

function SearchPage({ setRoute }) {
  const [q, setQ] = React.useState('');
  const [tab, setTab] = React.useState('all'); // all | projects | people | tasks | posts
  const [filters, setFilters] = React.useState({
    stage: 'any',          // any | idea | recruiting | active | beta | release
    hasRoles: false,       // projects only
    stack: new Set(),      // projects, people
    sort: 'relevance',     // relevance | recent | popular
  });
  const inputRef = React.useRef();

  React.useEffect(() => { inputRef.current?.focus(); }, []);
  React.useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') setQ(''); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const qq = q.trim().toLowerCase();
  const matches = (s) => !qq || String(s).toLowerCase().includes(qq);
  const matchAny = (...xs) => xs.some(matches);

  // ─── Query results ────────────────────────────────────────────
  const projectResults = MOCK.projects.filter(p =>
    (filters.stage === 'any' || p.stage === filters.stage) &&
    (!filters.hasRoles || p.openRoles > 0) &&
    (filters.stack.size === 0 || p.stack.some(s => filters.stack.has(s))) &&
    (matchAny(p.name, p.tagline, p.description, p.stack.join(' ')))
  );

  const peopleResults = [MOCK.me, ...MOCK.users].filter(u =>
    (filters.stack.size === 0 || (u.skills || []).some(s => filters.stack.has(s))) &&
    matchAny(u.name, u.title, (u.skills || []).join(' '))
  );

  const taskResults = MOCK.tasks.filter(t => matchAny(t.title, t.assignee, t.tags.join(' ')));

  const postResults = MOCK.feed.filter(f => matchAny(f.text, f.author, f.project, f.tag));

  const totalCount = projectResults.length + peopleResults.length + taskResults.length + postResults.length;

  // Popular stacks for filter chips
  const allStacks = Array.from(new Set(MOCK.projects.flatMap(p => p.stack)));

  const toggleStack = (s) => {
    setFilters(f => {
      const next = new Set(f.stack);
      if (next.has(s)) next.delete(s); else next.add(s);
      return { ...f, stack: next };
    });
  };

  // Tab counts
  const tabs = [
    { id: 'all', label: 'All', count: totalCount },
    { id: 'projects', label: 'Projects', count: projectResults.length },
    { id: 'people', label: 'People', count: peopleResults.length },
    { id: 'tasks', label: 'Tasks', count: taskResults.length },
    { id: 'posts', label: 'Posts', count: postResults.length },
  ];

  // ─── Highlight helper ─────────────────────────────────────────
  const Highlight = ({ text }) => {
    if (!qq) return <>{text}</>;
    const s = String(text);
    const i = s.toLowerCase().indexOf(qq);
    if (i < 0) return <>{s}</>;
    return <>
      {s.slice(0, i)}
      <mark style={{ background: 'var(--accent-weak)', color: 'var(--accent)', padding: '0 2px', borderRadius: 3 }}>{s.slice(i, i + qq.length)}</mark>
      {s.slice(i + qq.length)}
    </>;
  };

  // ─── Recent / suggested when query empty ──────────────────────
  const SUGGESTED = ['Rust', 'Postgres', 'React', 'TypeScript', 'open roles', 'design'];
  const RECENT = ['postgres profiler', 'priya shah', 'ios role', 'plan-diff'];
  const TRENDING = [
    { type: 'project', label: 'Drift', sub: 'System design canvas', go: () => setRoute('projects/p3') },
    { type: 'project', label: 'Helix', sub: 'Postgres profiler', go: () => setRoute('projects/p1') },
    { type: 'person', label: 'Mira Chen', sub: 'ML Engineer', go: () => setRoute('profile') },
    { type: 'tag', label: '#launch-week', sub: '142 posts', go: () => setRoute('community') },
  ];

  return (
    <div style={{ padding: 'calc(24px * var(--d)) calc(28px * var(--d))', maxWidth: 1200, margin: '0 auto' }}>
      {/* Header */}
      <div style={{ marginBottom: 20 }}>
        <div className="caption" style={{ marginBottom: 6 }}>[Search]</div>
        <div style={{
          position: 'relative', display: 'flex', alignItems: 'center', gap: 8,
          padding: '4px 10px', background: 'var(--bg-elev)',
          border: '1px solid var(--border)', borderRadius: 'var(--r-lg)',
          boxShadow: 'var(--shadow-sm)',
        }}>
          <Icon name="search" size={18} style={{ color: 'var(--text-muted)' }} />
          <input ref={inputRef} value={q} onChange={e => setQ(e.target.value)}
            placeholder="Search projects, people, tasks, posts…"
            style={{
              flex: 1, height: 48, border: 'none', outline: 'none',
              background: 'transparent', color: 'var(--text)',
              fontSize: 18, letterSpacing: -0.2,
            }} />
          {q && (
            <button onClick={() => { setQ(''); inputRef.current?.focus(); }}
              className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}>
              <Icon name="x" size={14} />
            </button>
          )}
          <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', padding: '3px 7px', background: 'var(--bg-subtle)', border: '1px solid var(--border)', borderRadius: 4 }}>esc</span>
        </div>
        {q && (
          <div style={{ marginTop: 10, fontSize: 12, color: 'var(--text-muted)' }}>
            <span className="mono">{totalCount}</span> results for <span style={{ color: 'var(--text)' }}>"{q}"</span>
          </div>
        )}
      </div>

      {/* Empty state — trending / recent / suggested */}
      {!q && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
          <div className="card" style={{ padding: 16 }}>
            <div className="caption" style={{ marginBottom: 10 }}>[Trending now]</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {TRENDING.map((t, i) => (
                <button key={i} onClick={t.go}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 10,
                    padding: '8px 10px', border: 'none', background: 'transparent',
                    borderRadius: 'var(--r-sm)', cursor: 'pointer', textAlign: 'left',
                  }}
                  onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', width: 18 }}>0{i + 1}</span>
                  <Icon name={t.type === 'project' ? 'folder' : t.type === 'person' ? 'user' : 'hash'} size={13} style={{ color: 'var(--text-muted)' }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 13, fontWeight: 500 }}>{t.label}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{t.sub}</div>
                  </div>
                  <Icon name="chevronRight" size={13} style={{ color: 'var(--text-muted)' }} />
                </button>
              ))}
            </div>
          </div>

          <div className="card" style={{ padding: 16 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
              <div className="caption">[Recent]</div>
              <button style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', fontSize: 10, cursor: 'pointer', fontFamily: 'var(--font-mono)' }}>clear</button>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {RECENT.map((r, i) => (
                <button key={i} onClick={() => setQ(r)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 10,
                    padding: '8px 10px', border: 'none', background: 'transparent',
                    borderRadius: 'var(--r-sm)', cursor: 'pointer', textAlign: 'left',
                    fontSize: 12.5, color: 'var(--text)',
                  }}
                  onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <Icon name="clock" size={12} style={{ color: 'var(--text-muted)' }} />
                  <span style={{ flex: 1 }}>{r}</span>
                  <Icon name="arrowUpRight" size={11} style={{ color: 'var(--text-muted)' }} />
                </button>
              ))}
            </div>
          </div>

          <div className="card" style={{ padding: 16 }}>
            <div className="caption" style={{ marginBottom: 10 }}>[Try searching for]</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {SUGGESTED.map(s => (
                <button key={s} onClick={() => setQ(s)} className="chip" style={{ cursor: 'pointer' }}>
                  {s}
                </button>
              ))}
            </div>
            <div className="caption" style={{ marginTop: 18, marginBottom: 10 }}>[Operators]</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, fontSize: 12 }}>
              {[
                ['stack:rust', 'filter by tech stack'],
                ['role:open', 'only projects with open roles'],
                ['by:@user', 'posts or tasks by a user'],
                ['stage:beta', 'projects at a given stage'],
              ].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', gap: 10 }}>
                  <code className="mono" style={{ fontSize: 11, padding: '1px 6px', background: 'var(--bg-subtle)', borderRadius: 3, color: 'var(--accent)' }}>{k}</code>
                  <span style={{ color: 'var(--text-muted)' }}>{v}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Query active — tabs + filters + results */}
      {q && (
        <>
          {/* Tabs */}
          <div style={{ display: 'flex', gap: 2, borderBottom: '1px solid var(--border)', marginTop: 8 }}>
            {tabs.map(t => (
              <button key={t.id} onClick={() => setTab(t.id)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 6,
                  padding: '10px 14px', border: 'none', background: 'transparent',
                  borderBottom: `2px solid ${tab === t.id ? 'var(--accent)' : 'transparent'}`,
                  color: tab === t.id ? 'var(--text)' : 'var(--text-muted)',
                  fontSize: 13, fontWeight: 500, cursor: 'pointer',
                }}>
                {t.label}
                <span className="mono" style={{ fontSize: 10, padding: '1px 6px', background: tab === t.id ? 'var(--accent-weak)' : 'var(--bg-subtle)', color: tab === t.id ? 'var(--accent)' : 'var(--text-muted)', borderRadius: 999 }}>{t.count}</span>
              </button>
            ))}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '220px 1fr', gap: 20, marginTop: 20 }}>
            {/* Filters */}
            <aside style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
              <div>
                <div className="caption" style={{ marginBottom: 8 }}>[Sort]</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  {[['relevance', 'Relevance'], ['recent', 'Most recent'], ['popular', 'Most popular']].map(([v, l]) => (
                    <button key={v} onClick={() => setFilters(f => ({ ...f, sort: v }))}
                      style={{
                        padding: '6px 8px', border: 'none', background: filters.sort === v ? 'var(--bg-subtle)' : 'transparent',
                        borderRadius: 'var(--r-sm)', textAlign: 'left', fontSize: 12,
                        color: filters.sort === v ? 'var(--text)' : 'var(--text-muted)',
                        fontWeight: filters.sort === v ? 500 : 400, cursor: 'pointer',
                      }}>
                      {l}
                    </button>
                  ))}
                </div>
              </div>

              {(tab === 'all' || tab === 'projects') && (
                <div>
                  <div className="caption" style={{ marginBottom: 8 }}>[Stage]</div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    {['any','idea','recruiting','active','beta','release'].map(s => (
                      <button key={s} onClick={() => setFilters(f => ({ ...f, stage: s }))}
                        style={{
                          padding: '6px 8px', border: 'none', background: filters.stage === s ? 'var(--bg-subtle)' : 'transparent',
                          borderRadius: 'var(--r-sm)', textAlign: 'left', fontSize: 12, textTransform: 'capitalize',
                          color: filters.stage === s ? 'var(--text)' : 'var(--text-muted)',
                          fontWeight: filters.stage === s ? 500 : 400, cursor: 'pointer',
                        }}>
                        {s}
                      </button>
                    ))}
                  </div>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 12, fontSize: 12, color: 'var(--text-muted)', cursor: 'pointer' }}>
                    <input type="checkbox" checked={filters.hasRoles}
                      onChange={e => setFilters(f => ({ ...f, hasRoles: e.target.checked }))} />
                    Open roles only
                  </label>
                </div>
              )}

              <div>
                <div className="caption" style={{ marginBottom: 8 }}>[Stack]</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                  {allStacks.map(s => {
                    const active = filters.stack.has(s);
                    return (
                      <button key={s} onClick={() => toggleStack(s)}
                        className="chip"
                        style={{
                          cursor: 'pointer', fontSize: 11,
                          background: active ? 'var(--accent-weak)' : 'var(--bg-subtle)',
                          color: active ? 'var(--accent)' : 'var(--text-muted)',
                          borderColor: active ? 'var(--accent)' : 'var(--border)',
                        }}>
                        {s}
                      </button>
                    );
                  })}
                </div>
              </div>

              {(filters.stack.size > 0 || filters.stage !== 'any' || filters.hasRoles) && (
                <button onClick={() => setFilters({ stage: 'any', hasRoles: false, stack: new Set(), sort: 'relevance' })}
                  className="btn btn-sm" style={{ justifyContent: 'center' }}>
                  Clear filters
                </button>
              )}
            </aside>

            {/* Results */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 24, minWidth: 0 }}>
              {(tab === 'all' || tab === 'projects') && projectResults.length > 0 && (
                <section>
                  {tab === 'all' && <div className="caption" style={{ marginBottom: 10 }}>[Projects · {projectResults.length}]</div>}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {projectResults.map(p => (
                      <button key={p.id} onClick={() => setRoute('projects/' + p.id)}
                        className="card" style={{ padding: 14, display: 'flex', gap: 14, alignItems: 'center', textAlign: 'left', cursor: 'pointer', border: '1px solid var(--border)' }}>
                        <div style={{
                          width: 44, height: 44, borderRadius: 10, flexShrink: 0,
                          background: `oklch(0.72 0.14 ${p.name.charCodeAt(0) * 7 % 360})`, color: '#fff',
                          display: 'flex', alignItems: 'center', justifyContent: 'center',
                          fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 18,
                        }}>{p.name[0]}</div>
                        <div style={{ flex: 1, minWidth: 0 }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 2 }}>
                            <span style={{ fontSize: 14, fontWeight: 600 }}><Highlight text={p.name} /></span>
                            <span className="chip" style={{ fontSize: 10 }}>{p.stage}</span>
                            <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>/{p.tag}</span>
                          </div>
                          <div style={{ fontSize: 12.5, color: 'var(--text-muted)', marginBottom: 6, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            <Highlight text={p.tagline} />
                          </div>
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                            {p.stack.map(s => <span key={s} className="chip" style={{ fontSize: 10 }}><Highlight text={s} /></span>)}
                          </div>
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4, flexShrink: 0 }}>
                          <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'flex', gap: 10 }}>
                            <span><Icon name="zap" size={11} /> {p.stars}</span>
                            <span><Icon name="users" size={11} /> {p.members}</span>
                          </div>
                          {p.openRoles > 0 && <span className="chip accent" style={{ fontSize: 10 }}>{p.openRoles} open</span>}
                        </div>
                      </button>
                    ))}
                  </div>
                </section>
              )}

              {(tab === 'all' || tab === 'people') && peopleResults.length > 0 && (
                <section>
                  {tab === 'all' && <div className="caption" style={{ marginBottom: 10 }}>[People · {peopleResults.length}]</div>}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 10 }}>
                    {peopleResults.map(u => (
                      <button key={u.id} onClick={() => setRoute('profile')}
                        className="card" style={{ padding: 14, display: 'flex', gap: 12, alignItems: 'center', textAlign: 'left', cursor: 'pointer' }}>
                        <Avatar name={u.name} size={40} />
                        <div style={{ flex: 1, minWidth: 0 }}>
                          <div style={{ fontSize: 13, fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            <Highlight text={u.name} />
                          </div>
                          <div style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            <Highlight text={u.title} />
                          </div>
                          <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', marginTop: 6 }}>
                            {(u.skills || []).slice(0, 3).map(s => <span key={s} className="chip" style={{ fontSize: 9 }}><Highlight text={s} /></span>)}
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>
                </section>
              )}

              {(tab === 'all' || tab === 'tasks') && taskResults.length > 0 && (
                <section>
                  {tab === 'all' && <div className="caption" style={{ marginBottom: 10 }}>[Tasks · {taskResults.length}]</div>}
                  <div className="card" style={{ overflow: 'hidden' }}>
                    {taskResults.map((t, i) => (
                      <button key={t.id} onClick={() => setRoute('board/p1')}
                        style={{
                          width: '100%', display: 'flex', gap: 12, alignItems: 'center',
                          padding: '10px 14px', border: 'none', background: 'transparent',
                          borderBottom: i < taskResults.length - 1 ? '1px solid var(--border)' : 'none',
                          cursor: 'pointer', textAlign: 'left',
                        }}
                        onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
                        onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                        <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', width: 28 }}>#{t.id}</span>
                        <span className="chip" style={{ fontSize: 9, textTransform: 'uppercase' }}>{t.col}</span>
                        <span style={{ flex: 1, fontSize: 13 }}><Highlight text={t.title} /></span>
                        <Avatar name={t.assignee} size={20} />
                      </button>
                    ))}
                  </div>
                </section>
              )}

              {(tab === 'all' || tab === 'posts') && postResults.length > 0 && (
                <section>
                  {tab === 'all' && <div className="caption" style={{ marginBottom: 10 }}>[Posts · {postResults.length}]</div>}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {postResults.map(f => (
                      <button key={f.id} onClick={() => setRoute('community')}
                        className="card" style={{ padding: 14, display: 'flex', gap: 12, textAlign: 'left', cursor: 'pointer' }}>
                        <Avatar name={f.author} size={32} />
                        <div style={{ flex: 1, minWidth: 0 }}>
                          <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4 }}>
                            <span style={{ fontSize: 12.5, fontWeight: 500 }}><Highlight text={f.author} /></span>
                            {f.project && <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>/{f.project}</span>}
                            <span className="chip" style={{ fontSize: 9 }}>{f.tag}</span>
                            <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>{f.time}</span>
                          </div>
                          <div style={{ fontSize: 13, lineHeight: 1.5, color: 'var(--text-muted)' }}>
                            <Highlight text={f.text} />
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>
                </section>
              )}

              {totalCount === 0 && (
                <div style={{ padding: 40, textAlign: 'center', color: 'var(--text-muted)' }}>
                  <Icon name="search" size={32} style={{ opacity: 0.4, marginBottom: 12 }} />
                  <div style={{ fontSize: 14, marginBottom: 4 }}>No results for "{q}"</div>
                  <div style={{ fontSize: 12 }}>Try different keywords or clear filters.</div>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

Object.assign(window, { SearchPage });
