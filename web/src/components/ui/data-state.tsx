import { AlertCircleIcon } from "lucide-react"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableRow,
} from "@/components/ui/table"

interface TableSkeletonProps {
  rows?: number
  cols?: number
  className?: string
}

/** Admin tablolarının yüklenme hâli — gerçek satır sayısına yakın bir iskelet gösterir. */
function TableSkeleton({ rows = 5, cols = 4, className }: TableSkeletonProps) {
  return (
    <Table className={className}>
      <TableBody>
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <TableRow key={rowIndex} className="hover:bg-transparent">
            {Array.from({ length: cols }).map((_, colIndex) => (
              <TableCell key={colIndex}>
                <Skeleton className="h-4 w-full" />
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

interface InlineErrorProps {
  message: string
  onRetry?: () => void
  className?: string
}

/** Admin tablolarının hata hâli — mesaj + opsiyonel "Tekrar dene" butonu. */
function InlineError({ message, onRetry, className }: InlineErrorProps) {
  return (
    <div
      data-slot="inline-error"
      className={cn(
        "flex flex-col items-center gap-3 py-10 text-center",
        className
      )}
    >
      <AlertCircleIcon className="size-8 text-destructive/60" />
      <p className="text-sm text-muted-foreground">{message}</p>
      {onRetry && (
        <Button type="button" variant="outline" size="sm" onClick={onRetry}>
          Tekrar dene
        </Button>
      )}
    </div>
  )
}

export { TableSkeleton, InlineError }
