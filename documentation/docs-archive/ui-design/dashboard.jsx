// DevHunt — Dashboard Home, Notifications, Community Feed

function HomeDashboard({ setRoute }) {
  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 1280, margin: '0 auto' }}>
      <div style={{ marginBottom: 28 }}>
        <div className="caption" style={{ marginBottom: 6 }}>[Tuesday · Apr 19, 2026]</div>
        <h1 className="serif" style={{ fontSize: 44, margin: 0, letterSpacing: -0.8, fontWeight: 400 }}>
          Good afternoon, Alex.
        </h1>
        <p style={{ color: 'var(--text-muted)', fontSize: 14, margin: '6px 0 0' }}>
          2 projects active · 6 tasks assigned · 3 unread messages
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 20 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>

          {/* AI quick action */}
          <div className="ai-border" style={{ padding: 20 }}>
            <div className="ai-glow" style={{ padding: 20, borderRadius: 'var(--r-lg)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
                <Icon name="sparkle" size={14} style={{ color: 'var(--accent)' }} />
                <span className="caption">[AI ASSISTANT]</span>
              </div>
              <div style={{ fontSize: 18, fontWeight: 500, marginBottom: 4 }}>
                3 developers match your open Rust role on Helix
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 14 }}>
                Ranked by GitHub activity + tech stack overlap. Tomás already joined — these look like good fits next.
              </div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {MOCK.users.slice(1, 4).map(u => (
                  <div key={u.id} className="card" style={{ padding: 10, display: 'flex', alignItems: 'center', gap: 10, background: 'var(--bg-elev)' }}>
                    <Avatar name={u.name} size={32} />
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontSize: 12, fontWeight: 500 }}>{u.name}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{u.title}</div>
                    </div>
                    <button className="btn btn-sm" style={{ marginLeft: 8 }}>Invite</button>
                  </div>
                ))}
              </div>
              <div style={{ marginTop: 14 }}>
                <button className="btn-ghost" onClick={() => setRoute('ai')} style={{ background: 'transparent', border: 'none', color: 'var(--accent)', fontSize: 12, padding: 0, cursor: 'pointer' }}>
                  See all matches →
                </button>
              </div>
            </div>
          </div>

          {/* My projects */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <span className="caption">[Your projects]</span>
              <button className="btn-ghost" onClick={() => setRoute('projects')} style={{ fontSize: 11, background: 'transparent', border: 'none', color: 'var(--text-muted)' }}>View all →</button>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
              {MOCK.projects.slice(0, 3).map(p => (
                <ProjectCard key={p.id} project={p} onClick={() => setRoute('projects/' + p.id)} />
              ))}
            </div>
          </div>

          {/* Recent activity / feed */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <span className="caption">[Community feed]</span>
              <button className="btn-ghost" onClick={() => setRoute('community')} style={{ fontSize: 11, background: 'transparent', border: 'none', color: 'var(--text-muted)' }}>Open →</button>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {MOCK.feed.slice(0, 2).map(p => <FeedPost key={p.id} post={p} />)}
            </div>
          </div>
        </div>

        {/* Right rail */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="card" style={{ padding: 16 }}>
            <div className="caption" style={{ marginBottom: 10 }}>[Your week]</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <Stat label="Tasks completed" value="14" delta="+3" />
              <Stat label="PRs merged" value="7" delta="+2" />
              <Stat label="Team invites" value="2" delta="+2" />
              <Stat label="Reviews received" value="5" delta="+1" />
            </div>
          </div>

          <div className="card" style={{ padding: 16 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
              <span className="caption">[Notifications]</span>
              <span className="chip accent" style={{ fontSize: 10, padding: '2px 6px' }}>3 new</span>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {MOCK.notifications.slice(0, 4).map(n => (
                <div key={n.id} style={{ display: 'flex', gap: 10, padding: '10px 0', borderBottom: '1px solid var(--border)' }}>
                  {n.unread && <div style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--accent)', marginTop: 7, flexShrink: 0 }} />}
                  {!n.unread && <div style={{ width: 6 }} />}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 12, lineHeight: 1.4 }}>{n.text}</div>
                    <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4 }}>{n.time} · {n.kind}</div>
                  </div>
                </div>
              ))}
            </div>
            <button className="btn-ghost" onClick={() => setRoute('notifications')} style={{ marginTop: 8, width: '100%', justifyContent: 'center', fontSize: 12 }}>View all notifications</button>
          </div>

          <div className="card" style={{ padding: 16 }}>
            <div className="caption" style={{ marginBottom: 10 }}>[Upcoming]</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {[
                ['Helix v0.5 release', 'Fri Apr 22'],
                ['Paperstack design sync', 'Wed Apr 20, 2pm'],
                ['Berlin Hack Night', 'Sat Apr 23'],
              ].map(([t, d]) => (
                <div key={t} style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                  <Icon name="calendar" size={13} style={{ color: 'var(--text-muted)', marginTop: 2 }} />
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontSize: 12, fontWeight: 500 }}>{t}</div>
                    <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>{d}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

const Stat = ({ label, value, delta }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
    <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{label}</span>
    <span style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
      <span style={{ fontSize: 16, fontWeight: 600 }}>{value}</span>
      <span className="mono" style={{ fontSize: 10, color: 'var(--success)' }}>{delta}</span>
    </span>
  </div>
);

const STAGE_STYLE = {
  idea: { bg: 'var(--accent-weak)', fg: 'var(--accent)' },
  recruiting: { bg: 'color-mix(in oklch, var(--warning) 15%, transparent)', fg: 'var(--warning)' },
  active: { bg: 'color-mix(in oklch, var(--success) 15%, transparent)', fg: 'var(--success)' },
  beta: { bg: 'var(--accent-weak)', fg: 'var(--accent)' },
  release: { bg: 'var(--accent-weak)', fg: 'var(--accent)' },
  completed: { bg: 'var(--bg-subtle)', fg: 'var(--text-muted)' },
};

function ProjectCard({ project: p, onClick, cardStyle = 'outline' }) {
  const stageStyle = STAGE_STYLE[p.stage] || STAGE_STYLE.active;
  return (
    <div className="card" onClick={onClick} style={{
      padding: 16, cursor: 'pointer', transition: 'transform 0.12s, box-shadow 0.12s',
      background: cardStyle === 'filled' ? 'var(--bg-subtle)' : cardStyle === 'ghost' ? 'transparent' : 'var(--bg-elev)',
      border: cardStyle === 'ghost' ? '1px dashed var(--border)' : '1px solid var(--border)',
    }}
      onMouseEnter={e => { e.currentTarget.style.transform = 'translateY(-2px)'; e.currentTarget.style.boxShadow = 'var(--shadow-md)'; }}
      onMouseLeave={e => { e.currentTarget.style.transform = ''; e.currentTarget.style.boxShadow = ''; }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
        <div style={{
          width: 34, height: 34, borderRadius: 8,
          background: 'var(--accent-weak)', color: 'var(--accent)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 14,
        }}>{p.name[0]}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 14, fontWeight: 600 }}>{p.name}</div>
          <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>/{p.tag}</div>
        </div>
        <span className="chip" style={{ background: stageStyle.bg, color: stageStyle.fg, borderColor: 'transparent' }}>{p.stage}</span>
      </div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.4, marginBottom: 12, minHeight: 32 }}>{p.tagline}</div>
      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginBottom: 12 }}>
        {p.stack.slice(0, 3).map(s => <span key={s} className="chip" style={{ fontSize: 10 }}>{s}</span>)}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11, color: 'var(--text-muted)', borderTop: '1px solid var(--border)', paddingTop: 10 }}>
        <span><Icon name="users" size={11} style={{ verticalAlign: -1 }} /> {p.members}</span>
        <span><Icon name="zap" size={11} style={{ verticalAlign: -1 }} /> {p.stars}</span>
        {p.openRoles > 0 && <span style={{ color: 'var(--accent)', fontWeight: 500 }}>{p.openRoles} open</span>}
        <span className="mono">{p.updated}</span>
      </div>
    </div>
  );
}

