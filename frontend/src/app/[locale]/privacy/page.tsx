import { Link } from "@/i18n/routing"

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-background">
      <div className="max-w-3xl mx-auto px-6 py-16 space-y-8">
        <div className="space-y-2">
          <h1 className="text-3xl font-bold tracking-tight">Privacy Policy</h1>
          <p className="text-muted-foreground">Last updated: May 2026</p>
        </div>

        <div className="prose prose-neutral dark:prose-invert max-w-none space-y-6 text-sm text-muted-foreground">
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Information We Collect</h2>
            <p>
              DevHunt collects information you provide when creating an account, such as your name,
              email address, and profile details. We also collect usage data to improve the platform.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">How We Use Your Information</h2>
            <p>
              We use your information to operate and improve DevHunt, send notifications relevant to
              your projects, and communicate important updates. We do not sell your personal data.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Data Security</h2>
            <p>
              We implement industry-standard security measures to protect your data. All communications
              are encrypted in transit, and sensitive data is encrypted at rest.
            </p>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-foreground">Contact</h2>
            <p>
              For privacy-related inquiries, please contact us via the{" "}
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
