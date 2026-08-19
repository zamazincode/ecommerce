"use client"

import * as React from "react"
import Link from "next/link"
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronsLeftIcon,
  ChevronsRightIcon,
  MoreHorizontalIcon,
} from "lucide-react"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"

interface PaginationProps {
  page: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
  /** Callback modu — admin client sayfaları bunu kullanır. */
  onPageChange?: (page: number) => void
  /** Link modu — SSR sayfaları (`buildHref` varsa `<Link>` render edilir, `onPageChange` yok sayılır). */
  buildHref?: (page: number) => string
  className?: string
}

/** Sayfa numaralarını "1 … 4 5 6 … 20" biçiminde daraltır. */
function getPageRange(page: number, totalPages: number): (number | "ellipsis")[] {
  const delta = 1
  const range: (number | "ellipsis")[] = []
  const start = Math.max(2, page - delta)
  const end = Math.min(totalPages - 1, page + delta)

  range.push(1)
  if (start > 2) range.push("ellipsis")
  for (let i = start; i <= end; i++) range.push(i)
  if (end < totalPages - 1) range.push("ellipsis")
  if (totalPages > 1) range.push(totalPages)

  return range
}

function Pagination({
  page,
  totalPages,
  hasPrevious,
  hasNext,
  onPageChange,
  buildHref,
  className,
}: PaginationProps) {
  if (totalPages <= 1) return null

  const pages = getPageRange(page, totalPages)

  function renderButton(
    targetPage: number,
    content: React.ReactNode,
    opts: { disabled?: boolean; ariaLabel?: string; active?: boolean } = {}
  ) {
    const { disabled = false, ariaLabel, active = false } = opts

    if (disabled) {
      return (
        <Button variant="ghost" size="icon-sm" disabled aria-label={ariaLabel}>
          {content}
        </Button>
      )
    }

    if (buildHref) {
      return (
        <Button
          variant={active ? "default" : "ghost"}
          size="icon-sm"
          className={active ? "rounded-lg" : undefined}
          aria-label={ariaLabel}
          aria-current={active ? "page" : undefined}
          render={<Link href={buildHref(targetPage)} />}
          nativeButton={false}
        >
          {content}
        </Button>
      )
    }

    return (
      <Button
        variant={active ? "default" : "ghost"}
        size="icon-sm"
        className={active ? "rounded-lg" : undefined}
        aria-label={ariaLabel}
        aria-current={active ? "page" : undefined}
        onClick={() => onPageChange?.(targetPage)}
      >
        {content}
      </Button>
    )
  }

  return (
    <nav
      aria-label="Sayfalama"
      className={cn("flex items-center justify-center gap-1", className)}
    >
      {renderButton(1, <ChevronsLeftIcon />, {
        disabled: !hasPrevious,
        ariaLabel: "İlk sayfa",
      })}
      {renderButton(page - 1, <ChevronLeftIcon />, {
        disabled: !hasPrevious,
        ariaLabel: "Önceki sayfa",
      })}

      {pages.map((p, i) =>
        p === "ellipsis" ? (
          <span
            key={`ellipsis-${i}`}
            className="flex size-8 items-center justify-center text-muted-foreground"
          >
            <MoreHorizontalIcon className="size-4" />
          </span>
        ) : (
          <React.Fragment key={p}>
            {renderButton(p, p, { active: p === page })}
          </React.Fragment>
        )
      )}

      {renderButton(page + 1, <ChevronRightIcon />, {
        disabled: !hasNext,
        ariaLabel: "Sonraki sayfa",
      })}
      {renderButton(totalPages, <ChevronsRightIcon />, {
        disabled: !hasNext,
        ariaLabel: "Son sayfa",
      })}
    </nav>
  )
}

export { Pagination }
