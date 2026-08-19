import Link from "next/link"
import { ArrowRightIcon } from "lucide-react"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"

interface SectionHeadingProps {
  title: string
  description?: string
  href?: string
  hrefLabel?: string
  /** Başlığın yanına konan opsiyonel rozet (ör. "İndirimdekiler" yanındaki "%'ye varan indirim"). */
  badge?: React.ReactNode
  className?: string
}

/** Anasayfa bölümleri ve "benzer ürünler" gibi listelerin ortak başlık şeridi. */
function SectionHeading({
  title,
  description,
  href,
  hrefLabel = "Tümünü Gör",
  badge,
  className,
}: SectionHeadingProps) {
  return (
    <div
      data-slot="section-heading"
      className={cn("flex items-end justify-between gap-4", className)}
    >
      <div>
        <div className="flex items-center gap-2">
          <h2 className="font-heading text-xl font-semibold sm:text-2xl">
            {title}
          </h2>
          {badge}
        </div>
        {description && (
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {href && (
        <Button
          variant="ghost"
          shape="pill"
          size="sm"
          render={<Link href={href} />}
          nativeButton={false}
        >
          {hrefLabel}
          <ArrowRightIcon />
        </Button>
      )}
    </div>
  )
}

export { SectionHeading }
