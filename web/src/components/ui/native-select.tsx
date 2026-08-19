import * as React from "react"
import { ChevronDownIcon } from "lucide-react"

import { cn } from "@/lib/utils"

/**
 * Native `<select>` — Base UI'ın Select'i `product-form.test.tsx`'in
 * `userEvent.selectOptions` beklentisiyle uyumsuz olduğundan bilerek native
 * kalıyor; sadece `Input` ile aynı yükseklik/kenarlık/odak halkasıyla giydiriliyor.
 */
const NativeSelect = React.forwardRef<
  HTMLSelectElement,
  React.ComponentProps<"select">
>(({ className, children, ...props }, ref) => {
  return (
    <div className="relative">
      <select
        ref={ref}
        data-slot="native-select"
        className={cn(
          "h-10 w-full appearance-none rounded-lg border border-input bg-background px-3 pr-9 text-sm shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-primary/50 focus-visible:ring-3 focus-visible:ring-primary/25 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20",
          className
        )}
        {...props}
      >
        {children}
      </select>
      <ChevronDownIcon className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-muted-foreground" />
    </div>
  )
})
NativeSelect.displayName = "NativeSelect"

export { NativeSelect }
