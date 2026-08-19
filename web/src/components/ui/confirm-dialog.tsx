"use client"

import * as React from "react"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"

interface ConfirmDialogProps {
  /** Diyaloğu açan öğe — bir `<Button>` örneği olmalı (native `<button>` render eder). */
  trigger: React.ReactElement
  title: string
  description?: string
  confirmLabel?: string
  cancelLabel?: string
  /** "danger" yıkıcı eylemler (silme) için dolu kırmızı onay butonu kullanır. */
  tone?: "danger" | "default"
  onConfirm: () => void | Promise<void>
}

/**
 * `window.confirm()` yerine geçen hazır onay diyaloğu. Projedeki 5 native
 * `confirm()` çağrısı bunu kullanacak (Faz B/C/D'de).
 */
function ConfirmDialog({
  trigger,
  title,
  description,
  confirmLabel = "Onayla",
  cancelLabel = "Vazgeç",
  tone = "default",
  onConfirm,
}: ConfirmDialogProps) {
  return (
    <AlertDialog>
      <AlertDialogTrigger render={trigger} />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description && (
            <AlertDialogDescription>{description}</AlertDialogDescription>
          )}
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            variant={tone === "danger" ? "destructive-solid" : "default"}
            onClick={onConfirm}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export { ConfirmDialog }
