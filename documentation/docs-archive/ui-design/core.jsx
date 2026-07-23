// DevHunt — shared state, mock data, icons, tiny helpers

// ─── Icons (stroke-based, 18px) ─────────────────────────────────────
const Icon = ({ name, size = 16, ...rest }) => {
  const paths = {
    search: 'M21 21l-4.3-4.3M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16z',
    bell: 'M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10 21a2 2 0 0 0 4 0',
    home: 'M3 10.5L12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-7h-6v7H4a1 1 0 0 1-1-1z',
    folder: 'M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z',
    board: 'M4 4h6v16H4zM14 4h6v10h-6z',
    users: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
    chat: 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
    sparkle: 'M12 3l1.9 5.6L19 10l-5.1 1.4L12 17l-1.9-5.6L5 10l5.1-1.4zM19 3l.5 1.5L21 5l-1.5.5L19 7l-.5-1.5L17 5l1.5-.5zM5 15l.5 1.5L7 17l-1.5.5L5 19l-.5-1.5L3 17l1.5-.5z',
    community: 'M2 12s3-7 10-7 10 7 10 7-3 7-10 7S2 12 2 12zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z',
    shield: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
    user: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8',
    plus: 'M12 5v14M5 12h14',
    check: 'M20 6L9 17l-5-5',
    x: 'M18 6L6 18M6 6l12 12',
    menu: 'M3 6h18M3 12h18M3 18h18',
    chevronDown: 'M6 9l6 6 6-6',
    chevronRight: 'M9 6l6 6-6 6',
    chevronLeft: 'M15 6l-6 6 6 6',
    arrowRight: 'M5 12h14M13 5l7 7-7 7',
    arrowUpRight: 'M7 17L17 7M7 7h10v10',
    star: 'M12 2l3.1 6.3 7 1-5 4.9 1.2 6.9L12 17.8l-6.3 3.3L7 14.2 2 9.3l7-1z',
    heart: 'M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.6l-1-1a5.5 5.5 0 0 0-7.8 7.8l1 1L12 21l7.8-7.8 1-1a5.5 5.5 0 0 0 0-7.8z',
    comment: 'M21 12a8 8 0 0 1-11.9 7L4 20l1-5A8 8 0 1 1 21 12z',
    github: 'M9 19c-5 1.5-5-2.5-7-3m14 6v-4c0-1 .1-1.4-.5-2 3-.3 6-1.5 6-6.5A5 5 0 0 0 20 5c.2-.5.5-2-.2-4 0 0-1.6-.5-5 2-1.6-.4-3-.4-4.5 0-3.5-2.5-5-2-5-2-.7 2-.4 3.5-.2 4A5 5 0 0 0 3.5 9.5c0 5 3 6.2 6 6.5-.6.6-.6 1.2-.5 2v4',
    link: 'M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.5 1.5M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7L12 19',
    settings: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33h0a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82v0a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z',
    logout: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9',
    send: 'M22 2L11 13M22 2l-7 20-4-9-9-4z',
    paperclip: 'M21.4 11L12 20.4a6 6 0 0 1-8.5-8.5l9.4-9.4a4 4 0 0 1 5.7 5.7L9.2 17.6a2 2 0 0 1-2.8-2.8l8.5-8.5',
    zap: 'M13 2L3 14h9l-1 8 10-12h-9z',
    clock: 'M12 6v6l4 2M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z',
    calendar: 'M3 10h18M8 2v4M16 2v4M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z',
    flag: 'M4 21V4h13l-2 4 2 4H4M4 21h0',
    filter: 'M22 3H2l8 9.5V19l4 2v-8.5z',
    moon: 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z',
    sun: 'M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10zM12 1v2M12 21v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M1 12h2M21 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4',
    trophy: 'M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0zM17 5h3a0 0 0 0 1 0 0v3a4 4 0 0 1-4 4M7 5H4a0 0 0 0 0 0 0v3a4 4 0 0 0 4 4',
    edit: 'M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7M18.5 2.5a2.1 2.1 0 0 1 3 3L12 15l-4 1 1-4z',
    trash: 'M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6',
    lock: 'M5 11h14v10H5zM8 11V7a4 4 0 0 1 8 0v4',
    slack: 'M10 13a2 2 0 1 1-4 0 2 2 0 0 1 4 0V3M11 14a2 2 0 1 1 0-4h10M13 13a2 2 0 1 1 4 0 2 2 0 0 1-4 0V21M14 11a2 2 0 1 1 0 4H3',
    moreH: 'M5 12h.01M12 12h.01M19 12h.01',
    moreV: 'M12 5h.01M12 12h.01M12 19h.01',
    grid: 'M3 3h7v7H3zM14 3h7v7h-7zM14 14h7v7h-7zM3 14h7v7H3z',
    list: 'M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01',
    eye: 'M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z',
    logo: 'M4 20L12 4l8 16M8 14h8',
    hash: 'M4 9h16M4 15h16M10 3L8 21M16 3l-2 18',
  };
  const d = paths[name];
  if (!d) return null;
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" {...rest}>
      {d.split('M').filter(Boolean).map((seg, i) => (
        <path key={i} d={'M' + seg} />
      ))}
    </svg>
  );
};

