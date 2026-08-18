"use client";

import dynamic from "next/dynamic";
import { Skeleton } from "@/components/ui/skeleton";

// bkz. sales-chart-lazy.tsx'teki aynı not.
export const CategoryChart = dynamic(
	() =>
		import("@/components/admin/category-chart").then(
			(m) => m.CategoryChart,
		),
	{ ssr: false, loading: () => <Skeleton className="h-60 w-full" /> },
);
