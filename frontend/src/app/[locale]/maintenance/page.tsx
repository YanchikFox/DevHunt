import { Link } from "@/i18n/routing"
import { Wrench } from "lucide-react"

export default function MaintenancePage() {
  return <MaintenanceContent />
}

function MaintenanceContent() {
  // Server component — useTranslations works here
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center max-w-md px-6 space-y-6">
        <div className="flex justify-center">
          <div className="rounded-full bg-yellow-100 dark:bg-yellow-900/20 p-6">
            <Wrench className="h-16 w-16 text-yellow-600 dark:text-yellow-400" />
          </div>
        </div>
        <h1 className="text-3xl font-bold tracking-tight">Under Maintenance</h1>
        <p className="text-muted-foreground text-lg">
          DevHunt is currently undergoing scheduled maintenance. We&apos;ll be back shortly.
        </p>
        <p className="text-sm text-muted-foreground">
          If you are an administrator, please{" "}
          <Link href="/login" className="underline underline-offset-4 hover:text-foreground">
            log in
          </Link>{" "}
          to access the admin panel.
        </p>
      </div>
    </div>
  )
}
