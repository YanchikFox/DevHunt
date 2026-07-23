import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <div className="text--center">
          <Heading as="h1" className="hero__title">
            {siteConfig.title}
          </Heading>
          <p className="hero__subtitle">{siteConfig.tagline}</p>
          <p className={styles.heroDescription}>
            Complete technical documentation for the DevHunt developer collaboration platform.
            Find guides, API references, and everything you need to work with our backend and frontend.
          </p>
        </div>
      </div>
    </header>
  );
}

function StartHereSection() {
  return (
    <section className={styles.startHere}>
      <div className="container">
        <div className="text--center margin-bottom--xl">
          <Heading as="h2">Start Here</Heading>
          <p className="hero__subtitle margin-top--md">
            Essential guides to get you up and running with DevHunt
          </p>
        </div>

        <div className="row">
          <div className={clsx('col col--3')}>
            <Link className={clsx(styles.startCard, styles.quickstartCard)} to="/docs/getting-started/quickstart">
              <div className={styles.startCardIcon}>🚀</div>
              <h3>Quick Start</h3>
              <p>Get up and running in 5 minutes</p>
            </Link>
          </div>
          <div className={clsx('col col--3')}>
            <Link className={clsx(styles.startCard, styles.architectureCard)} to="/docs/architecture/overview">
              <div className={styles.startCardIcon}>🏗️</div>
              <h3>Architecture</h3>
              <p>Understand the system design</p>
            </Link>
          </div>
          <div className={clsx('col col--3')}>
            <Link className={clsx(styles.startCard, styles.backendCard)} to="/docs/backend/overview">
              <div className={styles.startCardIcon}>⚙️</div>
              <h3>Backend</h3>
              <p>.NET API and services</p>
            </Link>
          </div>
          <div className={clsx('col col--3')}>
            <Link className={clsx(styles.startCard, styles.frontendCard)} to="/docs/frontend/overview">
              <div className={styles.startCardIcon}>🎨</div>
              <h3>Frontend</h3>
              <p>React & Next.js application</p>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}

function DocPortal() {
  return (
    <section className={styles.portal}>
      <div className="container">
        <div className="text--center margin-bottom--xl">
          <Heading as="h2">Explore Documentation</Heading>
          <p className="hero__subtitle margin-top--md">
            Dive deep into specific areas of the DevHunt platform
          </p>
        </div>

        <div className="row">
          {/* Backend Documentation Card */}
          <div className={clsx('col col--6')}>
            <div className={clsx('card card--full-height', styles.docCard, styles.backendCard)}>
              <div className="card__header">
                <Heading as="h3" className={styles.cardTitle}>
                  ⚙️ Backend Documentation
                </Heading>
              </div>
              <div className="card__body">
                <ul className={styles.featureList}>
                  <li><strong>Code Architecture</strong> - How the backend is structured</li>
                  <li><strong>API Reference</strong> - Complete .NET services documentation</li>
                  <li><strong>How to Run Locally</strong> - Development setup and debugging</li>
                  <li><strong>Common Issues</strong> - Troubleshooting and FAQ</li>
                </ul>
              </div>
              <div className="card__footer">
                <Link
                  className="button button--primary button--block button--lg"
                  to="/docs/backend/overview">
                  Explore Backend →
                </Link>
              </div>
            </div>
          </div>

          {/* Frontend Documentation Card */}
          <div className={clsx('col col--6')}>
            <div className={clsx('card card--full-height', styles.docCard, styles.frontendCard)}>
              <div className="card__header">
                <Heading as="h3" className={styles.cardTitle}>
                  🎨 Frontend Documentation
                </Heading>
              </div>
              <div className="card__body">
                <ul className={styles.featureList}>
                  <li><strong>Code Architecture</strong> - How the frontend is organized</li>
                  <li><strong>Components & Hooks</strong> - React components and custom hooks</li>
                  <li><strong>How to Run Locally</strong> - Development setup and debugging</li>
                  <li><strong>Common Issues</strong> - Troubleshooting and FAQ</li>
                </ul>
              </div>
              <div className="card__footer">
                <Link
                  className="button button--primary button--block button--lg"
                  to="/docs/frontend/overview">
                  Explore Frontend →
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function AdditionalResources() {
  return (
    <section className={styles.resources}>
      <div className="container">
        <div className="text--center margin-bottom--lg">
          <Heading as="h3">Additional Resources</Heading>
        </div>

        <div className="row">
          <div className={clsx('col col--4')}>
            <Link className={styles.resourceCard} to="/docs/api/intro">
              <div className={styles.resourceIcon}>🔗</div>
              <h4>API Reference</h4>
              <p>Interactive REST API documentation</p>
            </Link>
          </div>
          <div className={clsx('col col--4')}>
            <Link className={styles.resourceCard} to="/docs/architecture/security">
              <div className={styles.resourceIcon}>🔒</div>
              <h4>Security</h4>
              <p>Security best practices and guidelines</p>
            </Link>
          </div>
          <div className={clsx('col col--4')}>
            <Link className={styles.resourceCard} to="/docs/operations/overview">
              <div className={styles.resourceIcon}>🚀</div>
              <h4>Operations</h4>
              <p>Deployment, incident response, and runbooks</p>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title}`}
      description="Complete technical documentation for DevHunt platform - Frontend, Backend, API Reference, and Guides">
      <HomepageHeader />
      <main>
        <StartHereSection />
        <DocPortal />
        <AdditionalResources />
      </main>
    </Layout>
  );
}
