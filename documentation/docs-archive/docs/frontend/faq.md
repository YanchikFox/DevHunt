---
sidebar_position: 4
title: Frontend FAQ
description: Frequently asked questions about DevHunt frontend development
---

import Link from '@docusaurus/Link';

<div style={{textAlign: 'center', marginBottom: '2rem'}}>
  <Link className="button button--secondary button--lg" to="/docs/frontend/overview">
    ← Back to Frontend Overview
  </Link>
  <Link className="button button--primary button--lg margin-left--md" to="/">
    🏠 Home
  </Link>
</div>

# Frontend FAQ

Frequently asked questions about the DevHunt frontend architecture, development, and deployment.

## ⚛️ React & Next.js Questions

### Q: Why Next.js instead of Create React App?

**A:** Next.js provides several advantages:

- **Server-Side Rendering (SSR)** - Better SEO and initial page load
- **Static Generation** - Pre-built pages for better performance
- **File-based Routing** - Intuitive page structure
- **Built-in Optimizations** - Image optimization, code splitting
- **API Routes** - Backend functionality in the same project

### Q: How do I add a new page?

**A:** Create a new file in the appropriate directory:

```typescript
// src/app/dashboard/page.tsx (App Router)
export default function Dashboard() {
  return <div>Dashboard Content</div>;
}

// With metadata
export const metadata = {
  title: 'Dashboard',
  description: 'User dashboard page',
};
```

For dynamic routes:
```typescript
// src/app/projects/[id]/page.tsx
interface Props {
  params: { id: string };
}

export default function ProjectPage({ params }: Props) {
  return <div>Project {params.id}</div>;
}
```

### Q: How do I handle forms and validation?

**A:** Use React Hook Form with Zod validation:

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const schema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
});

function LoginForm() {
  const { register, handleSubmit, formState: { errors } } = useForm({
    resolver: zodResolver(schema)
  });

  const onSubmit = (data) => {
    // Handle form submission
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <input {...register('email')} />
      {errors.email && <span>{errors.email.message}</span>}
      <button type="submit">Login</button>
    </form>
  );
}
```

## 🎨 Styling Questions

### Q: How do I add custom styles?

**A:** Use Tailwind CSS classes or CSS modules:

```typescript
// Tailwind (preferred)
function Button({ variant = 'primary' }) {
  return (
    <button className={`
      px-4 py-2 rounded-md font-medium
      ${variant === 'primary' ? 'bg-blue-600 text-white' : 'bg-gray-200 text-gray-800'}
    `}>
      Click me
    </button>
  );
}

// CSS Modules
// styles.module.css
.button {
  padding: 0.5rem 1rem;
  border-radius: 0.375rem;
}

// component.tsx
import styles from './component.module.css';

function Component() {
  return <button className={styles.button}>Click me</button>;
}
```

### Q: How do I handle dark mode?

**A:** Use Tailwind's dark mode classes:

```typescript
function ThemeToggle() {
  const [isDark, setIsDark] = useState(false);

  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDark);
  }, [isDark]);

  return (
    <button
      onClick={() => setIsDark(!isDark)}
      className="bg-gray-200 dark:bg-gray-800 text-gray-800 dark:text-gray-200"
    >
      Toggle Theme
    </button>
  );
}
```

Configure Tailwind for dark mode:
```javascript
// tailwind.config.js
module.exports = {
  darkMode: 'class', // or 'media' for system preference
};
```

## 🌐 API & Data Fetching Questions

### Q: How do I fetch data from the API?

**A:** Use React Query (TanStack Query) for data fetching:

```typescript
import { useQuery } from '@tanstack/react-query';

