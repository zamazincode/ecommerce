"use client";

import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip } from "recharts";
import type { components } from "@/types/api";

type CategoryDto = components["schemas"]["CategoryDto"] & {
	productCount?: number;
};

export function CategoryChart({ categories }: { categories: CategoryDto[] }) {
	// Yalnızca KÖK kategoriler (ParentId null) — 389 kategorinin tamamını
	// çubuk grafikte göstermek okunaksız olurdu (7 kök, 4 seviye).
	const roots = categories
		.filter((c) => !c.parentId)
		.map((c) => ({ name: c.name, ürünSayısı: c.productCount ?? 0 }))
		.sort((a, b) => b.ürünSayısı - a.ürünSayısı)
		.slice(0, 7);

	return (
		<ResponsiveContainer width="100%" height={240}>
			<BarChart data={roots} layout="vertical">
				<XAxis
					type="number"
					axisLine={false}
					tickLine={false}
					tick={{ fill: "var(--muted-foreground)", fontSize: 12 }}
				/>
				<YAxis
					type="category"
					dataKey="name"
					width={100}
					axisLine={false}
					tickLine={false}
					tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
				/>
				<Tooltip
					contentStyle={{
						borderRadius: 12,
						border: "1px solid var(--border)",
						boxShadow: "var(--shadow-pop)",
						background: "var(--popover)",
					}}
				/>
				<Bar dataKey="ürünSayısı" fill="var(--chart-1)" radius={[0, 6, 6, 0]} />
			</BarChart>
		</ResponsiveContainer>
	);
}
