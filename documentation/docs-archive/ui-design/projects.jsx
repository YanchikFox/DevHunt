// DevHunt — Projects listing, Project detail, Kanban

function ProjectsList({ setRoute, cardStyle }) {
  const [view, setView] = React.useState('grid');
  const [filter, setFilter] = React.useState('all');
  const [query, setQuery] = React.useState('');
  const filtered = MOCK.projects.filter(p =>
    (filter === 'all' || p.stage === filter) &&
    (query === '' || p.name.toLowerCase().includes(query.toLowerCase()) || p.tagline.toLowerCase().includes(query.toLowerCase()))
  );
  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 1280, margin: '0 auto' }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', marginBottom: 24, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <div className="caption" style={{ marginBottom: 6 }}>[Explore · {filtered.length} results]</div>
          <h1 className="serif" style={{ fontSize: 36, margin: 0, letterSpacing: -0.6, fontWeight: 400 }}>Projects</h1>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-sm"><Icon name="filter" size={12} /> Filters</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> New</button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 10, marginBottom: 20, flexWrap: 'wrap' }}>
        <div style={{ position: 'relative', flex: '1 1 280px' }}>
          <Icon name="search" size={14} style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
          <input className="input" placeholder="Search projects…" value={query} onChange={e => setQuery(e.target.value)} style={{ paddingLeft: 32 }} />
        </div>
        <div style={{ display: 'flex', gap: 4, background: 'var(--bg-subtle)', padding: 3, borderRadius: 'var(--r-md)' }}>
          {['all','idea','recruiting','active','beta'].map(s => (
            <button key={s} onClick={() => setFilter(s)} style={{
              padding: '5px 12px', border: 'none', borderRadius: 6,
              background: filter === s ? 'var(--bg-elev)' : 'transparent',
              boxShadow: filter === s ? 'var(--shadow-sm)' : 'none',
              fontSize: 12, textTransform: 'capitalize', fontWeight: 500,
              color: filter === s ? 'var(--text)' : 'var(--text-muted)',
            }}>{s}</button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 2, background: 'var(--bg-subtle)', padding: 3, borderRadius: 'var(--r-md)' }}>
          {[['grid','grid'],['list','list']].map(([v, ic]) => (
            <button key={v} onClick={() => setView(v)} style={{
              padding: 7, border: 'none', borderRadius: 6,
              background: view === v ? 'var(--bg-elev)' : 'transparent',
              boxShadow: view === v ? 'var(--shadow-sm)' : 'none',
              color: view === v ? 'var(--text)' : 'var(--text-muted)',
            }}><Icon name={ic} size={13} /></button>
          ))}
        </div>
      </div>

      {view === 'grid' ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
          {filtered.map(p => <ProjectCard key={p.id} project={p} onClick={() => setRoute('projects/' + p.id)} cardStyle={cardStyle} />)}
        </div>
      ) : (
        <div className="card" style={{ overflow: 'hidden' }}>
          {filtered.map((p, i) => (
            <div key={p.id} onClick={() => setRoute('projects/' + p.id)} style={{
              display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr 80px', gap: 16, alignItems: 'center',
              padding: '14px 18px', cursor: 'pointer',
              borderBottom: i < filtered.length - 1 ? '1px solid var(--border)' : 'none',
            }}>
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', minWidth: 0 }}>
                <div style={{
                  width: 32, height: 32, borderRadius: 8,
                  background: `oklch(0.72 0.12 ${p.name.charCodeAt(0) * 7 % 360})`, color: '#fff',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 13,
                }}>{p.name[0]}</div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: 500 }}>{p.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.tagline}</div>
                </div>
              </div>
              <div><span className="chip" style={{ background: STAGE_STYLE[p.stage]?.bg, color: STAGE_STYLE[p.stage]?.fg, borderColor: 'transparent' }}>{p.stage}</span></div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>{p.stack.slice(0,2).join(' · ')}</div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>
                <Icon name="users" size={11} /> {p.members} · <Icon name="zap" size={11} /> {p.stars}
              </div>
              <div className="mono" style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'right' }}>{p.updated}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function ProjectDetail({ projectId, setRoute, cardStyle }) {
  const p = MOCK.projects.find(x => x.id === projectId) || MOCK.projects[0];
  const [tab, setTab] = React.useState('overview');
  const stageStyle = STAGE_STYLE[p.stage] || STAGE_STYLE.active;
  return (
    <div style={{ overflowY: 'auto', height: '100%' }}>
      <div style={{
        height: 170, position: 'relative', overflow: 'hidden',
        background: `
          radial-gradient(55% 100% at 20% 80%, oklch(0.72 0.14 ${p.name.charCodeAt(0) * 7 % 360} / 0.35), transparent 70%),
          radial-gradient(55% 100% at 80% 20%, oklch(0.72 0.14 ${(p.name.charCodeAt(1) || 100) * 7 % 360} / 0.25), transparent 70%),
          linear-gradient(180deg, var(--bg-subtle), var(--bg))
        `,
      }}>
        <svg width="100%" height="100%" style={{ position: 'absolute', inset: 0, opacity: 0.14 }}>
          <defs>
            <pattern id={'pd-grid-' + p.id} width="28" height="28" patternUnits="userSpaceOnUse">
              <path d="M28 0H0V28" fill="none" stroke="currentColor" strokeWidth="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill={`url(#pd-grid-${p.id})`} style={{ color: 'var(--text-muted)' }} />
        </svg>
        <div className="mono" style={{
          position: 'absolute', right: 24, bottom: 14,
          fontSize: 11, color: 'var(--text-muted)', opacity: 0.6,
          letterSpacing: 0.5, display: 'flex', gap: 10,
        }}>
          <span>devhunt://project/{p.tag}</span>
          <span>·</span>
          <span>{p.stack.slice(0, 3).join(' · ')}</span>
        </div>
        <div style={{ position: 'absolute', inset: 0, background: 'linear-gradient(180deg, transparent 55%, var(--bg) 100%)' }} />
      </div>
      <div style={{ padding: '0 calc(28px * var(--d)) calc(28px * var(--d))', maxWidth: 1200, margin: '0 auto' }}>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', marginTop: -52, position: 'relative', flexWrap: 'wrap' }}>
          <div style={{
            width: 80, height: 80, borderRadius: 14,
            background: `oklch(0.72 0.14 ${p.name.charCodeAt(0) * 7 % 360})`, color: '#fff',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 36,
            border: '4px solid var(--bg)', boxShadow: 'var(--shadow-md)',
          }}>{p.name[0]}</div>
          <div style={{ flex: 1, paddingBottom: 4 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
              <h1 style={{ margin: 0, fontSize: 28, fontWeight: 600, letterSpacing: -0.4 }}>{p.name}</h1>
              <span className="chip" style={{ background: stageStyle.bg, color: stageStyle.fg, borderColor: 'transparent' }}>{p.stage}</span>
              <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>/{p.tag}</span>
            </div>
            <div style={{ fontSize: 14, color: 'var(--text-muted)' }}>{p.tagline}</div>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <button className="btn btn-sm"><Icon name="zap" size={12} /> Boost · {p.stars}</button>
            <button className="btn btn-sm"><Icon name="github" size={12} /> GitHub</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> Join team</button>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 4, marginTop: 28, borderBottom: '1px solid var(--border)' }}>
          {['overview','board','team','chat','activity','showcase','metrics'].map(t => (
            <button key={t} onClick={() => t === 'board' ? setRoute('board/' + p.id) : setTab(t)}
              style={{
                padding: '10px 14px', border: 'none', background: 'transparent',
                borderBottom: `2px solid ${tab === t ? 'var(--accent)' : 'transparent'}`,
                color: tab === t ? 'var(--text)' : 'var(--text-muted)',
                fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
              }}>{t}</button>
          ))}
        </div>

        {tab === 'overview' && (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 300px', gap: 24, marginTop: 24 }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[About]</div>
                <p style={{ fontSize: 14, lineHeight: 1.6, margin: 0 }}>{p.description}</p>
                <p style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--text-muted)', marginTop: 12 }}>
                  We're building this in the open. Progress posts every Friday. Open to feedback, PRs, and collaborators — particularly strong on the Rust side of the house.
                </p>
              </section>
              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[Open roles]</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 10 }}>
                  {['Rust backend', 'DevRel / docs'].map(r => (
                    <div key={r} className="card" style={{ padding: 14 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                        <div style={{ fontSize: 13, fontWeight: 500 }}>{r}</div>
                        <span className="chip accent" style={{ fontSize: 10 }}>Open</span>
                      </div>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10 }}>~10 hrs/week · equity optional</div>
                      <button className="btn btn-sm" style={{ width: '100%', justifyContent: 'center' }}>Apply</button>
                    </div>
                  ))}
                </div>
              </section>
              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[Recent activity]</div>
                <div className="card" style={{ overflow: 'hidden' }}>
                  {[
                    ['Tomás Vega', 'closed', 'Investigate regression in explain parser', '2h'],
                    ['Alex K.', 'opened', 'Add plan-diff tooltip', '4h'],
                    ['github-bot', 'synced', '12 commits to main', '5h'],
                    ['Priya S.', 'updated', 'design tokens spec', '1d'],
                  ].map(([who, what, thing, when], i) => (
                    <div key={i} style={{ display: 'flex', gap: 12, padding: '12px 16px', borderBottom: i < 3 ? '1px solid var(--border)' : 'none', fontSize: 12 }}>
                      <Avatar name={who} size={24} />
                      <div style={{ flex: 1 }}>
                        <span style={{ fontWeight: 500 }}>{who}</span>{' '}
                        <span style={{ color: 'var(--text-muted)' }}>{what}</span>{' '}
                        <span style={{ color: 'var(--accent)' }}>{thing}</span>
                      </div>
                      <span className="mono" style={{ color: 'var(--text-muted)' }}>{when}</span>
                    </div>
                  ))}
                </div>
              </section>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className="card" style={{ padding: 14 }}>
                <div className="caption" style={{ marginBottom: 10 }}>[Tech stack]</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {p.stack.map(s => <span key={s} className="chip">{s}</span>)}
                </div>
              </div>
              <div className="card" style={{ padding: 14 }}>
                <div className="caption" style={{ marginBottom: 10 }}>[Team · {p.members}]</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {MOCK.users.slice(0, 4).map(u => (
                    <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <Avatar name={u.name} size={28} />
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontSize: 12, fontWeight: 500 }}>{u.name}</div>
                        <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>{u.title}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
              <div className="card" style={{ padding: 14 }}>
                <div className="caption" style={{ marginBottom: 10 }}>[Lifecycle]</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  {['idea','recruiting','active','beta','release'].map(s => (
                    <div key={s} style={{
                      display: 'flex', alignItems: 'center', gap: 8,
                      padding: '6px 8px',
                      background: s === p.stage ? 'var(--accent-weak)' : 'transparent',
                      borderRadius: 6, fontSize: 12,
                      color: s === p.stage ? 'var(--accent)' : 'var(--text-muted)',
                      fontWeight: s === p.stage ? 500 : 400,
                    }}>
                      <div style={{
                        width: 8, height: 8, borderRadius: 999,
                        background: s === p.stage ? 'var(--accent)' : 'var(--border-strong)',
                      }} />
                      <span style={{ textTransform: 'capitalize' }}>{s}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}
        {tab === 'chat' && <ProjectChat project={p} />}
        {tab === 'team' && (
          <div style={{ marginTop: 24, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 12 }}>
            {MOCK.users.map(u => (
              <div key={u.id} className="card" style={{ padding: 16 }}>
                <div style={{ display: 'flex', gap: 12, marginBottom: 10 }}>
                  <Avatar name={u.name} size={44} />
                  <div>
                    <div style={{ fontSize: 14, fontWeight: 500 }}>{u.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{u.title}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                  {u.skills.map(s => <span key={s} className="chip" style={{ fontSize: 10 }}>{s}</span>)}
                </div>
              </div>
            ))}
          </div>
        )}
        {tab === 'activity' && (
          <div style={{ marginTop: 24 }}>
            <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>Activity timeline · GitHub sync active · last synced 2m ago</div>
            <div className="card" style={{ marginTop: 12, padding: 18, display: 'grid', gridTemplateColumns: 'repeat(52, 1fr)', gap: 3 }}>
              {Array.from({ length: 52 * 7 }, (_, i) => {
                const intensity = Math.random();
                return <div key={i} style={{
                  aspectRatio: 1, borderRadius: 2,
                  background: intensity < 0.4 ? 'var(--bg-subtle)' : intensity < 0.6 ? 'color-mix(in oklch, var(--accent) 25%, var(--bg-subtle))' : intensity < 0.85 ? 'color-mix(in oklch, var(--accent) 55%, var(--bg-subtle))' : 'var(--accent)',
                }} />;
              })}
            </div>
          </div>
        )}
        {tab === 'showcase' && (
          <div style={{ marginTop: 24, maxWidth: 780 }}>
            <div className="placeholder-stripe" style={{ aspectRatio: '16/9', borderRadius: 'var(--r-lg)', marginBottom: 16 }} />
            <h2 className="serif" style={{ fontSize: 32, letterSpacing: -0.5, margin: '16px 0 8px', fontWeight: 400 }}>{p.name} — {p.tagline}</h2>
            <p style={{ fontSize: 15, lineHeight: 1.6, color: 'var(--text-muted)' }}>{p.description}</p>
            <div style={{ display: 'flex', gap: 18, marginTop: 16, fontSize: 13, color: 'var(--text-muted)' }}>
              <span><Icon name="heart" size={13} /> {p.stars} likes</span>
              <span><Icon name="comment" size={13} /> 42 comments</span>
              <span><Icon name="eye" size={13} /> 2.1k views</span>
            </div>
          </div>
        )}
        {tab === 'metrics' && (
          <div style={{ marginTop: 24, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12 }}>
            {[
              ['Tasks completed', '47', '+12 this week'],
              ['Velocity', '3.2/wk', '+0.4'],
              ['Team contributions', '86%', 'active'],
              ['Est. vs actual', '1.08x', 'on track'],
            ].map(([l, v, d]) => (
              <div key={l} className="card" style={{ padding: 16 }}>
                <div className="caption" style={{ marginBottom: 8 }}>{l}</div>
                <div style={{ fontSize: 28, fontWeight: 600, letterSpacing: -0.5 }}>{v}</div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 4 }}>{d}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Kanban Board ─────────────────────────────────────────────────
function KanbanBoard({ setRoute, projectId = 'p1' }) {
  const project = MOCK.projects.find(p => p.id === projectId) || MOCK.projects[0];
  const [tasks, setTasks] = React.useState(MOCK.tasks);
  const [dragId, setDragId] = React.useState(null);
  const [dragOverCol, setDragOverCol] = React.useState(null);
  const [editing, setEditing] = React.useState(null);

  const onDragStart = (id) => setDragId(id);
  const onDragEnd = () => { setDragId(null); setDragOverCol(null); };
  const onDropTo = (colId) => {
    if (dragId) setTasks(ts => ts.map(t => t.id === dragId ? { ...t, col: colId } : t));
    onDragEnd();
  };

  const addTask = (colId) => {
    const t = { id: 'new' + Date.now(), col: colId, title: 'New task', priority: 'medium', assignee: MOCK.me.name, tags: [] };
    setTasks(ts => [...ts, t]);
    setEditing(t.id);
  };

  return (
    <div style={{ padding: 'calc(20px * var(--d)) calc(28px * var(--d))', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <div className="caption" style={{ marginBottom: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
            <button onClick={() => setRoute && setRoute('projects/' + projectId)}
              style={{ background: 'transparent', border: 'none', padding: 0, color: 'var(--text-muted)', fontFamily: 'inherit', fontSize: 'inherit', letterSpacing: 'inherit', textTransform: 'inherit', cursor: 'pointer' }}
              onMouseEnter={e => e.currentTarget.style.color = 'var(--accent)'}
              onMouseLeave={e => e.currentTarget.style.color = 'var(--text-muted)'}>
              ← [{project.name}]
            </button>
            <span style={{ opacity: 0.5 }}>/</span>
            <span>[Board]</span>
          </div>
          <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, letterSpacing: -0.3 }}>Task board</h1>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <div style={{ display: 'flex', marginRight: 8 }}>
            {MOCK.users.slice(0,4).map((u, i) => (
              <div key={u.id} style={{ marginLeft: i === 0 ? 0 : -8 }}>
                <Avatar name={u.name} size={26} />
              </div>
            ))}
          </div>
          <button className="btn btn-sm"><Icon name="filter" size={12} /> Filter</button>
          <button className="btn btn-primary btn-sm" onClick={() => addTask('todo')}><Icon name="plus" size={12} /> Task</button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: `repeat(${MOCK.columns.length}, minmax(270px, 1fr))`, gap: 14, flex: 1, overflowX: 'auto', paddingBottom: 20, alignItems: 'start' }}>
        {MOCK.columns.map(col => {
          const colTasks = tasks.filter(t => t.col === col.id);
          const overLimit = col.limit && colTasks.length > col.limit;
          return (
            <div key={col.id}
              onDragOver={e => { e.preventDefault(); setDragOverCol(col.id); }}
              onDragLeave={() => setDragOverCol(c => c === col.id ? null : c)}
              onDrop={() => onDropTo(col.id)}
              style={{
                background: dragOverCol === col.id ? 'var(--accent-weak)' : 'var(--bg-subtle)',
                borderRadius: 'var(--r-lg)', padding: 10,
                transition: 'background 0.12s',
                display: 'flex', flexDirection: 'column', minHeight: 120,
              }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '4px 6px 10px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontSize: 13, fontWeight: 600 }}>{col.name}</span>
                  <span className="mono" style={{
                    fontSize: 10, padding: '1px 6px',
                    background: overLimit ? 'color-mix(in oklch, var(--danger) 20%, transparent)' : 'var(--bg-elev)',
                    color: overLimit ? 'var(--danger)' : 'var(--text-muted)',
                    borderRadius: 999,
                  }}>{colTasks.length}{col.limit && `/${col.limit}`}</span>
                </div>
                <button onClick={() => addTask(col.id)} className="btn-ghost" style={{ padding: 4, border: 'none', background: 'transparent' }}>
                  <Icon name="plus" size={13} />
                </button>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {colTasks.map(t => (
                  <TaskCard key={t.id} task={t} dragging={dragId === t.id}
                    onDragStart={onDragStart} onDragEnd={onDragEnd}
                    editing={editing === t.id}
                    setEditing={setEditing}
                    onUpdate={updates => setTasks(ts => ts.map(x => x.id === t.id ? { ...x, ...updates } : x))}
                  />
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

const PRIO_STYLE = {
  urgent: { bg: 'color-mix(in oklch, var(--danger) 20%, transparent)', fg: 'var(--danger)' },
  high: { bg: 'color-mix(in oklch, var(--warning) 20%, transparent)', fg: 'var(--warning)' },
  medium: { bg: 'var(--bg-subtle)', fg: 'var(--text-muted)' },
  low: { bg: 'var(--bg-subtle)', fg: 'var(--text-subtle)' },
};

function TaskCard({ task, dragging, onDragStart, onDragEnd, editing, setEditing, onUpdate }) {
  const ref = React.useRef();
  const prio = PRIO_STYLE[task.priority];
  return (
    <div ref={ref} draggable={!editing}
      onDragStart={() => onDragStart(task.id)} onDragEnd={onDragEnd}
      className="card"
      style={{
        padding: 10, cursor: editing ? 'text' : 'grab',
        opacity: dragging ? 0.4 : 1, userSelect: 'none',
      }}>
      {editing ? (
        <input autoFocus value={task.title}
          onChange={e => onUpdate({ title: e.target.value })}
          onBlur={() => setEditing(null)}
          onKeyDown={e => e.key === 'Enter' && setEditing(null)}
          className="input" style={{ padding: 4, fontSize: 13, border: '1px solid var(--accent)' }} />
      ) : (
        <div onDoubleClick={() => setEditing(task.id)} style={{ fontSize: 13, fontWeight: 500, marginBottom: 8, lineHeight: 1.4 }}>
          {task.title}
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        <span className="chip" style={{ fontSize: 10, background: prio.bg, color: prio.fg, borderColor: 'transparent' }}>
          <span style={{ width: 5, height: 5, borderRadius: 999, background: prio.fg, display: 'inline-block' }} />
          {task.priority}
        </span>
        {task.tags.map(t => <span key={t} className="chip" style={{ fontSize: 10 }}>{t}</span>)}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8, borderTop: '1px solid var(--border)', paddingTop: 8 }}>
        <Avatar name={task.assignee} size={20} />
        <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>#{task.id}</span>
      </div>
    </div>
  );
}

Object.assign(window, { ProjectsList, ProjectDetail, KanbanBoard, ProjectChat });

function ProjectChat({ project }) {
  const [messages, setMessages] = React.useState([
    { id: 1, user: MOCK.users[0], text: 'PR #142 is ready for review — Redis rate limiter + tests included.', time: '10:24' },
    { id: 2, user: MOCK.users[1], text: 'Nice, pulling it now. I\'ll run the E2E suite locally.', time: '10:26' },
    { id: 3, user: MOCK.users[2], text: 'Reminder: design review Thursday 4pm. I pushed the new onboarding mocks to Figma.', time: '10:41', reactions: [{ emoji: '👀', count: 3 }, { emoji: '🔥', count: 2 }] },
    { id: 4, user: MOCK.users[0], text: 'Also — should we freeze the API contract before the launch? Getting ambiguous reports from external integrators.', time: '11:02' },
  ]);
  const [draft, setDraft] = React.useState('');
  const endRef = React.useRef();
  React.useEffect(() => { endRef.current?.parentElement?.scrollTo({ top: 99999, behavior: 'smooth' }); }, [messages]);

  const send = () => {
    if (!draft.trim()) return;
    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    setMessages(m => [...m, { id: Date.now(), user: MOCK.me, text: draft, time }]);
    setDraft('');
    setTimeout(() => {
      setMessages(m => [...m, { id: Date.now() + 1, user: MOCK.users[1], text: 'Good call — I\'ll draft a proposal.', time, typing: false }]);
    }, 1100);
  };

  return (
    <div style={{ marginTop: 16, display: 'grid', gridTemplateColumns: '1fr 260px', gap: 16, alignItems: 'start' }}>
      <div className="card" style={{ padding: 0, display: 'flex', flexDirection: 'column', height: 'calc(100vh - 260px)', minHeight: 440 }}>
        <div style={{ padding: '12px 18px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Icon name="hash" size={14} style={{ color: 'var(--text-muted)' }} />
            <span style={{ fontSize: 13, fontWeight: 500 }}>general</span>
            <span className="caption">· {MOCK.users.length + 1} members</span>
          </div>
          <div style={{ display: 'flex', gap: 4 }}>
            <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="search" size={13} /></button>
            <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="settings" size={13} /></button>
          </div>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', padding: '14px 18px', display: 'flex', flexDirection: 'column', gap: 14 }}>
          {messages.map(m => (
            <div key={m.id} style={{ display: 'flex', gap: 10 }}>
              <Avatar name={m.user.name} size={30} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', marginBottom: 2 }}>
                  <span style={{ fontSize: 12.5, fontWeight: 500 }}>{m.user.name}</span>
                  <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>{m.time}</span>
                </div>
                <div style={{ fontSize: 13, lineHeight: 1.5, color: 'var(--text)' }}>{m.text}</div>
                {m.reactions && (
                  <div style={{ display: 'flex', gap: 4, marginTop: 6 }}>
                    {m.reactions.map(r => (
                      <button key={r.emoji} className="chip" style={{ fontSize: 11, padding: '2px 8px', cursor: 'pointer' }}>
                        {r.emoji} <span className="mono" style={{ fontSize: 10, marginLeft: 3 }}>{r.count}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))}
          <div ref={endRef} />
        </div>
        <div style={{ padding: 12, borderTop: '1px solid var(--border)', display: 'flex', gap: 8 }}>
          <input className="input" placeholder={`Message #general · ${project.name}`}
            value={draft} onChange={e => setDraft(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && send()}
            style={{ flex: 1 }} />
          <button className="btn btn-primary btn-sm" onClick={send}><Icon name="arrowRight" size={13} /></button>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div className="card" style={{ padding: 12 }}>
          <div className="caption" style={{ marginBottom: 8 }}>[Channels]</div>
          {['general', 'engineering', 'design', 'launches', 'random'].map((c, i) => (
            <div key={c} style={{
              display: 'flex', alignItems: 'center', gap: 8,
              padding: '4px 6px', fontSize: 12.5,
              color: i === 0 ? 'var(--text)' : 'var(--text-muted)',
              background: i === 0 ? 'var(--bg-hover)' : 'transparent',
              borderRadius: 4, cursor: 'pointer',
            }}>
              <Icon name="hash" size={11} /> {c}
            </div>
          ))}
        </div>
        <div className="card" style={{ padding: 12 }}>
          <div className="caption" style={{ marginBottom: 8 }}>[Members · {MOCK.users.length + 1}]</div>
          {[MOCK.me, ...MOCK.users].slice(0, 5).map(u => (
            <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0', fontSize: 12 }}>
              <div style={{ position: 'relative' }}>
                <Avatar name={u.name} size={22} />
                <div style={{ position: 'absolute', bottom: -1, right: -1, width: 7, height: 7, borderRadius: 999, background: 'var(--success)', border: '1.5px solid var(--bg)' }} />
              </div>
              <span style={{ flex: 1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{u.name}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
