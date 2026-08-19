import type { LucideIcon } from "lucide-react"

import { cn } from "@/lib/utils"

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description?: string
  action?: React.ReactNode
  className?: string
  /** "danger" — hata sayfaları (admin `error.tsx`) için kırmızı tonlu ikon dairesi. */
  tone?: "default" | "danger"
}

/** Boş sepet/favori/sipariş/arama sonucu/admin tablosu için ortak boş durum görünümü. */
function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
  tone = "default",
}: EmptyStateProps) {
  return (
    <div
      data-slot="empty-state"
      className={cn(
        "flex flex-col items-center gap-3 py-16 text-center",
        className
      )}
    >
      <div
        className={cn(
          "grid size-16 place-items-center rounded-full",
          tone === "danger" ? "bg-destructive/10" : "bg-muted"
        )}
      >
        <Icon
          className={cn(
            "size-8",
            tone === "danger"
              ? "text-destructive/70"
              : "text-muted-foreground/40"
          )}
        />
      </div>
      <p className="text-sm font-medium text-foreground">{title}</p>
      {description && (
        <p className="max-w-sm text-sm text-muted-foreground">{description}</p>
      )}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

export { EmptyState }
