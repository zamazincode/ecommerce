"use client"

import { MinusIcon, PlusIcon } from "lucide-react"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"

interface QuantityStepperProps {
  value: number
  min?: number
  max?: number
  onChange: (value: number) => void
  disabled?: boolean
  className?: string
}

/** Sepet/ürün detayında 3 ham `<input type=number>`'ın yerini alacak. */
function QuantityStepper({
  value,
  min = 1,
  max,
  onChange,
  disabled = false,
  className,
}: QuantityStepperProps) {
  const canDecrease = !disabled && value > min
  const canIncrease = !disabled && (max === undefined || value < max)

  return (
    <div
      data-slot="quantity-stepper"
      className={cn(
        "inline-flex h-10 items-center rounded-full border border-input",
        className
      )}
    >
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        className="rounded-full"
        disabled={!canDecrease}
        onClick={() => onChange(Math.max(min, value - 1))}
        aria-label="Azalt"
      >
        <MinusIcon />
      </Button>
      <span className="w-10 text-center text-sm tabular-nums">{value}</span>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        className="rounded-full"
        disabled={!canIncrease}
        onClick={() =>
          onChange(max === undefined ? value + 1 : Math.min(max, value + 1))
        }
        aria-label="Artır"
      >
        <PlusIcon />
      </Button>
    </div>
  )
}

export { QuantityStepper }
