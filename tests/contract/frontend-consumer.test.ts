// Example Consumer Contract Test
// This file demonstrates how to set up Pact consumer tests for the frontend

import { PactV4, MatchersV3 } from '@pact-foundation/pact';
import path from 'path';

const { like, eachLike, regex, integer, string, boolean, datetime } = MatchersV3;

// Create a new Pact instance
const provider = new PactV4({
  consumer: 'DevHunt-Frontend',
  provider: 'DevHunt-CoreApi',
  dir: path.resolve(process.cwd(), 'pacts'),
  logLevel: 'warn',
});

describe('Projects API Contract', () => {
  describe('GET /api/v1/projects', () => {
    it('returns a list of projects', async () => {
      // Define the expected interaction
      await provider
        .addInteraction()
        .given('projects exist')
        .uponReceiving('a request for all projects')
        .withRequest({
          method: 'GET',
          path: '/api/v1/projects',
          query: { page: '1', limit: '10' },
          headers: {
            Accept: 'application/json',
          },
        })
        .willRespondWith({
          status: 200,
          headers: {
            'Content-Type': 'application/json',
          },
          body: {
            data: eachLike({
              id: integer(1),
              name: string('Example Project'),
              description: string('A sample project description'),
              status: regex('active|inactive|archived', 'active'),
              createdAt: datetime("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"),
              owner: like({
                id: integer(1),
                username: string('johndoe'),
                avatarUrl: string('https://example.com/avatar.png'),
              }),
              techStack: eachLike({
                id: integer(1),
                name: string('TypeScript'),
              }),
            }),
            pagination: like({
              page: integer(1),
              limit: integer(10),
              total: integer(100),
              totalPages: integer(10),
            }),
          },
        })
        .executeTest(async (mockServer) => {
          // Make the actual API call to the mock server
          const response = await fetch(
            `${mockServer.url}/api/v1/projects?page=1&limit=10`,
            {
              headers: { Accept: 'application/json' },
            }
          );

          expect(response.status).toBe(200);
          const data = await response.json();
          expect(data.data).toBeDefined();
          expect(data.pagination).toBeDefined();
        });
    });
  });

  describe('GET /api/v1/projects/:id', () => {
    it('returns a single project', async () => {
      await provider
        .addInteraction()
        .given('project with id 1 exists')
        .uponReceiving('a request for project 1')
        .withRequest({
          method: 'GET',
          path: '/api/v1/projects/1',
          headers: {
            Accept: 'application/json',
          },
        })
        .willRespondWith({
          status: 200,
          headers: {
            'Content-Type': 'application/json',
          },
          body: like({
            id: integer(1),
            name: string('Example Project'),
            description: string('A detailed project description'),
            status: string('active'),
            createdAt: datetime("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"),
            updatedAt: datetime("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"),
            owner: like({
              id: integer(1),
              username: string('johndoe'),
            }),
            members: eachLike({
              id: integer(2),
              username: string('janedoe'),
              role: string('developer'),
            }),
            techStack: eachLike({
              id: integer(1),
              name: string('TypeScript'),
              category: string('language'),
            }),
          }),
        })
        .executeTest(async (mockServer) => {
          const response = await fetch(`${mockServer.url}/api/v1/projects/1`, {
            headers: { Accept: 'application/json' },
          });

          expect(response.status).toBe(200);
          const data = await response.json();
          expect(data.id).toBe(1);
        });
    });

    it('returns 404 for non-existent project', async () => {
      await provider
        .addInteraction()
        .given('project with id 999 does not exist')
        .uponReceiving('a request for non-existent project')
        .withRequest({
          method: 'GET',
          path: '/api/v1/projects/999',
          headers: {
            Accept: 'application/json',
          },
        })
        .willRespondWith({
          status: 404,
          headers: {
            'Content-Type': 'application/json',
          },
          body: like({
            error: string('Not Found'),
            message: string('Project not found'),
          }),
        })
        .executeTest(async (mockServer) => {
          const response = await fetch(`${mockServer.url}/api/v1/projects/999`, {
            headers: { Accept: 'application/json' },
          });

          expect(response.status).toBe(404);
        });
    });
  });
});

describe('Auth API Contract', () => {
  const authProvider = new PactV4({
    consumer: 'DevHunt-Frontend',
    provider: 'DevHunt-AuthService',
    dir: path.resolve(process.cwd(), 'pacts'),
    logLevel: 'warn',
  });

  describe('POST /api/auth/login', () => {
    it('returns tokens on successful login', async () => {
      await authProvider
        .addInteraction()
        .given('user exists with valid credentials')
        .uponReceiving('a login request with valid credentials')
        .withRequest({
          method: 'POST',
          path: '/api/auth/login',
          headers: {
            'Content-Type': 'application/json',
          },
          body: {
            email: 'test@example.com',
            password: 'password123',
          },
        })
        .willRespondWith({
          status: 200,
          headers: {
            'Content-Type': 'application/json',
          },
          body: like({
            accessToken: string('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'),
            refreshToken: string('refresh-token-value'),
            expiresIn: integer(3600),
            user: like({
              id: integer(1),
              email: string('test@example.com'),
              username: string('testuser'),
            }),
          }),
        })
        .executeTest(async (mockServer) => {
          const response = await fetch(`${mockServer.url}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              email: 'test@example.com',
              password: 'password123',
            }),
          });

          expect(response.status).toBe(200);
          const data = await response.json();
          expect(data.accessToken).toBeDefined();
        });
    });

    it('returns 401 on invalid credentials', async () => {
      await authProvider
        .addInteraction()
        .given('user exists but password is wrong')
        .uponReceiving('a login request with invalid credentials')
        .withRequest({
          method: 'POST',
          path: '/api/auth/login',
          headers: {
            'Content-Type': 'application/json',
          },
          body: {
            email: 'test@example.com',
            password: 'wrongpassword',
          },
        })
        .willRespondWith({
          status: 401,
          headers: {
            'Content-Type': 'application/json',
          },
          body: like({
            error: string('Unauthorized'),
            message: string('Invalid credentials'),
          }),
        })
        .executeTest(async (mockServer) => {
          const response = await fetch(`${mockServer.url}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              email: 'test@example.com',
              password: 'wrongpassword',
            }),
          });

          expect(response.status).toBe(401);
        });
    });
  });
});