function useProjects() {
  return useQuery({
    queryKey: ['projects'],
    queryFn: async () => {
      const response = await fetch('/api/projects');
      return response.json();
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

function ProjectsList() {
  const { data, isLoading, error } = useProjects();

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error loading projects</div>;

  return (
    <ul>
      {data?.map(project => (
        <li key={project.id}>{project.name}</li>
      ))}
    </ul>
  );
}
```

### Q: How do I handle authentication?

**A:** Use a custom hook for authentication state:

```typescript
// hooks/useAuth.ts
import { createContext, useContext, useEffect, useState } from 'react';

interface User {
  id: string;
  email: string;
  name: string;
}

interface AuthContextType {
  user: User | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check for existing session
    checkAuth();
  }, []);

  const checkAuth = async () => {
    try {
      const response = await fetch('/api/auth/me');
      if (response.ok) {
        const userData = await response.json();
        setUser(userData);
      }
    } catch (error) {
      console.error('Auth check failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (email: string, password: string) => {
    const response = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) throw new Error('Login failed');

    const userData = await response.json();
    setUser(userData);
    localStorage.setItem('token', userData.token);
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem('token');
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, isLoading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
```

## 🔴 Real-time Features Questions

### Q: How do I implement real-time chat?

**A:** Use SignalR with React hooks:

```typescript
import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

function useChat(roomId: string) {
  const [messages, setMessages] = useState([]);
  const [connection, setConnection] = useState(null);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('/chatHub')
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          connection.invoke('JoinRoom', roomId);
        })
        .catch(err => console.error('Connection failed:', err));

      connection.on('ReceiveMessage', (message) => {
        setMessages(prev => [...prev, message]);
      });

      return () => {
        connection.stop();
      };
    }
  }, [connection, roomId]);

  const sendMessage = async (content: string) => {
    if (connection) {
      await connection.invoke('SendMessage', roomId, content);
    }
  };

  return { messages, sendMessage };
}
```

## 🧪 Testing Questions

### Q: What testing frameworks do you use?

**A:** We use multiple testing approaches:

- **Vitest** for unit tests and component tests
- **Playwright** for end-to-end tests
- **React Testing Library** for component testing

### Q: How do I test a component?

**A:** Use React Testing Library with Vitest:

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { expect, test } from 'vitest';
import { Button } from './Button';

test('Button calls onClick when clicked', async () => {
  const handleClick = vi.fn();
  render(<Button onClick={handleClick}>Click me</Button>);

  const button = screen.getByRole('button', { name: /click me/i });
  fireEvent.click(button);

  expect(handleClick).toHaveBeenCalledTimes(1);
});
```

### Q: How do I test API calls?

**A:** Mock API calls using Vitest:

```typescript
import { rest } from 'msw';
import { setupServer } from 'msw/node';

const server = setupServer(
  rest.get('/api/projects', (req, res, ctx) => {
    return res(ctx.json([{ id: 1, name: 'Test Project' }]));
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

test('fetches projects', async () => {
  const { result } = renderHook(() => useProjects(), {
    wrapper: QueryClientProvider,
  });

  await waitFor(() => {
    expect(result.current.data).toEqual([{ id: 1, name: 'Test Project' }]);
  });
});
```

## 🚀 Performance Questions

### Q: How do I optimize component re-renders?

**A:** Use React.memo and useMemo:

```typescript
import { memo, useMemo } from 'react';

const UserCard = memo(({ user, onSelect }) => {
  console.log('UserCard rendered'); // Should not log on every render

  return (
    <div onClick={() => onSelect(user.id)}>
      {user.name}
    </div>
  );
});

function UserList({ users, filter }) {
  const filteredUsers = useMemo(() => {
    return users.filter(user =>
      user.name.toLowerCase().includes(filter.toLowerCase())
    );
  }, [users, filter]);

  return (
    <div>
      {filteredUsers.map(user => (
        <UserCard key={user.id} user={user} onSelect={handleSelect} />
      ))}
    </div>
  );
}
```

### Q: How do I optimize images?

**A:** Use Next.js Image component:

```typescript
import Image from 'next/image';

function OptimizedImage() {
  return (
    <Image
      src="/hero.jpg"
      alt="Hero image"
      width={800}
      height={600}
      priority // Load immediately (above the fold)
      placeholder="blur" // Show blur placeholder
      sizes="(max-width: 768px) 100vw, 50vw" // Responsive sizes
    />
  );
}
```

## 📱 Mobile & Responsive Questions

### Q: How do I make components responsive?

**A:** Use Tailwind responsive utilities:

```typescript
function ResponsiveCard() {
  return (
    <div className="
      grid grid-cols-1
      md:grid-cols-2
      lg:grid-cols-3
      gap-4
    ">
      {/* Cards will stack on mobile, 2 columns on tablet, 3 on desktop */}
    </div>
  );
}
```

### Q: How do I handle touch interactions?

**A:** Use appropriate event handlers and sizing:

```typescript
function TouchButton() {
  const [isPressed, setIsPressed] = useState(false);

  return (
    <button
      className={`
        min-h-[44px] min-w-[44px] // Minimum touch target size
        ${isPressed ? 'scale-95' : 'scale-100'}
        transition-transform
      `}
      onTouchStart={() => setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
    >
      Tap me
    </button>
  );
}
```

