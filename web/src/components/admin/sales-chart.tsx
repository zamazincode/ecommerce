"use client";

import {
	ResponsiveContainer,
	LineChart,
	Line,
	XAxis,
	YAxis,
	Tooltip,
	CartesianGrid,
} from "recharts";
import { formatPrice } from "@/lib/format";
import type { components } from "@/types/api";

type SalesReportItemDto = components["schemas"]["SalesReportItemDto"];

export function SalesChart({ data }: { data: SalesReportItemDto[] }) {
	if (data.length === 0) {
		return (
			<p className="text-sm text-muted-foreground">
				Bu aralıkta sipariş yok.
			</p>
		);
	}

	// `revenue` şemada `number | string` — openapi-typescript'in .NET
	// `decimal`'ini kaçınılmaz biçimde birleştirmesi. Grafiğin sayısal
	// eksende çalışması için burada sayıya çeviriyoruz.
	const chartData = data.map((item) => ({
		...item,
		revenue: Number(item.revenue),
	}));

	return (
		<ResponsiveContainer width="100%" height={240}>
			<LineChart data={chartData}>
				<CartesianGrid
					stroke="var(--border)"
					strokeDasharray="3 3"
					vertical={false}
				/>
				<XAxis
					dataKey="period"
					axisLine={false}
					tickLine={false}
					tick={{ fill: "var(--muted-foreground)", fontSize: 12 }}
				/>
				<YAxis
					axisLine={false}
					tickLine={false}
					tick={{ fill: "var(--muted-foreground)", fontSize: 12 }}
				/>
				<Tooltip
					contentStyle={{
						borderRadius: 12,
						border: "1px solid var(--border)",
						boxShadow: "var(--shadow-pop)",
						background: "var(--popover)",
					}}
					formatter={(value) => [formatPrice(Number(value)), "Ciro"]}
				/>
				<Line
					type="monotone"
					dataKey="revenue"
					stroke="var(--primary)"
					strokeWidth={2}
					dot={false}
					activeDot={{ r: 4 }}
				/>
			</LineChart>
		</ResponsiveContainer>
	);
}
