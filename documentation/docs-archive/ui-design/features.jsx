// DevHunt — AI Plan, AI Assistant, Chat, Profile, Admin

function AIPlan() {
  const [step, setStep] = React.useState(0);
  const [idea, setIdea] = React.useState('A real-time collaborative whiteboard focused on system-design interviews, with voice presence and architecture templates.');
  const [generating, setGenerating] = React.useState(false);
  const [showChat, setShowChat] = React.useState(false);

  const generate = () => {
    setGenerating(true);
    setTimeout(() => { setGenerating(false); setStep(1); }, 1400);
  };

  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 1100, margin: '0 auto' }}>
      <div style={{ marginBottom: 24, display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <div className="caption" style={{ marginBottom: 6 }}>[AI · Plan]</div>
          <h1 className="serif" style={{ fontSize: 40, margin: 0, letterSpacing: -0.8, fontWeight: 400 }}>
            Describe it. We'll plan it.
          </h1>
          <p style={{ color: 'var(--text-muted)', fontSize: 14, margin: '6px 0 0', maxWidth: 600 }}>
            Sketch your idea in natural language. AI will draft a tech stack, roadmap, and roles — then help you build the team.
          </p>
        </div>
        <button className="btn" onClick={() => setShowChat(v => !v)}>
          <Icon name="chat" size={13} /> {showChat ? 'Hide' : 'Open'} AI assistant
        </button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: showChat ? '1fr 360px' : '1fr', gap: 16 }}>
        <div>
          <div className="ai-border" style={{ marginBottom: 20 }}>
            <div className="ai-glow" style={{ padding: 20, borderRadius: 'var(--r-lg)' }}>
              <div className="caption" style={{ marginBottom: 8, color: 'var(--accent)' }}>[PROMPT]</div>
              <textarea value={idea} onChange={e => setIdea(e.target.value)}
                style={{
                  width: '100%', background: 'transparent', border: 'none', outline: 'none',
                  fontFamily: 'inherit', fontSize: 16, color: 'var(--text)',
                  minHeight: 70, resize: 'none', lineHeight: 1.5,
                }} />
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12, paddingTop: 12, borderTop: '1px solid var(--border)' }}>
                <div className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                  {idea.length} chars · model: haiku-4.5
                </div>
                <button className="btn btn-primary" onClick={generate} disabled={generating}>
                  {generating ? <>Generating…<span className="pulse-dot">●</span></> : <>Generate plan <Icon name="sparkle" size={13} /></>}
                </button>
              </div>
            </div>
          </div>

          {step >= 1 && (
            <div className="fade-in" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[Recommended stack]</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 8 }}>
                  {[
                    ['Frontend', 'React + Tiptap'],
                    ['CRDT', 'Yjs'],
                    ['Realtime', 'WebRTC + Liveblocks'],
                    ['Backend', 'Node.js + Postgres'],
                    ['Deploy', 'Vercel + Fly.io'],
                  ].map(([k, v]) => (
                    <div key={k} className="card" style={{ padding: 12 }}>
                      <div className="caption" style={{ marginBottom: 4 }}>{k}</div>
                      <div style={{ fontSize: 13, fontWeight: 500 }}>{v}</div>
                    </div>
                  ))}
                </div>
              </section>

              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[Roadmap · 6 weeks]</div>
                <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
                  {[
                    ['01', 'Week 1–2', 'Foundations', 'Canvas scaffold, CRDT sync layer, auth, project setup'],
                    ['02', 'Week 3–4', 'Collaboration', 'Voice presence, cursor trails, shape library, templates'],
                    ['03', 'Week 5', 'Polish', 'Export to PNG/PDF, keyboard shortcuts, performance pass'],
                    ['04', 'Week 6', 'Launch', 'Landing page, pricing, Product Hunt post, docs'],
                  ].map(([n, w, t, b], i) => (
                    <div key={n} style={{ display: 'grid', gridTemplateColumns: '40px 100px 160px 1fr', gap: 16, padding: '14px 18px', borderBottom: i < 3 ? '1px solid var(--border)' : 'none', alignItems: 'center' }}>
                      <span className="mono" style={{ fontSize: 11, color: 'var(--accent)' }}>{n}</span>
                      <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>{w}</span>
                      <span style={{ fontSize: 13, fontWeight: 500 }}>{t}</span>
                      <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{b}</span>
                    </div>
                  ))}
                </div>
              </section>

              <section>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
                  <div className="caption">[Roles you'll need]</div>
                  <button className="btn btn-sm">Add role</button>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 10 }}>
                  {[
                    ['Frontend · React', '~12 hrs/wk', 3],
                    ['Realtime / CRDT', '~8 hrs/wk', 2],
                    ['Product designer', '~6 hrs/wk', 4],
                  ].map(([r, h, matches]) => (
                    <div key={r} className="card" style={{ padding: 14 }}>
                      <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 4 }}>{r}</div>
                      <div className="mono" style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10 }}>{h}</div>
                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <span className="chip accent" style={{ fontSize: 10 }}><Icon name="sparkle" size={9} /> {matches} AI matches</span>
                        <button className="btn btn-sm">View</button>
                      </div>
                    </div>
                  ))}
                </div>
              </section>

              <section>
                <div className="caption" style={{ marginBottom: 10 }}>[AI-matched collaborators]</div>
                <div className="card" style={{ overflow: 'hidden' }}>
                  {MOCK.users.slice(0, 4).map((u, i) => (
                    <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '14px 18px', borderBottom: i < 3 ? '1px solid var(--border)' : 'none' }}>
                      <Avatar name={u.name} size={38} />
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span style={{ fontSize: 13, fontWeight: 500 }}>{u.name}</span>
                          <span className="chip accent" style={{ fontSize: 10 }}>{95 - i * 7}% match</span>
                        </div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{u.title} · {u.skills.join(' · ')}</div>
                      </div>
                      <button className="btn btn-sm">Message</button>
                      <button className="btn btn-primary btn-sm">Invite</button>
                    </div>
                  ))}
                </div>
              </section>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <button className="btn">Refine plan</button>
                <button className="btn btn-primary"><Icon name="plus" size={13} /> Create project from plan</button>
              </div>
            </div>
          )}
        </div>

        {showChat && <AIAssistantPanel />}
      </div>
    </div>
  );
}

