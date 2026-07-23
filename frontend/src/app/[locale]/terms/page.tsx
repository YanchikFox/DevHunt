import { Link } from "@/i18n/routing"

export default function TermsPage() {
  return (
    <div className="min-h-screen bg-background">
      <div className="max-w-3xl mx-auto px-6 py-16 space-y-8">
        <div className="space-y-2">
          <h1 className="text-3xl font-bold tracking-tight">Terms of Service</h1>
          <p className="text-muted-foreground">Last updated: May 2026</p>
        </div>

        <div className="prose prose-neutral dark:prose-invert max-w-none space-y-6 text-sm text-muted-foreground">
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Acceptance of Terms</h2>
            <p>
              By accessing or using DevHunt, you agree to be bound by these Terms of Service.
              If you do not agree, please do not use the platform.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Use of the Platform</h2>
            <p>
              DevHunt is a platform for developers to collaborate on projects. You are responsible
              for all content you post and must comply with applicable laws. You may not use the
              platform for illegal or harmful activities.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Intellectual Property</h2>
            <p>
              You retain ownership of content you create on DevHunt. By posting content, you grant
              DevHunt a license to display and distribute it within the platform.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Account Termination</h2>
            <p>
              We reserve the right to suspend or terminate accounts that violate these terms.
              You may delete your account at any time via your profile settings.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Contact</h2>
            <p>
              For questions about these terms, please reach out via the{" "}
              <Link href="/support" className="underline underline-offset-4 hover:text-foreground">
                support page
              </Link>
              .
            </p>
          </section>
        </div>

        <div className="pt-4 border-t border-border">
          <Link href="/" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
            ← Back to DevHunt
          </Link>
        </div>
      </div>
    </div>
  )
}