// ─── Avatar (initials with deterministic hue) ────────────────────
const Avatar = ({ name = '?', size = 28, src = null }) => {
  const initials = name.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  const hue = [...name].reduce((a, c) => a + c.charCodeAt(0), 0) % 360;
  const bg = `linear-gradient(135deg, oklch(0.72 0.18 ${hue}), oklch(0.62 0.20 ${(hue + 40) % 360}))`;
  return (
    <div className="avatar" style={{
      width: size, height: size, fontSize: size * 0.38,
      background: bg, color: '#fff',
    }}>
      {src ? <img src={src} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : initials}
    </div>
  );
};

// Deterministic hue from a string, for project colors
const hueFor = (s = '') => {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) % 360;
  return (h * 37) % 360;
};
const projectColor = (name, shade = 'mid') => {
  const h = hueFor(name || '');
  const lc = { mid: [0.62, 0.20], light: [0.92, 0.06], soft: [0.86, 0.10], edge: [0.55, 0.22] }[shade] || [0.62, 0.20];
  return `oklch(${lc[0]} ${lc[1]} ${h})`;
};

// ─── Mock Data ───────────────────────────────────────────────────
const MOCK = {
  me: {
    id: 1, name: 'Alex Kurenkov', username: 'alexk',
    title: 'Full-stack dev · TypeScript, Go',
    bio: 'Building tools for developer workflows. Previously at Stripe. Open to frontend-heavy collabs.',
    location: 'Berlin', github: 'alexk', followers: 248, following: 91,
  },
  users: [
    { id: 2, name: 'Mira Chen', title: 'ML Engineer', skills: ['Python', 'PyTorch', 'NLP'] },
    { id: 3, name: 'Tomás Vega', title: 'Backend · Rust', skills: ['Rust', 'Postgres', 'gRPC'] },
    { id: 4, name: 'Priya Shah', title: 'Product Designer', skills: ['Figma', 'Design systems'] },
    { id: 5, name: 'Jon Okafor', title: 'DevOps', skills: ['K8s', 'Terraform', 'AWS'] },
    { id: 6, name: 'Emma Lindqvist', title: 'iOS Developer', skills: ['Swift', 'SwiftUI'] },
    { id: 7, name: 'Raj Patel', title: 'Frontend · React', skills: ['React', 'Next.js', 'Tailwind'] },
  ],
  projects: [
    {
      id: 'p1', name: 'Helix', tag: 'helix',
      tagline: 'Postgres query profiler for production debugging',
      stage: 'active', visibility: 'public', stars: 812,
      stack: ['Rust', 'Postgres', 'WASM', 'TypeScript'],
      members: 4, openRoles: 1, updated: '2h ago',
      description: 'Helix captures live query plans and surfaces regressions without agents. Built as a developer tool that drops into any Postgres-backed app.',
    },
    {
      id: 'p2', name: 'Paperstack', tag: 'paperstack',
      tagline: 'Collaborative research notebook with citation graph',
      stage: 'recruiting', visibility: 'public', stars: 204,
      stack: ['TypeScript', 'Next.js', 'Neo4j'],
      members: 2, openRoles: 3, updated: '1d ago',
      description: 'Paperstack turns reading sessions into a shared graph. Think Obsidian for lab groups.',
    },
    {
      id: 'p3', name: 'Drift', tag: 'drift',
      tagline: 'Real-time collab canvas for system design',
      stage: 'active', visibility: 'public', stars: 1420,
      stack: ['React', 'WebRTC', 'CRDT'],
      members: 6, openRoles: 0, updated: '5h ago',
      description: 'Drift is a whiteboard purpose-built for system design interviews and architecture reviews.',
    },
    {
      id: 'p4', name: 'Moss', tag: 'moss',
      tagline: 'Self-hosted analytics for indie products',
      stage: 'beta', visibility: 'public', stars: 340,
      stack: ['Go', 'ClickHouse', 'Svelte'],
      members: 3, openRoles: 2, updated: '3d ago',
      description: 'Lightweight, GDPR-friendly analytics. One binary, no cookies.',
    },
    {
      id: 'p5', name: 'Tidepool', tag: 'tidepool',
      tagline: 'LLM eval harness for fine-tuned models',
      stage: 'idea', visibility: 'public', stars: 18,
      stack: ['Python', 'FastAPI'],
      members: 1, openRoles: 4, updated: '1w ago',
      description: 'Regression-test your fine-tunes against a growing library of evals.',
    },
  ],
  tasks: [
    { id: 't1', col: 'todo', title: 'Wire up pg_stat_statements ingest', priority: 'high', assignee: 'Tomás Vega', tags: ['backend'] },
    { id: 't2', col: 'todo', title: 'Write docs for self-hosted mode', priority: 'low', assignee: 'Alex Kurenkov', tags: ['docs'] },
    { id: 't3', col: 'todo', title: 'Design empty-state for query list', priority: 'medium', assignee: 'Priya Shah', tags: ['design'] },
    { id: 't4', col: 'progress', title: 'Build plan-diff visualisation', priority: 'high', assignee: 'Alex Kurenkov', tags: ['frontend'] },
    { id: 't5', col: 'progress', title: 'Investigate regression in explain parser', priority: 'urgent', assignee: 'Tomás Vega', tags: ['bug'] },
    { id: 't6', col: 'review', title: 'CLI flags for daemon mode', priority: 'medium', assignee: 'Jon Okafor', tags: ['cli'] },
    { id: 't7', col: 'done', title: 'Set up CI on tagged releases', priority: 'medium', assignee: 'Jon Okafor', tags: ['devops'] },
    { id: 't8', col: 'done', title: 'Initial landing page copy', priority: 'low', assignee: 'Priya Shah', tags: ['marketing'] },
    { id: 't9', col: 'done', title: 'Pick name + buy domain', priority: 'low', assignee: 'Alex Kurenkov', tags: [] },
  ],
  columns: [
    { id: 'todo', name: 'To Do', limit: null },
    { id: 'progress', name: 'In Progress', limit: 3 },
    { id: 'review', name: 'In Review', limit: 2 },
    { id: 'done', name: 'Done', limit: null },
  ],
  notifications: [
    { id: 'n1', kind: 'invite', text: 'Mira Chen invited you to Paperstack', time: '12m', unread: true },
    { id: 'n2', kind: 'mention', text: 'Tomás mentioned you in "Investigate regression in explain parser"', time: '1h', unread: true },
    { id: 'n3', kind: 'review', text: 'New review on Helix by @sofie · 5 stars', time: '3h', unread: true },
    { id: 'n4', kind: 'badge', text: 'You earned the First Release badge', time: '1d', unread: false },
    { id: 'n5', kind: 'message', text: 'Priya: "pushed a new iteration of the empty state"', time: '1d', unread: false },
    { id: 'n6', kind: 'match', text: 'AI: 3 new devs match your open Rust role', time: '2d', unread: false },
  ],
  feed: [
    { id: 'f1', author: 'Mira Chen', project: 'Paperstack', text: 'Just shipped the citation-graph rewrite. 4× faster on 10k-node notebooks. Looking for 2 more collaborators for the iOS companion — DM me.', time: '1h', likes: 38, comments: 7, tag: 'update' },
    { id: 'f2', author: 'Alex Kurenkov', project: 'Helix', text: 'Small milestone — Helix v0.4 is live. EXPLAIN plan diffs now render inline. Thanks everyone who tested the beta.', time: '3h', likes: 142, comments: 24, tag: 'release' },
    { id: 'f3', author: 'Emma Lindqvist', text: 'Open question: is anyone running SwiftData in a shipping app? Hitting weird migration edge cases. Happy to pair-debug.', time: '5h', likes: 18, comments: 12, tag: 'question' },
    { id: 'f4', author: 'Tomás Vega', project: 'Helix', text: 'Preview of the plan-diff UI — pulling this together with @alexk. Feedback welcome before we merge it.', time: '8h', likes: 94, comments: 16, tag: 'preview' },
  ],
  badges: [
    { code: 'first-ship', name: 'First Release', earned: true, desc: 'Shipped your first project to Release stage' },
    { code: 'committed', name: 'Committed', earned: true, desc: '30 days of activity in a row' },
    { code: 'mentor', name: 'Mentor', earned: true, desc: 'Reviewed 10 projects from other teams' },
    { code: 'matchmaker', name: 'Matchmaker', earned: false, progress: 0.6, desc: 'Invite 5 users who join a project' },
    { code: 'open-source', name: 'Open Source', earned: false, progress: 0.3, desc: 'Publish 3 projects under a permissive license' },
    { code: 'one-k-stars', name: '1k Stars', earned: false, progress: 0.8, desc: 'Accumulate 1,000 stars across projects' },
  ],
  chats: [
    { id: 'c1', name: 'Helix · core', kind: 'project', last: 'Tomás: pushed the parser fix', unread: 2, time: '12m' },
    { id: 'c2', name: 'Mira Chen', kind: 'direct', last: 'Can we sync tomorrow?', unread: 1, time: '1h' },
    { id: 'c3', name: 'Paperstack · design', kind: 'project', last: 'Priya: latest mocks ↑', unread: 0, time: '3h' },
    { id: 'c4', name: 'Jon Okafor', kind: 'direct', last: 'CI is green again', unread: 0, time: '1d' },
    { id: 'c5', name: 'Drift · founders', kind: 'project', last: 'voice note 0:42', unread: 0, time: '2d' },
  ],
  messages: {
    c1: [
      { id: 'm1', from: 'Tomás Vega', text: 'Pushed the parser fix. Mind pulling and re-running the regression suite?', time: '12:04' },
      { id: 'm2', from: 'Alex Kurenkov', text: 'On it. Give me 5.', time: '12:05', me: true },
      { id: 'm3', from: 'Tomás Vega', text: 'Also — noticed the EXPLAIN output for CTEs still drops the sort node. Filed as issue #142.', time: '12:07' },
      { id: 'm4', from: 'Priya Shah', text: 'I added the empty-state mock to the Figma. Will push to the doc.', time: '12:11' },
      { id: 'm5', from: 'Alex Kurenkov', text: 'Regression suite is green ✓. Merging.', time: '12:18', me: true },
    ],
  },
  reports: [
    { id: 'r1', target: 'Project · Anonvote', reason: 'Spam', reporter: 'multiple', date: '2h ago', severity: 'med' },
    { id: 'r2', target: 'Comment by @user_294', reason: 'Harassment', reporter: 'sofie', date: '4h ago', severity: 'high' },
    { id: 'r3', target: 'Review on Moss', reason: 'Off-topic', reporter: 'jon', date: '1d', severity: 'low' },
    { id: 'r4', target: 'User · @grindr4cash', reason: 'Spam / bot', reporter: 'system', date: '1d', severity: 'high' },
  ],
};

// ─── Small helpers ───────────────────────────────────────────────
const useLocalState = (key, initial) => {
  const [v, setV] = React.useState(() => {
    try { const s = localStorage.getItem(key); return s ? JSON.parse(s) : initial; } catch { return initial; }
  });
  React.useEffect(() => {
    try { localStorage.setItem(key, JSON.stringify(v)); } catch {}
  }, [key, v]);
  return [v, setV];
};

const cls = (...xs) => xs.filter(Boolean).join(' ');

Object.assign(window, { Icon, Avatar, MOCK, useLocalState, cls });