function FeedPost({ post }) {
  const [liked, setLiked] = React.useState(false);
  const [likes, setLikes] = React.useState(post.likes);
  return (
    <div className="card" style={{ padding: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
        <Avatar name={post.author} size={32} />
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 500 }}>
            {post.author} {post.project && <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>in <span style={{ color: 'var(--accent)' }}>{post.project}</span></span>}
          </div>
          <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>{post.time} ago · #{post.tag}</div>
        </div>
        <button className="btn-ghost" style={{ padding: 4, border: 'none', background: 'transparent' }}><Icon name="moreH" size={14} /></button>
      </div>
      <div style={{ fontSize: 13, lineHeight: 1.55, marginBottom: 12 }}>{post.text}</div>
      <div style={{ display: 'flex', gap: 16, fontSize: 12, color: 'var(--text-muted)', borderTop: '1px solid var(--border)', paddingTop: 10 }}>
        <button onClick={() => { setLiked(!liked); setLikes(l => liked ? l - 1 : l + 1); }}
          style={{ background: 'transparent', border: 'none', color: liked ? 'var(--danger)' : 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: 5, padding: 0, fontSize: 12 }}>
          <Icon name="heart" size={13} style={{ fill: liked ? 'currentColor' : 'none' }} />{likes}
        </button>
        <button style={{ background: 'transparent', border: 'none', color: 'inherit', display: 'flex', alignItems: 'center', gap: 5, padding: 0, fontSize: 12 }}>
          <Icon name="comment" size={13} />{post.comments}
        </button>
        <button style={{ background: 'transparent', border: 'none', color: 'inherit', display: 'flex', alignItems: 'center', gap: 5, padding: 0, fontSize: 12 }}>
          <Icon name="arrowUpRight" size={13} />Share
        </button>
      </div>
    </div>
  );
}

