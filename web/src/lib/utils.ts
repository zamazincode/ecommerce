import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * JSON-LD verisini <script type="application/ld+json"> içine güvenle
 * gömülebilecek şekilde serileştirir. `<` karakteri kaçışlanmazsa, veri
 * içinde "</script>" geçtiğinde tarayıcının HTML parser'ı script tag'ini
 * erken kapatır ve sayfa bozulur.
 */
export function toSafeJsonLd(data: unknown): string {
  return JSON.stringify(data).replace(/</g, "\\u003c")
}