function AIAssistantPanel() {
  const [msgs, setMsgs] = React.useState([
    { role: 'ai', text: "Hi Alex — I can help you refine your plan, suggest tradeoffs, or brainstorm features. What should we dig into?" },
    { role: 'me', text: 'Should I pick Yjs or Automerge for the CRDT layer?' },
    { role: 'ai', text: "For a whiteboard with high-frequency cursor + shape mutations, Yjs tends to feel snappier — its document model is optimised for rich-text/structured edits and has a great React ecosystem. Automerge is more principled but currently has higher memory overhead for long sessions. I'd start with Yjs and revisit if you hit hard wall." },
  ]);
  const [draft, setDraft] = React.useState('');
  const scrollRef = React.useRef();
  React.useEffect(() => { scrollRef.current?.scrollTo({ top: 99999 }); }, [msgs]);
  const send = () => {
    if (!draft.trim()) return;
    setMsgs(m => [...m, { role: 'me', text: draft }, { role: 'ai', text: 'Thinking…', pending: true }]);
    setDraft('');
    setTimeout(() => {
      setMsgs(m => m.slice(0, -1).concat({ role: 'ai', text: "Good question. For a 2-person team on a 6-week timeline, I'd skip auth complexity and use magic-link email via Resend — one migration, one endpoint. Upgrade to OAuth once you have traction." }));
    }, 1200);
  };
  return (
    <div className="card" style={{ padding: 0, display: 'flex', flexDirection: 'column', height: 'calc(100vh - 180px)', position: 'sticky', top: 20 }}>
      <div style={{ padding: '12px 14px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8 }}>
        <div style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--accent-weak)', color: 'var(--accent)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="sparkle" size={14} />
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 12, fontWeight: 600 }}>AI Assistant</div>
          <div className="mono" style={{ fontSize: 10, color: 'var(--success)' }}>● online</div>
        </div>
      </div>
      <div ref={scrollRef} style={{ flex: 1, overflowY: 'auto', padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
        {msgs.map((m, i) => (
          <div key={i} style={{
            alignSelf: m.role === 'me' ? 'flex-end' : 'flex-start',
            maxWidth: '88%',
            padding: '9px 12px', borderRadius: 12,
            background: m.role === 'me' ? 'var(--accent)' : 'var(--bg-subtle)',
            color: m.role === 'me' ? 'var(--accent-fg)' : 'var(--text)',
            fontSize: 12.5, lineHeight: 1.5,
            opacity: m.pending ? 0.6 : 1,
          }}>{m.text}</div>
        ))}
      </div>
      <div style={{ padding: 10, borderTop: '1px solid var(--border)', display: 'flex', gap: 6 }}>
        <input className="input" value={draft} onChange={e => setDraft(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && send()}
          placeholder="Ask about stack, roadmap, tradeoffs…" style={{ fontSize: 12 }} />
        <button className="btn btn-primary btn-sm" onClick={send}><Icon name="send" size={12} /></button>
      </div>
    </div>
  );
}

// ─── Chat (realtime) ──────────────────────────────────────────────
function ChatScreen() {
  const [activeId, setActiveId] = React.useState('c1');
  const [messages, setMessages] = React.useState(MOCK.messages.c1);
  const [draft, setDraft] = React.useState('');
  const [typing, setTyping] = React.useState(false);
  const scrollRef = React.useRef();
  const active = MOCK.chats.find(c => c.id === activeId);

  React.useEffect(() => { scrollRef.current?.scrollTo({ top: 99999 }); }, [messages]);

  const send = () => {
    if (!draft.trim()) return;
    setMessages(m => [...m, { id: 'nm' + Date.now(), from: MOCK.me.name, me: true, text: draft, time: 'now' }]);
    setDraft('');
    setTimeout(() => setTyping(true), 600);
    setTimeout(() => {
      setTyping(false);
      setMessages(m => [...m, { id: 'reply' + Date.now(), from: 'Tomás Vega', text: "Got it — I'll look at this after lunch.", time: 'now' }]);
    }, 2400);
  };

  return (
    <div style={{ height: '100%', display: 'grid', gridTemplateColumns: '280px 1fr 260px' }}>
      {/* Conversations list */}
      <div style={{ borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--border)' }}>
          <div className="caption" style={{ marginBottom: 8 }}>[Messages]</div>
          <div style={{ position: 'relative' }}>
            <Icon name="search" size={13} style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
            <input className="input" placeholder="Search…" style={{ paddingLeft: 30, fontSize: 12, height: 32 }} />
          </div>
        </div>
        <div style={{ flex: 1, overflowY: 'auto' }}>
          {MOCK.chats.map(c => (
            <button key={c.id} onClick={() => setActiveId(c.id)} style={{
              display: 'flex', gap: 10, alignItems: 'center',
              width: '100%', padding: '12px 16px', border: 'none',
              background: activeId === c.id ? 'var(--bg-subtle)' : 'transparent',
              borderLeft: `3px solid ${activeId === c.id ? 'var(--accent)' : 'transparent'}`,
              textAlign: 'left', cursor: 'pointer',
            }}>
              {c.kind === 'project' ? (
                <div style={{ width: 34, height: 34, borderRadius: 8, background: `oklch(0.72 0.12 ${c.name.charCodeAt(0) * 7 % 360})`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 13, flexShrink: 0 }}>#</div>
              ) : <Avatar name={c.name} size={34} />}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6 }}>
                  <span style={{ fontSize: 13, fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.name}</span>
                  <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>{c.time}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6, marginTop: 2 }}>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.last}</span>
                  {c.unread > 0 && <span style={{ fontSize: 10, padding: '1px 6px', background: 'var(--accent)', color: 'var(--accent-fg)', borderRadius: 999, fontWeight: 500 }}>{c.unread}</span>}
                </div>
              </div>
            </button>
          ))}
        </div>
      </div>

      {/* Messages */}
      <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <div style={{ padding: '12px 18px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 10 }}>
          <div>
            <div style={{ fontSize: 14, fontWeight: 600 }}>{active.name}</div>
            <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)' }}>4 members · Helix core team</div>
          </div>
          <div style={{ flex: 1 }} />
          <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="search" size={14} /></button>
          <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="moreV" size={14} /></button>
        </div>
        <div ref={scrollRef} style={{ flex: 1, overflowY: 'auto', padding: '20px 18px', display: 'flex', flexDirection: 'column', gap: 12 }}>
          {messages.map((m, i) => (
            <div key={m.id} style={{
              display: 'flex', gap: 10, alignItems: 'flex-start',
              flexDirection: m.me ? 'row-reverse' : 'row',
            }}>
              <Avatar name={m.from} size={28} />
              <div style={{ maxWidth: '70%' }}>
                {!m.me && <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 3, marginLeft: 4 }}>{m.from} · {m.time}</div>}
                <div style={{
                  padding: '9px 13px', borderRadius: 14,
                  background: m.me ? 'var(--accent)' : 'var(--bg-subtle)',
                  color: m.me ? 'var(--accent-fg)' : 'var(--text)',
                  fontSize: 13, lineHeight: 1.5,
                }}>{m.text}</div>
              </div>
            </div>
          ))}
          {typing && (
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', color: 'var(--text-muted)' }}>
              <Avatar name="Tomás Vega" size={24} />
              <div style={{ padding: '7px 11px', background: 'var(--bg-subtle)', borderRadius: 12, display: 'flex', gap: 3 }}>
                <span className="pulse-dot" style={{ width: 5, height: 5, background: 'currentColor', borderRadius: 999, animationDelay: '0s' }} />
                <span className="pulse-dot" style={{ width: 5, height: 5, background: 'currentColor', borderRadius: 999, animationDelay: '0.2s' }} />
                <span className="pulse-dot" style={{ width: 5, height: 5, background: 'currentColor', borderRadius: 999, animationDelay: '0.4s' }} />
              </div>
            </div>
          )}
        </div>
        <div style={{ padding: 12, borderTop: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', gap: 6, alignItems: 'center', background: 'var(--bg-subtle)', padding: 4, borderRadius: 'var(--r-md)' }}>
            <button className="btn-ghost" style={{ padding: 6, border: 'none', background: 'transparent' }}><Icon name="paperclip" size={15} /></button>
            <input value={draft} onChange={e => setDraft(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && send()}
              placeholder={`Message #${active.name}`}
              style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', fontFamily: 'inherit', fontSize: 13, padding: '6px 4px', color: 'var(--text)' }} />
            <button className="btn btn-primary btn-sm" onClick={send}>
              <Icon name="send" size={12} /> Send
            </button>
          </div>
        </div>
      </div>

      {/* Right: channel info */}
      <div style={{ borderLeft: '1px solid var(--border)', padding: 16, overflowY: 'auto' }} className="hide-mobile">
        <div className="caption" style={{ marginBottom: 10 }}>[Members]</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 20 }}>
          {MOCK.users.slice(0, 4).map(u => (
            <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{ position: 'relative' }}>
                <Avatar name={u.name} size={26} />
                <div style={{ position: 'absolute', bottom: -1, right: -1, width: 8, height: 8, borderRadius: 999, background: 'var(--success)', border: '2px solid var(--bg)' }} />
              </div>
              <div style={{ fontSize: 12, fontWeight: 500 }}>{u.name}</div>
            </div>
          ))}
        </div>
        <div className="caption" style={{ marginBottom: 10 }}>[Pinned]</div>
        <div className="card" style={{ padding: 10, fontSize: 11, color: 'var(--text-muted)' }}>
          Standup Tuesdays 10:00 CET. Notes in /docs/standups.
        </div>
      </div>
    </div>
  );
}