function Notifications() {
  const [items, setItems] = React.useState(MOCK.notifications);
  const [filter, setFilter] = React.useState('all');
  const visible = items.filter(n => filter === 'all' || (filter === 'unread' && n.unread));
  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 760, margin: '0 auto' }}>
      <div style={{ marginBottom: 20 }}>
        <div className="caption" style={{ marginBottom: 6 }}>[Inbox]</div>
        <h1 className="serif" style={{ fontSize: 36, margin: 0, letterSpacing: -0.6, fontWeight: 400 }}>Notifications</h1>
      </div>
      <div style={{ display: 'flex', gap: 4, marginBottom: 16, borderBottom: '1px solid var(--border)' }}>
        {['all', 'unread', 'mentions', 'invites'].map(f => (
          <button key={f} onClick={() => setFilter(f)} style={{
            padding: '8px 14px', border: 'none', background: 'transparent',
            borderBottom: `2px solid ${filter === f ? 'var(--accent)' : 'transparent'}`,
            color: filter === f ? 'var(--text)' : 'var(--text-muted)',
            fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
          }}>{f}</button>
        ))}
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 10 }}>
        <button className="btn btn-sm" onClick={() => setItems(items.map(i => ({ ...i, unread: false })))}>
          <Icon name="check" size={12} /> Mark all read
        </button>
      </div>
      <div className="card" style={{ overflow: 'hidden' }}>
        {visible.map((n, i) => (
          <div key={n.id} onClick={() => setItems(xs => xs.map(x => x.id === n.id ? { ...x, unread: false } : x))}
            style={{
              display: 'flex', gap: 14, padding: '16px 18px',
              borderBottom: i < visible.length - 1 ? '1px solid var(--border)' : 'none',
              background: n.unread ? 'var(--bg-subtle)' : 'transparent',
              cursor: 'pointer',
            }}>
            <div style={{
              width: 34, height: 34, borderRadius: 8,
              background: 'var(--accent-weak)', color: 'var(--accent)',
              display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
            }}>
              <Icon name={{ invite: 'users', mention: 'chat', review: 'star', badge: 'trophy', message: 'chat', match: 'sparkle' }[n.kind]} size={15} />
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 13, fontWeight: n.unread ? 500 : 400 }}>{n.text}</div>
              <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4 }}>{n.time} ago · {n.kind}</div>
            </div>
            {n.unread && <div style={{ width: 8, height: 8, borderRadius: 999, background: 'var(--accent)', marginTop: 10 }} />}
          </div>
        ))}
      </div>
    </div>
  );
}

