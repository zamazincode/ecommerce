"use client";

import dynamic from "next/dynamic";
import { Skeleton } from "@/components/ui/skeleton";

// `next/dynamic`'in `ssr: false` seçeneği yalnızca Client Component'lerde
// izinli — bu yüzden `sales-chart-section.tsx` (async Server Component)
// içine DOĞRUDAN yazılamıyor, bu küçük istemci sarmalayıcısına taşındı.
export const SalesChart = dynamic(
	() => import("@/components/admin/sales-chart").then((m) => m.SalesChart),
	{ ssr: false, loading: () => <Skeleton className="h-60 w-full" /> },
);