## 🌐 Internationalization Questions

### Q: How do I add translations?

**A:** Use next-intl:

```typescript
// messages/en.json
{
  "nav": {
    "home": "Home",
    "projects": "Projects"
  }
}

// messages/ru.json
{
  "nav": {
    "home": "Главная",
    "projects": "Проекты"
  }
}

// component.tsx
import { useTranslations } from 'next-intl';

function Navigation() {
  const t = useTranslations('nav');

  return (
    <nav>
      <Link href="/">{t('home')}</Link>
      <Link href="/projects">{t('projects')}</Link>
    </nav>
  );
}
```

### Q: How do I change languages?

**A:** Use the language switcher:

```typescript
'use client';

import { useRouter, usePathname } from 'next/navigation';
import { useLocale } from 'next-intl';

function LanguageSwitcher() {
  const router = useRouter();
  const pathname = usePathname();
  const locale = useLocale();

  const switchLanguage = (newLocale: string) => {
    router.push(pathname.replace(`/${locale}`, `/${newLocale}`));
  };

  return (
    <select
      value={locale}
      onChange={(e) => switchLanguage(e.target.value)}
    >
      <option value="en">English</option>
      <option value="ru">Русский</option>
    </select>
  );
}
```

## 🚀 Deployment Questions

### Q: How do you deploy the frontend?

**A:** We use containerized deployment:

1. **Build Docker image** with multi-stage build
2. **Push to container registry** (Docker Hub, ECR, etc.)
3. **Deploy to Kubernetes** or cloud platforms
4. **Configure CDN** for static assets
5. **Set up monitoring** and error tracking

### Q: How do I configure environment variables?

**A:** Use different .env files:

```bash
# .env.local (development)
NEXT_PUBLIC_API_URL=http://localhost:7002

# .env.production (production)
NEXT_PUBLIC_API_URL=https://api.devhunt.com
```

Access in code:
```typescript
const apiUrl = process.env.NEXT_PUBLIC_API_URL;
```

### Q: How do I handle SEO?

**A:** Use Next.js metadata API:

```typescript
// page.tsx
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Project Dashboard | DevHunt',
  description: 'Manage your development projects on DevHunt',
  openGraph: {
    title: 'Project Dashboard',
    description: 'Manage your development projects',
    images: ['/og-image.png'],
  },
};

export default function Dashboard() {
  return <div>Dashboard content</div>;
}
```

## 🐛 Debugging Questions

### Q: How do I debug hydration issues?

**A:** Check for server/client mismatches:

```typescript
'use client';

import { useEffect, useState } from 'react';

function ClientOnly({ children }) {
  const [hasMounted, setHasMounted] = useState(false);

  useEffect(() => {
    setHasMounted(true);
  }, []);

  if (!hasMounted) {
    return null; // Or loading placeholder
  }

  return children;
}
```

### Q: How do I debug performance issues?

**A:** Use React DevTools Profiler:

```typescript
import { Profiler } from 'react';

function onRender(id, phase, actualDuration, baseDuration, startTime, commitTime) {
  console.log(`${id} took ${actualDuration}ms to render`);
}

function App() {
  return (
    <Profiler id="App" onRender={onRender}>
      <MyComponent />
    </Profiler>
  );
}
```

## 📚 Learning Resources

### Recommended Libraries
- **Next.js Documentation** - Official docs with examples
- **React Documentation** - Comprehensive React guides
- **Tailwind CSS** - Utility-first CSS framework
- **TypeScript Handbook** - Complete TypeScript reference

### Online Resources
- **[Next.js Learn](https://nextjs.org/learn)** - Interactive Next.js tutorial
- **[React DevTools](https://react.dev/learn/react-developer-tools)** - Component inspection
- **[Tailwind Play](https://play.tailwindcss.com/)** - Try Tailwind online

### Community
- **Next.js Discord** - Community support
- **React subreddit** - r/react
- **DevHunt GitHub Discussions** - Project-specific questions

## ❓ Still Have Questions?

If you can't find the answer here:

1. **Check the [Troubleshooting Guide](/docs/frontend/troubleshooting)**
2. **Search existing GitHub issues**
3. **Ask in our community Discord/Slack**
4. **Create a new GitHub issue**

Remember to include:
- Your environment (Node.js version, OS, etc.)
- Steps to reproduce the issue
- Expected vs actual behavior
- Any error messages or console logs