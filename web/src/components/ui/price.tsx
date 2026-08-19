import { formatPrice } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Badge } from "@/components/ui/badge"

interface PriceProps {
  price: number
  discountedPrice?: number | null
  size?: "sm" | "md" | "lg"
  showBadge?: boolean
  className?: string
}

const SIZE_CLASSES: Record<NonNullable<PriceProps["size"]>, string> = {
  sm: "text-sm",
  md: "text-base",
  lg: "text-xl",
}

const ORIGINAL_SIZE_CLASSES: Record<NonNullable<PriceProps["size"]>, string> = {
  sm: "text-[11px]",
  md: "text-xs",
  lg: "text-sm",
}

/**
 * Projede 9 ayrı yerde ham `{x} ₺` basılıyordu — fiyat gösterimi tek
 * bileşende toplanıyor. İndirim varsa eski fiyat üstü çizili, yüzde rozeti
 * opsiyonel.
 */
function Price({
  price,
  discountedPrice,
  size = "md",
  showBadge = false,
  className,
}: PriceProps) {
  const hasDiscount =
    discountedPrice != null && discountedPrice < price

  const percentage = hasDiscount
    ? Math.round((1 - discountedPrice / price) * 100)
    : 0

  return (
    <div
      data-slot="price"
      className={cn("flex flex-wrap items-baseline gap-1.5", className)}
    >
      <span
        className={cn(
          "font-semibold text-foreground",
          SIZE_CLASSES[size]
        )}
      >
        {formatPrice(hasDiscount ? discountedPrice : price)}
      </span>
      {hasDiscount && (
        <span
          className={cn(
            "text-muted-foreground line-through",
            ORIGINAL_SIZE_CLASSES[size]
          )}
        >
          {formatPrice(price)}
        </span>
      )}
      {hasDiscount && showBadge && percentage > 0 && (
        <Badge variant="accent">%{percentage}</Badge>
      )}
    </div>
  )
}

export { Price }
