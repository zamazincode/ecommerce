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
				<XAxis type="number" tick={{ fontSize: 12 }} />
				<YAxis
					type="category"
					dataKey="name"
					width={100}
					tick={{ fontSize: 11 }}
				/>
				<Tooltip />
				<Bar dataKey="ürünSayısı" fill="var(--primary)" />
			</BarChart>
		</ResponsiveContainer>
	);
}