function Community({ cardStyle }) {
  const [posts, setPosts] = React.useState(MOCK.feed);
  const [draft, setDraft] = React.useState('');
  const [tab, setTab] = React.useState('feed');
  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 980, margin: '0 auto' }}>
      <div style={{ marginBottom: 20 }}>
        <div className="caption" style={{ marginBottom: 6 }}>[Community]</div>
        <h1 className="serif" style={{ fontSize: 36, margin: 0, letterSpacing: -0.6, fontWeight: 400 }}>What's shipping.</h1>
      </div>
      <div style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '1px solid var(--border)' }}>
        {['feed','showcase','questions','releases'].map(t => (
          <button key={t} onClick={() => setTab(t)} style={{
            padding: '10px 16px', border: 'none', background: 'transparent',
            borderBottom: `2px solid ${tab === t ? 'var(--accent)' : 'transparent'}`,
            color: tab === t ? 'var(--text)' : 'var(--text-muted)',
            fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
          }}>{t}</button>
        ))}
      </div>

      {tab === 'showcase' ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
          {MOCK.projects.map(p => <ShowcaseCard key={p.id} project={p} />)}
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 280px', gap: 20 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div className="card" style={{ padding: 14 }}>
              <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                <Avatar name={MOCK.me.name} size={32} />
                <textarea value={draft} onChange={e => setDraft(e.target.value)}
                  placeholder="Share an update, a question, a release…"
                  style={{
                    flex: 1, padding: 8, background: 'transparent', border: 'none',
                    outline: 'none', resize: 'none', fontFamily: 'inherit', fontSize: 13, minHeight: 48,
                  }} />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', gap: 4 }}>
                  <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="paperclip" size={14} /></button>
                  <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="folder" size={14} /></button>
                </div>
                <button className="btn btn-primary btn-sm" disabled={!draft.trim()}
                  onClick={() => {
                    if (!draft.trim()) return;
                    setPosts([{ id: 'new' + Date.now(), author: MOCK.me.name, text: draft, time: 'now', likes: 0, comments: 0, tag: 'update' }, ...posts]);
                    setDraft('');
                  }}>Post</button>
              </div>
            </div>
            {posts.map(p => <FeedPost key={p.id} post={p} />)}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="card" style={{ padding: 14 }}>
              <div className="caption" style={{ marginBottom: 10 }}>[Trending tags]</div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {['#rust','#ai','#release','#hiring','#postgres','#indie','#kanban','#design'].map(t => (
                  <span key={t} className="chip" style={{ cursor: 'pointer' }}>{t}</span>
                ))}
              </div>
            </div>
            <div className="card" style={{ padding: 14 }}>
              <div className="caption" style={{ marginBottom: 10 }}>[Suggested to follow]</div>
              {MOCK.users.slice(0, 3).map(u => (
                <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0' }}>
                  <Avatar name={u.name} size={30} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 12, fontWeight: 500 }}>{u.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{u.title}</div>
                  </div>
                  <button className="btn btn-sm">Follow</button>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function ShowcaseCard({ project: p }) {
  return (
    <div className="card" style={{ overflow: 'hidden', cursor: 'pointer' }}>
      <div className="placeholder-stripe" style={{ aspectRatio: '16/10', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', padding: '4px 8px', background: 'var(--bg)', borderRadius: 4 }}>
          ~ {p.name} cover ~
        </div>
      </div>
      <div style={{ padding: 14 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
          <div style={{ fontSize: 14, fontWeight: 600 }}>{p.name}</div>
          <span className="chip accent" style={{ fontSize: 10 }}>Featured</span>
        </div>
        <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 10, lineHeight: 1.4 }}>{p.tagline}</div>
        <div style={{ display: 'flex', gap: 12, fontSize: 11, color: 'var(--text-muted)' }}>
          <span><Icon name="heart" size={11} /> {p.stars}</span>
          <span><Icon name="comment" size={11} /> 42</span>
          <span><Icon name="eye" size={11} /> 2.1k</span>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { HomeDashboard, Notifications, Community, ProjectCard, FeedPost });