// ─── Profile ──────────────────────────────────────────────────────
function Profile({ setRoute }) {
  const [tab, setTab] = React.useState('projects');
  return (
    <div style={{ overflowY: 'auto', height: '100%' }}>
      <div style={{
        height: 170, position: 'relative', overflow: 'hidden',
        background: `
          radial-gradient(60% 100% at 15% 80%, color-mix(in oklch, var(--accent) 28%, transparent), transparent 70%),
          radial-gradient(50% 90% at 85% 20%, color-mix(in oklch, var(--accent) 18%, transparent), transparent 70%),
          linear-gradient(180deg, var(--bg-subtle), var(--bg))
        `,
      }}>
        <svg width="100%" height="100%" style={{ position: 'absolute', inset: 0, opacity: 0.18 }}>
          <defs>
            <pattern id="pf-grid" width="28" height="28" patternUnits="userSpaceOnUse">
              <path d="M28 0H0V28" fill="none" stroke="currentColor" strokeWidth="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#pf-grid)" style={{ color: 'var(--text-muted)' }} />
        </svg>
        <div className="mono" style={{
          position: 'absolute', right: 24, bottom: 14,
          fontSize: 11, color: 'var(--text-muted)', opacity: 0.55,
          letterSpacing: 0.5, display: 'flex', gap: 12,
        }}>
          <span>&gt; alex.kurenkov</span>
          <span>·</span>
          <span>devhunt://profile</span>
          <span>·</span>
          <span>since 2024</span>
        </div>
        <div style={{ position: 'absolute', inset: 0, background: 'linear-gradient(180deg, transparent 55%, var(--bg) 100%)' }} />
      </div>
      <div style={{ padding: '0 calc(28px * var(--d)) calc(28px * var(--d))', maxWidth: 1100, margin: '0 auto' }}>
        <div style={{ display: 'flex', gap: 20, alignItems: 'flex-end', marginTop: -44, flexWrap: 'wrap', position: 'relative' }}>
          <div style={{ border: '4px solid var(--bg)', borderRadius: 999, boxShadow: 'var(--shadow-md)' }}>
            <Avatar name={MOCK.me.name} size={96} />
          </div>
          <div style={{ flex: 1, paddingBottom: 8, minWidth: 240 }}>
            <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.3 }}>{MOCK.me.name}</h1>
            <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>@{MOCK.me.username} · {MOCK.me.location}</div>
            <div style={{ fontSize: 14, marginTop: 8, maxWidth: 540 }}>{MOCK.me.bio}</div>
            <div style={{ display: 'flex', gap: 16, marginTop: 10, fontSize: 12, color: 'var(--text-muted)' }}>
              <span><strong style={{ color: 'var(--text)' }}>{MOCK.me.followers}</strong> followers</span>
              <span><strong style={{ color: 'var(--text)' }}>{MOCK.me.following}</strong> following</span>
              <span><Icon name="github" size={12} /> @{MOCK.me.github}</span>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8, paddingBottom: 4 }}>
            <button className="btn" onClick={() => setRoute && setRoute('profile/edit')}><Icon name="edit" size={12} /> Edit profile</button>
            <button className="btn" onClick={() => setRoute && setRoute('settings')}><Icon name="settings" size={12} /> Privacy</button>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 4, marginTop: 28, borderBottom: '1px solid var(--border)' }}>
          {['projects','badges','skills','activity'].map(t => (
            <button key={t} onClick={() => setTab(t)} style={{
              padding: '10px 16px', border: 'none', background: 'transparent',
              borderBottom: `2px solid ${tab === t ? 'var(--accent)' : 'transparent'}`,
              color: tab === t ? 'var(--text)' : 'var(--text-muted)',
              fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
            }}>{t}</button>
          ))}
        </div>

        <div style={{ marginTop: 20 }}>
          {tab === 'projects' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 12 }}>
              {MOCK.projects.slice(0, 4).map(p => <ProjectCard key={p.id} project={p} onClick={() => {}} />)}
            </div>
          )}
          {tab === 'badges' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12 }}>
              {MOCK.badges.map(b => (
                <div key={b.code} className="card" style={{ padding: 18, textAlign: 'center', opacity: b.earned ? 1 : 0.6 }}>
                  <div style={{
                    width: 56, height: 56, borderRadius: 999, margin: '0 auto 10px',
                    background: b.earned ? 'var(--accent)' : 'var(--bg-subtle)',
                    color: b.earned ? 'var(--accent-fg)' : 'var(--text-muted)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    filter: b.earned ? 'none' : 'grayscale(1)',
                  }}>
                    <Icon name="trophy" size={24} />
                  </div>
                  <div style={{ fontSize: 13, fontWeight: 600 }}>{b.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 4, minHeight: 28 }}>{b.desc}</div>
                  {b.earned ? (
                    <span className="chip accent" style={{ fontSize: 10, marginTop: 8 }}>Earned</span>
                  ) : (
                    <div style={{ marginTop: 10 }}>
                      <div style={{ height: 4, background: 'var(--bg-subtle)', borderRadius: 999, overflow: 'hidden' }}>
                        <div style={{ height: '100%', width: `${b.progress * 100}%`, background: 'var(--accent)', borderRadius: 999 }} />
                      </div>
                      <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4 }}>{Math.round(b.progress * 100)}%</div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
          {tab === 'skills' && (
            <div className="card" style={{ padding: 18 }}>
              {[
                ['TypeScript', 5, 8], ['Go', 4, 5], ['React', 5, 7], ['Postgres', 4, 6], ['Rust', 3, 2],
              ].map(([s, lvl, yrs]) => (
                <div key={s} style={{ display: 'grid', gridTemplateColumns: '140px 1fr 80px', gap: 12, alignItems: 'center', padding: '10px 0', borderBottom: '1px solid var(--border)' }}>
                  <span style={{ fontSize: 13, fontWeight: 500 }}>{s}</span>
                  <div style={{ display: 'flex', gap: 3 }}>
                    {Array.from({ length: 5 }, (_, i) => (
                      <div key={i} style={{ flex: 1, height: 6, borderRadius: 2, background: i < lvl ? 'var(--accent)' : 'var(--bg-subtle)' }} />
                    ))}
                  </div>
                  <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'right' }}>{yrs} yrs</span>
                </div>
              ))}
            </div>
          )}
          {tab === 'activity' && (
            <div className="card" style={{ padding: 18, display: 'grid', gridTemplateColumns: 'repeat(52, 1fr)', gap: 3 }}>
              {Array.from({ length: 52 * 7 }, (_, i) => {
                const intensity = Math.random();
                return <div key={i} style={{
                  aspectRatio: 1, borderRadius: 2,
                  background: intensity < 0.45 ? 'var(--bg-subtle)' : intensity < 0.7 ? 'color-mix(in oklch, var(--accent) 30%, var(--bg-subtle))' : intensity < 0.88 ? 'color-mix(in oklch, var(--accent) 60%, var(--bg-subtle))' : 'var(--accent)',
                }} />;
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Admin / Moderation ───────────────────────────────────────────
function Admin() {
  const [reports, setReports] = React.useState(MOCK.reports);
  const [tab, setTab] = React.useState('queue');
  return (
    <div style={{ padding: 'calc(28px * var(--d))', maxWidth: 1200, margin: '0 auto' }}>
      <div style={{ marginBottom: 24, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <div className="caption" style={{ marginBottom: 6 }}>[Moderation]</div>
          <h1 style={{ margin: 0, fontSize: 28, fontWeight: 600, letterSpacing: -0.3 }}>Admin panel</h1>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <span className="chip" style={{ background: 'color-mix(in oklch, var(--danger) 15%, transparent)', color: 'var(--danger)', borderColor: 'transparent' }}>2 urgent</span>
          <button className="btn btn-sm"><Icon name="shield" size={12} /> Audit log</button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 10, marginBottom: 20 }}>
        {[
          ['Pending reports', '12', '+3 today', 'danger'],
          ['Users flagged', '4', 'bots', 'warning'],
          ['Projects hidden', '2', 'this week', 'text-muted'],
          ['Avg. response', '2.4h', '-0.8h', 'success'],
        ].map(([l, v, d, c]) => (
          <div key={l} className="card" style={{ padding: 14 }}>
            <div className="caption" style={{ marginBottom: 6 }}>{l}</div>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
              <span style={{ fontSize: 24, fontWeight: 600 }}>{v}</span>
              <span className="mono" style={{ fontSize: 10, color: `var(--${c})` }}>{d}</span>
            </div>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 4, marginBottom: 14, borderBottom: '1px solid var(--border)' }}>
        {['queue','users','projects','audit','support'].map(t => (
          <button key={t} onClick={() => setTab(t)} style={{
            padding: '10px 16px', border: 'none', background: 'transparent',
            borderBottom: `2px solid ${tab === t ? 'var(--accent)' : 'transparent'}`,
            color: tab === t ? 'var(--text)' : 'var(--text-muted)',
            fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
          }}>{t}</button>
        ))}
      </div>

      {tab === 'queue' && (
        <div className="card" style={{ overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 100px 220px', gap: 12, padding: '10px 16px', borderBottom: '1px solid var(--border)', background: 'var(--bg-subtle)' }}>
            {['Target','Reason','Reporter','Severity','Actions'].map(h => (
              <div key={h} className="caption">{h}</div>
            ))}
          </div>
          {reports.map(r => (
            <div key={r.id} style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 100px 220px', gap: 12, padding: '12px 16px', borderBottom: '1px solid var(--border)', alignItems: 'center', fontSize: 12 }}>
              <div style={{ fontWeight: 500 }}>{r.target}</div>
              <div style={{ color: 'var(--text-muted)' }}>{r.reason}</div>
              <div style={{ color: 'var(--text-muted)' }}>{r.reporter}</div>
              <div>
                <span className="chip" style={{
                  fontSize: 10,
                  background: r.severity === 'high' ? 'color-mix(in oklch, var(--danger) 15%, transparent)' : r.severity === 'med' ? 'color-mix(in oklch, var(--warning) 15%, transparent)' : 'var(--bg-subtle)',
                  color: r.severity === 'high' ? 'var(--danger)' : r.severity === 'med' ? 'var(--warning)' : 'var(--text-muted)',
                  borderColor: 'transparent',
                }}>{r.severity}</span>
              </div>
              <div style={{ display: 'flex', gap: 6 }}>
                <button className="btn btn-sm">Dismiss</button>
                <button className="btn btn-sm">Warn</button>
                <button className="btn btn-sm" style={{ color: 'var(--danger)' }}
                  onClick={() => setReports(rs => rs.filter(x => x.id !== r.id))}>Remove</button>
              </div>
            </div>
          ))}
        </div>
      )}
      {tab === 'users' && (
        <div className="card" style={{ overflow: 'hidden' }}>
          {MOCK.users.map((u, i) => (
            <div key={u.id} style={{ display: 'grid', gridTemplateColumns: '1fr 140px 100px 160px', gap: 12, padding: '14px 16px', borderBottom: i < MOCK.users.length - 1 ? '1px solid var(--border)' : 'none', alignItems: 'center', fontSize: 12 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Avatar name={u.name} size={30} />
                <div>
                  <div style={{ fontWeight: 500 }}>{u.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{u.title}</div>
                </div>
              </div>
              <span className="chip success" style={{ fontSize: 10, width: 'fit-content' }}>active</span>
              <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>user</span>
              <div style={{ display: 'flex', gap: 6 }}>
                <button className="btn btn-sm">Verify</button>
                <button className="btn btn-sm">Block</button>
              </div>
            </div>
          ))}
        </div>
      )}
      {(tab === 'projects' || tab === 'audit' || tab === 'support') && (
        <div className="card" style={{ padding: 40, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>
          <Icon name="folder" size={28} style={{ marginBottom: 10, opacity: 0.5 }} />
          <div>{tab} view — use filters to narrow scope.</div>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { AIPlan, ChatScreen, Profile, Admin, ChatDock });

// ─── Floating Chat Dock ───────────────────────────────────────────
function ChatDock({ onOpenFullChat }) {
  const [open, setOpen] = React.useState(false);
  const [activeId, setActiveId] = React.useState(null); // null = list view
  const [draft, setDraft] = React.useState('');
  const [messagesByChat, setMessagesByChat] = React.useState(() => ({
    c1: MOCK.messages.c1.slice(),
    c2: [{ id: 'dm1', from: 'Mira Chen', text: 'Can we sync tomorrow about the iOS role?', time: '13:02' }],
    c3: [{ id: 'dm2', from: 'Priya Shah', text: 'Latest mocks pushed ↑', time: '10:41' }],
    c4: [{ id: 'dm3', from: 'Jon Okafor', text: 'CI is green again', time: 'yday' }],
    c5: [{ id: 'dm4', from: 'Drift team', text: 'voice note 0:42', time: '2d' }],
  }));
  const scrollRef = React.useRef();
  const totalUnread = MOCK.chats.reduce((s, c) => s + c.unread, 0);

  React.useEffect(() => { if (open && activeId) scrollRef.current?.scrollTo({ top: 99999 }); }, [messagesByChat, activeId, open]);

  const send = () => {
    if (!draft.trim() || !activeId) return;
    const text = draft;
    setMessagesByChat(m => ({ ...m, [activeId]: [...(m[activeId] || []), { id: 'my' + Date.now(), from: MOCK.me.name, me: true, text, time: 'now' }] }));
    setDraft('');
    setTimeout(() => {
      const replies = {
        c1: 'Got it — pulling and re-running.',
        c2: 'Sounds good. 10am your time?',
        c3: 'On it, will leave review comments.',
        c4: 'Nice, thanks!',
        c5: 'Will listen and reply.',
      };
      setMessagesByChat(m => ({ ...m, [activeId]: [...(m[activeId] || []), { id: 'r' + Date.now(), from: MOCK.chats.find(c => c.id === activeId).name, text: replies[activeId] || 'Ack.', time: 'now' }] }));
    }, 1400);
  };

  const active = activeId && MOCK.chats.find(c => c.id === activeId);
  const activeMsgs = activeId ? (messagesByChat[activeId] || []) : [];

  return (
    <>
      {/* FAB */}
      <button onClick={() => setOpen(v => !v)} className="chat-dock-fab"
        style={{
          position: 'fixed', bottom: 20, right: 20, zIndex: 180,
          width: 52, height: 52, borderRadius: 999,
          background: 'var(--accent)', color: 'var(--accent-fg)',
          border: 'none', boxShadow: 'var(--shadow-lg)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          transition: 'transform 0.15s ease',
        }}
        title="Messages">
        <Icon name={open ? 'x' : 'chat'} size={20} />
        {!open && totalUnread > 0 && (
          <span style={{
            position: 'absolute', top: 4, right: 4,
            minWidth: 18, height: 18, padding: '0 5px',
            borderRadius: 999, background: 'var(--danger)', color: '#fff',
            fontSize: 10, fontWeight: 600, display: 'flex', alignItems: 'center', justifyContent: 'center',
            border: '2px solid var(--bg)',
          }}>{totalUnread}</span>
        )}
      </button>

      {/* Panel */}
      {open && (
        <div className="chat-dock-panel fade-in"
          style={{
            position: 'fixed', bottom: 84, right: 20, zIndex: 180,
            width: 360, height: 520, maxHeight: 'calc(100vh - 120px)',
            background: 'var(--bg-elev)', border: '1px solid var(--border)',
            borderRadius: 'var(--r-lg)', boxShadow: 'var(--shadow-lg)',
            display: 'flex', flexDirection: 'column', overflow: 'hidden',
          }}>
          {/* Header */}
          <div style={{ padding: '12px 14px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8 }}>
            {activeId ? (
              <>
                <button onClick={() => setActiveId(null)} className="btn-ghost"
                  style={{ padding: 4, border: 'none', background: 'transparent' }}>
                  <Icon name="chevronLeft" size={16} />
                </button>
                <Avatar name={active.name} size={26} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12, fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{active.name}</div>
                  <div className="mono" style={{ fontSize: 10, color: 'var(--success)' }}>● online</div>
                </div>
                <button onClick={() => { setOpen(false); onOpenFullChat?.(); }} className="btn-ghost" title="Open full chat"
                  style={{ padding: 4, border: 'none', background: 'transparent', color: 'var(--text-muted)' }}>
                  <Icon name="arrowUpRight" size={14} />
                </button>
              </>
            ) : (
              <>
                <Icon name="chat" size={15} />
                <span style={{ fontSize: 13, fontWeight: 600, flex: 1 }}>Messages</span>
                <span className="chip" style={{ fontSize: 10 }}>{MOCK.chats.length}</span>
                <button onClick={() => setOpen(false)} className="btn-ghost" style={{ padding: 4, border: 'none', background: 'transparent' }}>
                  <Icon name="x" size={14} />
                </button>
              </>
            )}
          </div>

          {/* Body */}
          {!activeId ? (
            <div style={{ flex: 1, overflowY: 'auto' }}>
              {MOCK.chats.map(c => (
                <button key={c.id} onClick={() => setActiveId(c.id)}
                  style={{
                    width: '100%', display: 'flex', gap: 10, alignItems: 'center',
                    padding: '10px 14px', border: 'none', background: 'transparent',
                    borderBottom: '1px solid var(--border)', textAlign: 'left', cursor: 'pointer',
                  }}>
                  {c.kind === 'project' ? (
                    <div style={{ width: 30, height: 30, borderRadius: 7, background: `oklch(0.72 0.12 ${c.name.charCodeAt(0) * 7 % 360})`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 12, flexShrink: 0 }}>#</div>
                  ) : <Avatar name={c.name} size={30} />}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6 }}>
                      <span style={{ fontSize: 12.5, fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.name}</span>
                      <span className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', flexShrink: 0 }}>{c.time}</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6, marginTop: 2, alignItems: 'center' }}>
                      <span style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.last}</span>
                      {c.unread > 0 && <span style={{ fontSize: 9, padding: '1px 6px', background: 'var(--accent)', color: 'var(--accent-fg)', borderRadius: 999, fontWeight: 600, flexShrink: 0 }}>{c.unread}</span>}
                    </div>
                  </div>
                </button>
              ))}
              <button onClick={() => { setOpen(false); onOpenFullChat?.(); }}
                style={{ width: '100%', padding: '12px', border: 'none', background: 'transparent', color: 'var(--accent)', fontSize: 12, fontWeight: 500, cursor: 'pointer' }}>
                Open full messages →
              </button>
            </div>
          ) : (
            <>
              <div ref={scrollRef} style={{ flex: 1, overflowY: 'auto', padding: '14px', display: 'flex', flexDirection: 'column', gap: 10 }}>
                {activeMsgs.map((m, i) => (
                  <div key={m.id} style={{
                    display: 'flex', gap: 8, alignItems: 'flex-start',
                    flexDirection: m.me ? 'row-reverse' : 'row',
                  }}>
                    {!m.me && <Avatar name={m.from} size={22} />}
                    <div style={{ maxWidth: '78%' }}>
                      {!m.me && <div className="mono" style={{ fontSize: 9.5, color: 'var(--text-muted)', marginBottom: 2, marginLeft: 3 }}>{m.from}</div>}
                      <div style={{
                        padding: '7px 11px', borderRadius: 12,
                        background: m.me ? 'var(--accent)' : 'var(--bg-subtle)',
                        color: m.me ? 'var(--accent-fg)' : 'var(--text)',
                        fontSize: 12.5, lineHeight: 1.45,
                      }}>{m.text}</div>
                    </div>
                  </div>
                ))}
              </div>
              <div style={{ padding: 10, borderTop: '1px solid var(--border)', display: 'flex', gap: 6, alignItems: 'center' }}>
                <input className="input" value={draft} onChange={e => setDraft(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && send()}
                  placeholder="Message…" style={{ fontSize: 12, height: 34 }} />
                <button className="btn btn-primary btn-sm" onClick={send} style={{ padding: '6px 10px' }}>
                  <Icon name="send" size={12} />
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </>
  );
}

Object.assign(window, { ChatDock });
