import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import Heading from '@theme/Heading';

import styles from './styles.module.css';

interface FeatureItem {
  title: string;
  description: ReactNode;
  link: string;
  icon: string;
}

const FeatureList: FeatureItem[] = [
  {
    title: '🎨 Frontend Documentation',
    description: (
      <>
        Полная документация по Next.js/React фронтенду: компоненты, хуки, 
        TypeScript API reference, архитектура и лучшие практики разработки.
      </>
    ),
    link: '/docs/frontend/intro',
    icon: '🎨',
  },
  {
    title: '⚙️ Backend Documentation',
    description: (
      <>
        Документация по .NET бэкенду: сервисы, API endpoints, модели базы данных, 
        OpenAPI спецификации и C# API reference.
      </>
    ),
    link: '/docs/backend/intro',
    icon: '⚙️',
  },
  {
    title: '📚 Guides & Architecture',
    description: (
      <>
        Руководства по началу работы, архитектуре системы, deployment, 
        operations и best practices для разработчиков.
      </>
    ),
    link: '/docs/intro',
    icon: '📚',
  },
];

function Feature({title, description, link, icon}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className={clsx('card', 'card--full-height')}>
        <div className="card__header">
          <Heading as="h3">{icon} {title}</Heading>
        </div>
        <div className="card__body">
          <p>{description}</p>
        </div>
        <div className="card__footer">
          <Link
            className="button button--primary button--block"
            to={link}>
            Перейти →
          </Link>
        </div>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
