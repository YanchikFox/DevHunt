// DevHunt — Edit Profile & Settings pages

// ─── Shared page header with breadcrumb back ─────────────────────
function PageHeader({ setRoute, back, crumb, title, subtitle, actions }) {
  return (
    <div style={{ marginBottom: 24 }}>
      <div className="caption" style={{ marginBottom: 6, display: 'flex', alignItems: 'center', gap: 6 }}>
        {back && (
          <>
            <button onClick={() => setRoute(back.route)}
              style={{ background: 'transparent', border: 'none', padding: 0, color: 'var(--text-muted)', fontFamily: 'inherit', fontSize: 'inherit', letterSpacing: 'inherit', textTransform: 'inherit', cursor: 'pointer' }}
              onMouseEnter={e => e.currentTarget.style.color = 'var(--accent)'}
              onMouseLeave={e => e.currentTarget.style.color = 'var(--text-muted)'}>
              ← [{back.label}]
            </button>
            <span style={{ opacity: 0.5 }}>/</span>
          </>
        )}
        <span>[{crumb}]</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 26, fontWeight: 600, letterSpacing: -0.3 }}>{title}</h1>
          {subtitle && <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 4 }}>{subtitle}</div>}
        </div>
        {actions}
      </div>
    </div>
  );
}

// ─── Two-column section: label + control on the right ────────────
function FormRow({ label, hint, children }) {
  return (
    <div className="form-row" style={{ padding: '18px 0', borderBottom: '1px solid var(--border)' }}>
      <div className="form-row-label">
        <div style={{ fontSize: 13, fontWeight: 500 }}>{label}</div>
        {hint && <div style={{ fontSize: 11.5, color: 'var(--text-muted)', marginTop: 4, lineHeight: 1.5 }}>{hint}</div>}
      </div>
      <div style={{ minWidth: 0 }}>{children}</div>
    </div>
  );
}

function FormSection({ title, description, children }) {
  return (
    <section className="card form-section" style={{ padding: '4px 24px 20px', marginBottom: 18 }}>
      <div style={{ padding: '18px 0 10px', borderBottom: '1px solid var(--border)' }}>
        <div style={{ fontSize: 15, fontWeight: 600 }}>{title}</div>
        {description && <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 3 }}>{description}</div>}
      </div>
      {children}
    </section>
  );
}

// ─── Toggle switch ───────────────────────────────────────────────
function Switch({ checked, onChange, label }) {
  return (
    <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', userSelect: 'none' }}>
      <button type="button" role="switch" aria-checked={checked}
        onClick={() => onChange(!checked)}
        style={{
          width: 34, height: 20, borderRadius: 999, border: 'none',
          background: checked ? 'var(--accent)' : 'var(--border-strong)',
          position: 'relative', cursor: 'pointer', transition: 'background 0.15s',
          padding: 0, flexShrink: 0,
        }}>
        <span style={{
          position: 'absolute', top: 2, left: checked ? 16 : 2,
          width: 16, height: 16, borderRadius: 999, background: '#fff',
          transition: 'left 0.15s', boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
        }} />
      </button>
      {label && <span style={{ fontSize: 13 }}>{label}</span>}
    </label>
  );
}

