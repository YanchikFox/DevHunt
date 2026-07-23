// DevHunt — Landing + Auth screens

function Landing({ onEnter }) {
  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', overflowY: 'auto' }}>
      <header style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '20px 48px', borderBottom: '1px solid var(--border)', position: 'sticky', top: 0, background: 'var(--bg)', zIndex: 10,
      }}>
        <Logo />
        <nav style={{ display: 'flex', gap: 24, fontSize: 13, color: 'var(--text-muted)' }} className="hide-mobile">
          <a>Product</a><a>AI</a><a>Showcase</a><a>Pricing</a><a>Docs</a>
        </nav>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-sm" onClick={() => onEnter('auth')}>Log in</button>
          <button className="btn btn-primary btn-sm" onClick={() => onEnter('auth')}>Start for free</button>
        </div>
      </header>

      {/* Hero */}
      <section style={{ padding: '80px 48px 60px', maxWidth: 1200, margin: '0 auto' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 24 }}>
          <span className="chip accent"><Icon name="sparkle" size={10} /> v1.0 — AI team matching now live</span>
        </div>
        <h1 className="serif" style={{
          fontSize: 'clamp(44px, 7vw, 92px)', lineHeight: 0.98, letterSpacing: -2,
          margin: 0, fontWeight: 400,
        }}>
          From idea to release,<br/>
          <em style={{ color: 'var(--accent)' }}>without leaving the room.</em>
        </h1>
        <p style={{ fontSize: 18, color: 'var(--text-muted)', maxWidth: 620, lineHeight: 1.5, marginTop: 28 }}>
          DevHunt is where developers ship side projects together. Plan with AI, find teammates who match your stack, track work on a built-in board, and launch to a community that actually reads your changelog.
        </p>
        <div style={{ display: 'flex', gap: 12, marginTop: 32, flexWrap: 'wrap' }}>
          <button className="btn btn-primary btn-lg" onClick={() => onEnter('auth')}>Start building <Icon name="arrowRight" size={14} /></button>
          <button className="btn btn-lg" onClick={() => onEnter('app')}><Icon name="github" size={14} /> Continue with GitHub</button>
          <button className="btn btn-ghost btn-lg" onClick={() => onEnter('app')}>Enter demo →</button>
        </div>
        <div style={{ display: 'flex', gap: 24, marginTop: 48, flexWrap: 'wrap', fontSize: 12, color: 'var(--text-muted)' }}>
          <span>◆ 14,200 builders</span>
          <span>◆ 3,400 projects shipped</span>
          <span>◆ 220 hackathons hosted</span>
        </div>
      </section>

      {/* Product tile */}
      <section style={{ padding: '0 48px 100px', maxWidth: 1200, margin: '0 auto' }}>
        <div className="card" style={{ padding: 6, overflow: 'hidden' }}>
          <div className="placeholder-stripe" style={{
            aspectRatio: '16/9', borderRadius: 'var(--r-md)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            position: 'relative',
          }}>
            <div style={{ display: 'grid', gridTemplateColumns: '200px 1fr 1fr 1fr', gap: 10, padding: 20, width: '94%', height: '94%',
              background: 'var(--bg)', borderRadius: 'var(--r-md)', border: '1px solid var(--border)',
            }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4, padding: 8 }}>
                <Logo size={14} />
                <div style={{ height: 1, background: 'var(--border)', margin: '8px 0' }} />
                {['Home','Projects','Board','AI Plan'].map(x => (
                  <div key={x} style={{ fontSize: 11, padding: 4, color: 'var(--text-muted)' }}>{x}</div>
                ))}
              </div>
              {MOCK.columns.slice(0,3).map(c => (
                <div key={c.id}>
                  <div className="caption" style={{ marginBottom: 8 }}>{c.name}</div>
                  {MOCK.tasks.filter(t => t.col === c.id).slice(0,3).map(t => (
                    <div key={t.id} className="card" style={{ padding: 8, marginBottom: 6, fontSize: 10 }}>
                      <div style={{ fontWeight: 500 }}>{t.title.slice(0, 30)}…</div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Feature grid */}
      <section style={{ padding: '0 48px 100px', maxWidth: 1200, margin: '0 auto' }}>
        <div className="caption" style={{ marginBottom: 16 }}>[What's inside]</div>
        <h2 className="serif" style={{ fontSize: 48, margin: '0 0 48px', letterSpacing: -1, fontWeight: 400 }}>
          Six surfaces. One workflow.
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 1, background: 'var(--border)', border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', overflow: 'hidden' }}>
          {[
            ['AI Plan', 'sparkle', 'Describe a project in a paragraph. Get a staged roadmap, tech stack recommendation, and a list of roles you need to fill.'],
            ['Smart matching', 'users', 'The ML service pairs your open roles with developers whose GitHub activity and stated skills fit — reverse-matching too.'],
            ['Built-in Kanban', 'board', 'No more leaving for Trello. Soft-deletes, task links, WIP limits, drag-reorder. Works offline.'],
            ['Realtime chat', 'chat', 'Per-project channels and DMs over SignalR. Typing indicators, threads, file drops.'],
            ['Project lifecycle', 'flag', 'Idea → Recruiting → Active → Beta → Release → Archived. Each stage changes what shows up in the feed.'],
            ['GitHub sync', 'github', 'Link a repo. Commits light up the activity wall. Issues sync both directions with your board.'],
          ].map(([title, icon, body]) => (
            <div key={title} style={{ background: 'var(--bg-elev)', padding: 28 }}>
              <div style={{ display: 'inline-flex', padding: 8, background: 'var(--accent-weak)', color: 'var(--accent)', borderRadius: 8, marginBottom: 16 }}>
                <Icon name={icon} size={16} />
              </div>
              <div style={{ fontSize: 17, fontWeight: 600, marginBottom: 6 }}>{title}</div>
              <div style={{ fontSize: 13, color: 'var(--text-muted)', lineHeight: 1.5 }}>{body}</div>
            </div>
          ))}
        </div>
      </section>

      {/* AI band */}
      <section style={{ padding: '0 48px 100px', maxWidth: 1200, margin: '0 auto' }}>
        <div className="ai-border" style={{ padding: 48, background: 'var(--bg-subtle)' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 48, alignItems: 'center' }}>
            <div>
              <div className="chip accent" style={{ marginBottom: 16 }}><Icon name="sparkle" size={10} /> AI that actually moves the cursor</div>
              <h3 className="serif" style={{ fontSize: 42, letterSpacing: -1, fontWeight: 400, margin: '0 0 16px' }}>
                A cofounder that never sleeps.
              </h3>
              <p style={{ color: 'var(--text-muted)', fontSize: 15, lineHeight: 1.6, margin: 0 }}>
                DevHunt's AI Plan turns "I want to build X" into a realistic 6-week roadmap, a tech stack your team will actually enjoy, and a shortlist of collaborators whose work matches the task.
              </p>
              <div style={{ display: 'flex', gap: 8, marginTop: 24 }}>
                <button className="btn btn-primary" onClick={() => onEnter('app')}>Try AI Plan <Icon name="arrowRight" size={13} /></button>
              </div>
            </div>
            <div className="card" style={{ padding: 20, background: 'var(--bg-elev)' }}>
              <div className="mono" style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 12 }}>/ai/plan/generate</div>
              <div style={{ padding: 12, background: 'var(--bg-subtle)', borderRadius: 8, fontSize: 13, marginBottom: 12 }}>
                "Real-time collab whiteboard focused on system-design interviews."
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {[
                  ['Week 1-2', 'CRDT sync layer · React canvas scaffold'],
                  ['Week 3-4', 'Voice + cursor presence · auth'],
                  ['Week 5-6', 'Template library · billing'],
                ].map(([w, b]) => (
                  <div key={w} style={{ display: 'flex', gap: 12, padding: '8px 10px', background: 'var(--bg)', borderRadius: 8, border: '1px solid var(--border)' }}>
                    <span className="mono" style={{ fontSize: 11, color: 'var(--accent)', minWidth: 60 }}>{w}</span>
                    <span style={{ fontSize: 12 }}>{b}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </section>

      <footer style={{ padding: '40px 48px', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, color: 'var(--text-muted)', fontSize: 12 }}>
        <div style={{ display: 'flex', gap: 24, alignItems: 'center' }}><Logo size={14} /><span>© 2026 DevHunt</span></div>
        <div style={{ display: 'flex', gap: 16 }}><a>Privacy</a><a>Terms</a><a>Status</a><a>Changelog</a></div>
      </footer>
    </div>
  );
}

function AuthScreen({ onEnter }) {
  const [mode, setMode] = React.useState('login');
  return (
    <div style={{ minHeight: '100vh', display: 'grid', gridTemplateColumns: '1fr 1fr' }}>
      <div style={{
        background: 'var(--bg-subtle)', padding: 48,
        display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
        borderRight: '1px solid var(--border)',
      }} className="hide-mobile">
        <Logo />
        <div>
          <div className="caption" style={{ marginBottom: 12 }}>[What you're joining]</div>
          <div className="serif" style={{ fontSize: 36, letterSpacing: -0.8, lineHeight: 1.1 }}>
            A quiet place to build <em style={{ color: 'var(--accent)' }}>loud</em> things.
          </div>
          <div style={{ marginTop: 32, display: 'flex', flexDirection: 'column', gap: 10 }}>
            {['AI-matched collaborators','Integrated Kanban & chat','GitHub sync, zero config','Public showcase on release'].map(x => (
              <div key={x} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13, color: 'var(--text-muted)' }}>
                <Icon name="check" size={14} style={{ color: 'var(--accent)' }} />{x}
              </div>
            ))}
          </div>
        </div>
        <div style={{ fontSize: 11, color: 'var(--text-subtle)' }} className="mono">EST. 2025 · BERLIN · WARSAW</div>
      </div>
      <div style={{ padding: 48, display: 'flex', flexDirection: 'column', justifyContent: 'center', maxWidth: 480, margin: '0 auto', width: '100%' }}>
        <div style={{ display: 'flex', gap: 4, marginBottom: 24, background: 'var(--bg-subtle)', padding: 3, borderRadius: 'var(--r-md)', width: 'fit-content' }}>
          {['login','register'].map(m => (
            <button key={m} onClick={() => setMode(m)} style={{
              padding: '6px 14px', borderRadius: 6, border: 'none',
              background: mode === m ? 'var(--bg-elev)' : 'transparent',
              boxShadow: mode === m ? 'var(--shadow-sm)' : 'none',
              fontSize: 12, fontWeight: 500, textTransform: 'capitalize',
            }}>{m === 'login' ? 'Log in' : 'Sign up'}</button>
          ))}
        </div>
        <h2 className="serif" style={{ fontSize: 36, letterSpacing: -0.6, margin: '0 0 8px', fontWeight: 400 }}>
          {mode === 'login' ? 'Welcome back.' : 'Create your account.'}
        </h2>
        <p style={{ color: 'var(--text-muted)', fontSize: 14, marginTop: 0, marginBottom: 28 }}>
          {mode === 'login' ? 'Sign in to continue shipping.' : 'Start your first project in under a minute.'}
        </p>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <button className="btn btn-lg" style={{ justifyContent: 'center' }} onClick={() => onEnter('app')}>
            <Icon name="github" size={15} /> Continue with GitHub
          </button>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--text-subtle)', fontSize: 11, margin: '8px 0' }}>
            <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />OR<div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
          </div>
          {mode === 'register' && (
            <div>
              <div className="caption" style={{ marginBottom: 4 }}>Name</div>
              <input className="input" placeholder="Alex Kurenkov" />
            </div>
          )}
          <div>
            <div className="caption" style={{ marginBottom: 4 }}>Email</div>
            <input className="input" placeholder="you@work.com" defaultValue={mode === 'login' ? 'alex@devhunt.io' : ''} />
          </div>
          <div>
            <div className="caption" style={{ marginBottom: 4 }}>Password</div>
            <input className="input" type="password" defaultValue="••••••••••" />
          </div>
          <button className="btn btn-primary btn-lg" style={{ justifyContent: 'center', marginTop: 8 }} onClick={() => onEnter('app')}>
            {mode === 'login' ? 'Log in' : 'Create account'} <Icon name="arrowRight" size={14} />
          </button>
        </div>
        <div style={{ marginTop: 24, fontSize: 12, color: 'var(--text-muted)', textAlign: 'center' }}>
          {mode === 'login'
            ? <>No account yet? <a style={{ color: 'var(--accent)', cursor: 'pointer' }} onClick={() => setMode('register')}>Sign up</a></>
            : <>By continuing you accept our Terms and Privacy Policy.</>}
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { Landing, AuthScreen });
