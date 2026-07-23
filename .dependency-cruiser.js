// dependency-cruiser config
// Goal: catch architectural drift early (cycles, forbidden cross-layer imports, etc.)
// Docs: https://www.npmjs.com/package/dependency-cruiser

module.exports = {
  options: {
    // Make it fast + deterministic in CI
    doNotFollow: {
      path: [
        "node_modules",
        "dist",
        "build",
        ".next",
        "coverage"
      ],
    },
    // tsConfig path is resolved relative to CWD where depcruise runs
    // Only used for TypeScript projects (frontend has tsconfig.json)
    // JS-only projects (notification-service, integration-gateway) don't need this
    tsPreCompilationDeps: true,
    enhancedResolveOptions: {
      exportsFields: ["exports"],
      conditionNames: ["require", "node", "import", "default"],
    },
    reporterOptions: {
      dot: {
        theme: {
          graph: { splines: "ortho" },
        },
      },
    },
  },

  forbidden: [
    // 1) No circular dependencies (classic architecture rot)
    {
      name: "no-circular",
      severity: "error",
      from: {},
      to: { circular: true },
    },

    // 2) Don't import from YOUR OWN build output (not node_modules)
    {
      name: "no-build-artifacts",
      severity: "error",
      from: { path: "^src" },
      to: {
        path: "^(dist|build|out|coverage|.next)(/|$)",
        pathNot: "node_modules"
      },
    },

    // 3) Keep tests from becoming a backdoor into internals
    {
      name: "tests-should-not-be-imported",
      severity: "warn",
      from: {},
      to: {
        path: "(^|/)__tests__(/|$)|(^|/)tests(/|$)",
      },
    },

    // 4) Example Clean-ish Architecture for frontend.
    // Adapt folder names to your reality.
    // ui -> application -> domain, and infrastructure can be used by application (not vice-versa).
    {
      name: "domain-must-not-depend-on-ui",
      severity: "error",
      from: { path: "^src/domain" },
      to: { path: "^src/ui" },
    },
    {
      name: "domain-must-not-depend-on-infrastructure",
      severity: "error",
      from: { path: "^src/domain" },
      to: { path: "^src/infrastructure" },
    },
    {
      name: "application-must-not-depend-on-ui",
      severity: "error",
      from: { path: "^src/application" },
      to: { path: "^src/ui" },
    },
  ],
};