// ─── Edit Profile page ───────────────────────────────────────────
function ProfileEditPage({ setRoute }) {
  const me = MOCK.me;
  const [form, setForm] = React.useState({
    name: me.name,
    username: me.username,
    title: me.title,
    bio: me.bio,
    location: me.location,
    website: 'alexk.dev',
    pronouns: 'he/him',
    timezone: 'Europe/Berlin',
    github: me.github,
    twitter: 'alexk_dev',
    linkedin: 'alexkurenkov',
    skills: ['TypeScript', 'Go', 'Postgres', 'React', 'Node.js'],
    openToWork: true,
    lookingFor: ['Frontend-heavy collabs', 'Dev tools', 'Open source'],
    availability: '10-15 hrs/week',
  });
  const [skillDraft, setSkillDraft] = React.useState('');
  const [saved, setSaved] = React.useState(false);
  const [dirty, setDirty] = React.useState(false);

  const update = (k, v) => { setForm(f => ({ ...f, [k]: v })); setDirty(true); setSaved(false); };

  const save = () => {
    setSaved(true);
    setDirty(false);
    setTimeout(() => setSaved(false), 2400);
  };

  const addSkill = () => {
    const s = skillDraft.trim();
    if (!s || form.skills.includes(s)) return;
    update('skills', [...form.skills, s]);
    setSkillDraft('');
  };
  const removeSkill = (s) => update('skills', form.skills.filter(x => x !== s));

  const addInterest = () => update('lookingFor', [...form.lookingFor, 'New interest']);
  const removeInterest = (i) => update('lookingFor', form.lookingFor.filter((_, j) => j !== i));

  return (
    <div style={{ padding: 'calc(24px * var(--d)) calc(28px * var(--d))', maxWidth: 880, margin: '0 auto' }}>
      <PageHeader
        setRoute={setRoute}
        back={{ route: 'profile', label: me.name }}
        crumb="Edit profile"
        title="Edit profile"
        subtitle="Your public identity on DevHunt. Changes save instantly when you hit Save."
        actions={
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            {saved && <span className="mono" style={{ fontSize: 11, color: 'var(--success)' }}>✓ saved</span>}
            <button className="btn" onClick={() => setRoute('profile')}>Cancel</button>
            <button className="btn btn-primary" onClick={save} disabled={!dirty}
              style={{ opacity: dirty ? 1 : 0.5, cursor: dirty ? 'pointer' : 'default' }}>
              {dirty ? 'Save changes' : 'Saved'}
            </button>
          </div>
        }
      />

      <FormSection title="Identity" description="How your name, avatar, and handle appear across DevHunt.">
        <FormRow label="Avatar" hint="A recognizable image helps teammates find you in chat and on the board.">
          <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
            <Avatar name={form.name} size={72} />
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <button className="btn btn-sm"><Icon name="plus" size={11} /> Upload image</button>
              <button className="btn btn-sm" style={{ color: 'var(--danger)' }}>Remove</button>
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 180 }}>
              PNG or JPG, 1:1, up to 2MB. We'll generate initials otherwise.
            </div>
          </div>
        </FormRow>

        <FormRow label="Display name" hint="Shown on your profile, comments, and team pages.">
          <input className="input" value={form.name} onChange={e => update('name', e.target.value)} />
        </FormRow>

        <FormRow label="Handle" hint="@username — used in mentions and your profile URL.">
          <div style={{ display: 'flex', alignItems: 'center', gap: 0 }}>
            <span className="mono" style={{ padding: '8px 10px', fontSize: 12, background: 'var(--bg-subtle)', border: '1px solid var(--border)', borderRight: 'none', borderRadius: 'var(--r-md) 0 0 var(--r-md)', color: 'var(--text-muted)' }}>devhunt.io/@</span>
            <input className="input" value={form.username} onChange={e => update('username', e.target.value.replace(/[^a-z0-9_-]/gi, '').toLowerCase())}
              style={{ borderRadius: '0 var(--r-md) var(--r-md) 0' }} />
          </div>
        </FormRow>

        <FormRow label="Role / headline" hint="One line that describes what you do.">
          <input className="input" value={form.title} onChange={e => update('title', e.target.value)} maxLength={80} />
          <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4, textAlign: 'right' }}>{form.title.length} / 80</div>
        </FormRow>

        <FormRow label="Bio" hint="A short paragraph about you. Markdown links supported.">
          <textarea className="input" value={form.bio} onChange={e => update('bio', e.target.value)} rows={4} maxLength={280}
            style={{ resize: 'vertical', padding: 10, lineHeight: 1.5 }} />
          <div className="mono" style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4, textAlign: 'right' }}>{form.bio.length} / 280</div>
        </FormRow>
      </FormSection>

      <FormSection title="Details">
        <FormRow label="Location"><input className="input" value={form.location} onChange={e => update('location', e.target.value)} /></FormRow>
        <FormRow label="Pronouns"><input className="input" value={form.pronouns} onChange={e => update('pronouns', e.target.value)} placeholder="e.g. she/her, they/them" /></FormRow>
        <FormRow label="Website">
          <div style={{ display: 'flex' }}>
            <span className="mono" style={{ padding: '8px 10px', fontSize: 12, background: 'var(--bg-subtle)', border: '1px solid var(--border)', borderRight: 'none', borderRadius: 'var(--r-md) 0 0 var(--r-md)', color: 'var(--text-muted)' }}>https://</span>
            <input className="input" value={form.website} onChange={e => update('website', e.target.value)} style={{ borderRadius: '0 var(--r-md) var(--r-md) 0' }} />
          </div>
        </FormRow>
        <FormRow label="Timezone">
          <select className="input" value={form.timezone} onChange={e => update('timezone', e.target.value)}>
            {['Europe/Berlin', 'Europe/London', 'America/New_York', 'America/Los_Angeles', 'Asia/Tokyo', 'Asia/Singapore', 'Australia/Sydney'].map(tz => <option key={tz}>{tz}</option>)}
          </select>
        </FormRow>
      </FormSection>

      <FormSection title="Skills & interests" description="Power AI matchmaking. More specific = better matches.">
        <FormRow label="Skills" hint="The technologies, tools, and domains you work with.">
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 8 }}>
            {form.skills.map(s => (
              <span key={s} className="chip" style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '3px 4px 3px 10px' }}>
                {s}
                <button onClick={() => removeSkill(s)} style={{ border: 'none', background: 'transparent', padding: 2, display: 'flex', cursor: 'pointer', color: 'var(--text-muted)' }}>
                  <Icon name="x" size={11} />
                </button>
              </span>
            ))}
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <input className="input" value={skillDraft} onChange={e => setSkillDraft(e.target.value)}
              onKeyDown={e => (e.key === 'Enter' || e.key === ',') && (e.preventDefault(), addSkill())}
              placeholder="Add a skill, press Enter…" />
            <button className="btn" onClick={addSkill}>Add</button>
          </div>
          <div style={{ marginTop: 10, display: 'flex', flexWrap: 'wrap', gap: 4 }}>
            <span className="caption" style={{ marginRight: 4 }}>[Suggested]</span>
            {['Rust', 'Kubernetes', 'GraphQL', 'Terraform', 'Python'].filter(s => !form.skills.includes(s)).map(s => (
              <button key={s} className="chip" style={{ fontSize: 10, cursor: 'pointer', opacity: 0.7 }}
                onClick={() => update('skills', [...form.skills, s])}>
                + {s}
              </button>
            ))}
          </div>
        </FormRow>

        <FormRow label="Looking for" hint="Types of projects or collaborations you'd like to join.">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {form.lookingFor.map((x, i) => (
              <div key={i} style={{ display: 'flex', gap: 6 }}>
                <input className="input" value={x}
                  onChange={e => update('lookingFor', form.lookingFor.map((y, j) => j === i ? e.target.value : y))} />
                <button className="btn btn-sm" onClick={() => removeInterest(i)} style={{ color: 'var(--danger)' }}>
                  <Icon name="trash" size={12} />
                </button>
              </div>
            ))}
            <button className="btn btn-sm" onClick={addInterest} style={{ alignSelf: 'flex-start', marginTop: 4 }}>
              <Icon name="plus" size={11} /> Add another
            </button>
          </div>
        </FormRow>

        <FormRow label="Availability" hint="Used for AI role matching so you don't get over-booked.">
          <select className="input" value={form.availability} onChange={e => update('availability', e.target.value)}>
            {['< 5 hrs/week', '5-10 hrs/week', '10-15 hrs/week', '15-25 hrs/week', '25+ hrs/week', 'Full-time looking'].map(a => <option key={a}>{a}</option>)}
          </select>
        </FormRow>

        <FormRow label="Open to work" hint="Shown as a badge on your profile and lets AI match you proactively.">
          <Switch checked={form.openToWork} onChange={v => update('openToWork', v)}
            label={form.openToWork ? 'Visible — appearing in AI matches' : 'Hidden'} />
        </FormRow>
      </FormSection>

      <FormSection title="Connected accounts" description="Link external accounts for richer profile and contribution proof.">
        <FormRow label={<><Icon name="github" size={12} /> GitHub</>} hint="Pulls commits, PRs, and languages into your profile.">
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <input className="input" value={form.github} onChange={e => update('github', e.target.value)} style={{ flex: 1 }} />
            <span className="chip" style={{ background: 'var(--success-weak, color-mix(in oklch, var(--success) 14%, transparent))', color: 'var(--success)', borderColor: 'transparent' }}>Connected</span>
          </div>
        </FormRow>
        <FormRow label="X / Twitter">
          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <input className="input" value={form.twitter} onChange={e => update('twitter', e.target.value)} style={{ flex: 1 }} placeholder="handle" />
            <button className="btn btn-sm">Connect</button>
          </div>
        </FormRow>
        <FormRow label="LinkedIn">
          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <input className="input" value={form.linkedin} onChange={e => update('linkedin', e.target.value)} style={{ flex: 1 }} placeholder="handle" />
            <button className="btn btn-sm">Connect</button>
          </div>
        </FormRow>
      </FormSection>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 20 }}>
        <button className="btn" onClick={() => setRoute('profile')}>Cancel</button>
        <button className="btn btn-primary" onClick={save} disabled={!dirty}
          style={{ opacity: dirty ? 1 : 0.5, cursor: dirty ? 'pointer' : 'default' }}>
          {dirty ? 'Save changes' : 'Saved'}
        </button>
      </div>
    </div>
  );
}

// ─── Settings & Privacy page ─────────────────────────────────────
function SettingsPage({ setRoute, theme, setTheme, tweaks, setTweaks }) {
  const [section, setSection] = React.useState('account');
  const [email, setEmail] = React.useState('alex@devhunt.io');
  const [privacy, setPrivacy] = React.useState({
    profileVisibility: 'public',    // public | community | private
    showLocation: true,
    showEmail: false,
    showActivity: true,
    discoverable: true,
    aiTraining: false,
    allowDMsFrom: 'connections',    // everyone | connections | none
    showOnlineStatus: true,
  });
  const [notif, setNotif] = React.useState({
    mentionsEmail: true,
    mentionsPush: true,
    invitesEmail: true,
    invitesPush: true,
    aiMatchesEmail: false,
    aiMatchesPush: true,
    marketingEmail: false,
    weeklyDigest: true,
    doNotDisturb: false,
  });

  const updatePrivacy = (k, v) => setPrivacy(p => ({ ...p, [k]: v }));
  const updateNotif = (k, v) => setNotif(n => ({ ...n, [k]: v }));

  const sections = [
    { id: 'account', label: 'Account', icon: 'user' },
    { id: 'privacy', label: 'Privacy', icon: 'shield' },
    { id: 'notifications', label: 'Notifications', icon: 'bell' },
    { id: 'appearance', label: 'Appearance', icon: 'moon' },
    { id: 'security', label: 'Security', icon: 'lock' },
    { id: 'data', label: 'Data & export', icon: 'folder' },
    { id: 'danger', label: 'Danger zone', icon: 'trash' },
  ];

  return (
    <div style={{ padding: 'calc(24px * var(--d)) calc(28px * var(--d))', maxWidth: 1100, margin: '0 auto' }}>
      <PageHeader
        setRoute={setRoute}
        back={{ route: 'profile', label: MOCK.me.name }}
        crumb="Settings"
        title="Settings & privacy"
        subtitle="Control who can see what, how DevHunt reaches you, and your account security."
      />

      <div style={{ display: 'grid', gridTemplateColumns: '200px 1fr', gap: 24, alignItems: 'start' }} className="settings-grid">
        {/* Section nav */}
        <aside style={{ position: 'sticky', top: 16, display: 'flex', flexDirection: 'column', gap: 2 }}>
          {sections.map(s => {
            const active = section === s.id;
            return (
              <button key={s.id} onClick={() => setSection(s.id)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 10,
                  padding: '8px 12px', border: 'none',
                  background: active ? 'var(--bg-subtle)' : 'transparent',
                  borderRadius: 'var(--r-md)', textAlign: 'left', cursor: 'pointer',
                  color: active ? 'var(--text)' : 'var(--text-muted)',
                  fontSize: 13, fontWeight: active ? 500 : 400,
                  boxShadow: active ? 'var(--shadow-sm)' : 'none',
                }}>
                <Icon name={s.icon} size={14} />
                {s.label}
              </button>
            );
          })}
        </aside>

        <div style={{ minWidth: 0 }}>
          {/* ── Account ──────────────────────────────────────── */}
          {section === 'account' && (
            <>
              <FormSection title="Account" description="Log-in credentials and the email DevHunt uses to reach you.">
                <FormRow label="Email" hint="All notifications and security alerts go here.">
                  <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                    <input className="input" value={email} onChange={e => setEmail(e.target.value)} style={{ flex: 1 }} />
                    <span className="chip" style={{ background: 'var(--success-weak, color-mix(in oklch, var(--success) 14%, transparent))', color: 'var(--success)', borderColor: 'transparent' }}>Verified</span>
                  </div>
                </FormRow>
                <FormRow label="Password" hint="Last changed 3 months ago.">
                  <button className="btn">Change password</button>
                </FormRow>
                <FormRow label="Language">
                  <select className="input" defaultValue="en">
                    <option value="en">English</option>
                    <option value="pl">Polski</option>
                    <option value="ru">Русский</option>
                    <option value="de">Deutsch</option>
                  </select>
                </FormRow>
              </FormSection>

              <FormSection title="Billing" description="DevHunt Core is free. Pro unlocks team AI and unlimited projects.">
                <FormRow label="Plan">
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span className="chip" style={{ fontSize: 11 }}>Free</span>
                    <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>3 of 5 project slots used</span>
                  </div>
                  <button className="btn btn-primary btn-sm" style={{ marginTop: 8 }}>Upgrade to Pro</button>
                </FormRow>
              </FormSection>
            </>
          )}

          {/* ── Privacy ──────────────────────────────────────── */}
          {section === 'privacy' && (
            <>
              <FormSection title="Profile visibility" description="Who can see your profile, activity, and projects.">
                <FormRow label="Profile">
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    {[
                      ['public', 'Public', 'Anyone on the internet can find your profile.'],
                      ['community', 'DevHunt only', 'Only logged-in DevHunt users can view your profile.'],
                      ['private', 'Private', 'Only people you accept as connections can view.'],
                    ].map(([v, l, d]) => (
                      <label key={v} style={{
                        display: 'flex', gap: 10, padding: 12,
                        border: `1px solid ${privacy.profileVisibility === v ? 'var(--accent)' : 'var(--border)'}`,
                        borderRadius: 'var(--r-md)', cursor: 'pointer',
                        background: privacy.profileVisibility === v ? 'var(--accent-weak)' : 'transparent',
                      }}>
                        <input type="radio" name="vis" checked={privacy.profileVisibility === v}
                          onChange={() => updatePrivacy('profileVisibility', v)} style={{ marginTop: 3 }} />
                        <div>
                          <div style={{ fontSize: 13, fontWeight: 500 }}>{l}</div>
                          <div style={{ fontSize: 11.5, color: 'var(--text-muted)', marginTop: 2 }}>{d}</div>
                        </div>
                      </label>
                    ))}
                  </div>
                </FormRow>
                <FormRow label="Show location"><Switch checked={privacy.showLocation} onChange={v => updatePrivacy('showLocation', v)} /></FormRow>
                <FormRow label="Show email on profile" hint="Off by default — collaborators can still DM you."><Switch checked={privacy.showEmail} onChange={v => updatePrivacy('showEmail', v)} /></FormRow>
                <FormRow label="Show activity graph" hint="Your commits, posts, and board activity as a heatmap."><Switch checked={privacy.showActivity} onChange={v => updatePrivacy('showActivity', v)} /></FormRow>
              </FormSection>

              <FormSection title="Discovery & contact">
                <FormRow label="Discoverable in search" hint="Allow users to find you via the global search."><Switch checked={privacy.discoverable} onChange={v => updatePrivacy('discoverable', v)} /></FormRow>
                <FormRow label="Show online status"><Switch checked={privacy.showOnlineStatus} onChange={v => updatePrivacy('showOnlineStatus', v)} /></FormRow>
                <FormRow label="Direct messages" hint="Who can start a conversation with you.">
                  <select className="input" value={privacy.allowDMsFrom} onChange={e => updatePrivacy('allowDMsFrom', e.target.value)}>
                    <option value="everyone">Anyone on DevHunt</option>
                    <option value="connections">Connections only</option>
                    <option value="none">Nobody</option>
                  </select>
                </FormRow>
              </FormSection>

              <FormSection title="AI & data" description="How DevHunt uses your data to improve recommendations.">
                <FormRow label="Use my public activity to train AI matching" hint="Helps AI Plan suggest better teammates. Limited to aggregated patterns — no private content.">
                  <Switch checked={privacy.aiTraining} onChange={v => updatePrivacy('aiTraining', v)} />
                </FormRow>
                <FormRow label="Personalize feed with my interests">
                  <Switch checked={true} onChange={() => {}} />
                </FormRow>
              </FormSection>

              <FormSection title="Blocked users" description="People who can't see your content or contact you.">
                <div style={{ padding: '20px 0', textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>
                  <Icon name="shield" size={22} style={{ opacity: 0.4, marginBottom: 8 }} />
                  <div>You haven't blocked anyone.</div>
                </div>
              </FormSection>
            </>
          )}

          {/* ── Notifications ────────────────────────────────── */}
          {section === 'notifications' && (
            <>
              <FormSection title="Do not disturb">
                <FormRow label="Pause all notifications" hint="Mute push and email notifications. We'll collect them for when you're back.">
                  <Switch checked={notif.doNotDisturb} onChange={v => updateNotif('doNotDisturb', v)} />
                </FormRow>
                {notif.doNotDisturb && (
                  <FormRow label="Until">
                    <select className="input" defaultValue="4h">
                      <option value="1h">In 1 hour</option>
                      <option value="4h">In 4 hours</option>
                      <option value="tomorrow">Tomorrow morning</option>
                      <option value="indefinite">Until I turn it back on</option>
                    </select>
                  </FormRow>
                )}
              </FormSection>

              <FormSection title="Notifications" description="Choose how you want to hear from DevHunt.">
                <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 80px 80px', gap: 12, padding: '14px 0 8px', borderBottom: '1px solid var(--border)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: 0.6 }}>
                  <span>Event</span>
                  <span style={{ textAlign: 'center' }}>Email</span>
                  <span style={{ textAlign: 'center' }}>Push</span>
                </div>
                {[
                  ['Mentions & replies', 'mentions'],
                  ['Invites to projects', 'invites'],
                  ['AI match suggestions', 'aiMatches'],
                ].map(([label, k]) => (
                  <div key={k} style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 80px 80px', gap: 12, padding: '14px 0', borderBottom: '1px solid var(--border)', alignItems: 'center' }}>
                    <span style={{ fontSize: 13 }}>{label}</span>
                    <div style={{ display: 'flex', justifyContent: 'center' }}>
                      <Switch checked={notif[k + 'Email']} onChange={v => updateNotif(k + 'Email', v)} />
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'center' }}>
                      <Switch checked={notif[k + 'Push']} onChange={v => updateNotif(k + 'Push', v)} />
                    </div>
                  </div>
                ))}
              </FormSection>

              <FormSection title="Email digests">
                <FormRow label="Weekly digest" hint="Summary of what happened across your projects + AI recommendations."><Switch checked={notif.weeklyDigest} onChange={v => updateNotif('weeklyDigest', v)} /></FormRow>
                <FormRow label="Product updates & tips"><Switch checked={notif.marketingEmail} onChange={v => updateNotif('marketingEmail', v)} /></FormRow>
              </FormSection>
            </>
          )}

          {/* ── Appearance ───────────────────────────────────── */}
          {section === 'appearance' && (
            <FormSection title="Appearance" description="How DevHunt looks on your device.">
              <FormRow label="Theme" hint="System will follow your OS preference.">
                <div style={{ display: 'flex', gap: 8 }}>
                  {['light', 'dark'].map(m => (
                    <button key={m} onClick={() => setTheme && setTheme(m)}
                      className="btn"
                      style={{
                        padding: 12, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                        borderColor: theme === m ? 'var(--accent)' : 'var(--border)',
                        background: theme === m ? 'var(--accent-weak)' : 'var(--bg-elev)',
                        minWidth: 100,
                      }}>
                      <div style={{
                        width: 64, height: 40, borderRadius: 6,
                        background: m === 'dark' ? '#0a0a0b' : '#fafafa',
                        border: '1px solid var(--border)',
                        position: 'relative', overflow: 'hidden',
                      }}>
                        <div style={{ position: 'absolute', top: 6, left: 6, width: 12, height: 4, background: m === 'dark' ? '#3a3a3f' : '#d1d1d6', borderRadius: 2 }} />
                        <div style={{ position: 'absolute', top: 14, left: 6, width: 20, height: 3, background: m === 'dark' ? '#2a2a2f' : '#e1e1e5', borderRadius: 2 }} />
                        <div style={{ position: 'absolute', top: 20, left: 6, width: 16, height: 3, background: m === 'dark' ? '#2a2a2f' : '#e1e1e5', borderRadius: 2 }} />
                      </div>
                      <span style={{ textTransform: 'capitalize', fontSize: 12 }}>{m}</span>
                    </button>
                  ))}
                </div>
              </FormRow>

              <FormRow label="Accent color" hint="Used for buttons, links, and highlights.">
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {[275, 240, 200, 160, 130, 60, 30, 0, 320].map(h => (
                    <button key={h} onClick={() => setTweaks && setTweaks(t => ({ ...t, accentHue: h }))}
                      style={{
                        width: 30, height: 30, borderRadius: 999, cursor: 'pointer',
                        background: `oklch(0.58 0.22 ${h})`,
                        border: `2px solid ${tweaks?.accentHue === h ? 'var(--text)' : 'transparent'}`,
                        boxShadow: tweaks?.accentHue === h ? '0 0 0 2px var(--bg)' : 'none',
                      }}
                      title={`hue ${h}°`} />
                  ))}
                </div>
              </FormRow>

              <FormRow label="Advanced" hint="Density, layout, typography, card styles.">
                <button className="btn" onClick={() => window.parent.postMessage({ type: '__activate_edit_mode' }, '*')}>
                  <Icon name="settings" size={12} /> Open Tweaks
                </button>
              </FormRow>
            </FormSection>
          )}

          {/* ── Security ─────────────────────────────────────── */}
          {section === 'security' && (
            <>
              <FormSection title="Two-factor authentication" description="Protects your account if someone gets your password.">
                <FormRow label="Authenticator app">
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span className="chip" style={{ fontSize: 11 }}>Not enabled</span>
                    <button className="btn btn-primary btn-sm">Enable 2FA</button>
                  </div>
                </FormRow>
                <FormRow label="Passkeys" hint="Sign in with biometrics on devices that support it.">
                  <button className="btn btn-sm"><Icon name="plus" size={11} /> Add passkey</button>
                </FormRow>
                <FormRow label="Backup codes"><button className="btn btn-sm">Generate codes</button></FormRow>
              </FormSection>

              <FormSection title="Sessions" description="Devices currently signed in to your account.">
                {[
                  ['MacBook Pro · Chrome', 'Berlin · now', true],
                  ['iPhone 15 · DevHunt iOS', 'Berlin · 2h ago', false],
                  ['Linux · Firefox', 'unknown · 3d ago', false],
                ].map(([name, when, current], i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '14px 0', borderBottom: i < 2 ? '1px solid var(--border)' : 'none' }}>
                    <div style={{ width: 36, height: 36, borderRadius: 8, background: 'var(--bg-subtle)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Icon name={current ? 'zap' : 'clock'} size={14} style={{ color: 'var(--text-muted)' }} />
                    </div>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontSize: 13, fontWeight: 500 }}>{name} {current && <span className="chip" style={{ fontSize: 9, marginLeft: 6, background: 'var(--accent-weak)', color: 'var(--accent)', borderColor: 'transparent' }}>Current</span>}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{when}</div>
                    </div>
                    {!current && <button className="btn btn-sm" style={{ color: 'var(--danger)' }}>Sign out</button>}
                  </div>
                ))}
                <div style={{ paddingTop: 14 }}>
                  <button className="btn btn-sm" style={{ color: 'var(--danger)' }}>Sign out all other sessions</button>
                </div>
              </FormSection>
            </>
          )}

          {/* ── Data & export ────────────────────────────────── */}
          {section === 'data' && (
            <FormSection title="Your data" description="Export or permanently delete what DevHunt stores about you.">
              <FormRow label="Export your data" hint="A zip with projects, posts, messages, and account metadata. Ready in ~10 minutes.">
                <button className="btn"><Icon name="arrowUpRight" size={12} /> Request export</button>
              </FormRow>
              <FormRow label="Download archive" hint="Machine-readable JSON of your profile + public activity.">
                <button className="btn btn-sm">Download JSON</button>
              </FormRow>
              <FormRow label="Connected integrations" hint="Services that can read or write to your DevHunt account.">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {[['GitHub', 'Read commits, open PRs'], ['Slack', 'Deliver notifications']].map(([n, d]) => (
                    <div key={n} className="card" style={{ padding: 12, display: 'flex', alignItems: 'center', gap: 10 }}>
                      <Icon name={n.toLowerCase()} size={16} style={{ color: 'var(--text-muted)' }} />
                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: 13, fontWeight: 500 }}>{n}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{d}</div>
                      </div>
                      <button className="btn btn-sm" style={{ color: 'var(--danger)' }}>Revoke</button>
                    </div>
                  ))}
                </div>
              </FormRow>
            </FormSection>
          )}

          {/* ── Danger zone ──────────────────────────────────── */}
          {section === 'danger' && (
            <section className="card" style={{ padding: 0, border: '1px solid var(--danger)', background: 'color-mix(in oklch, var(--danger) 5%, var(--bg-elev))' }}>
              <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--danger)' }}>
                <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--danger)' }}>Danger zone</div>
                <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 3 }}>Irreversible actions. Read the fine print.</div>
              </div>
              <div style={{ padding: '0 24px 18px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 16, padding: '16px 0', borderBottom: '1px solid var(--border)', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 500 }}>Deactivate account</div>
                    <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>Your profile disappears but data is preserved. Reactivate anytime by signing in.</div>
                  </div>
                  <button className="btn">Deactivate</button>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 16, padding: '16px 0', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--danger)' }}>Delete account permanently</div>
                    <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>Removes your profile, all posts, comments, and projects you solely own. Cannot be undone.</div>
                  </div>
                  <button className="btn" style={{ background: 'var(--danger)', color: '#fff', borderColor: 'var(--danger)' }}>
                    Delete account
                  </button>
                </div>
              </div>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ProfileEditPage, SettingsPage });
