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
				<CartesianGrid strokeDasharray="3 3" />
				<XAxis dataKey="period" tick={{ fontSize: 12 }} />
				<YAxis tick={{ fontSize: 12 }} />
				<Tooltip formatter={(value) => [`${value} ₺`, "Ciro"]} />
				<Line
					type="monotone"
					dataKey="revenue"
					stroke="var(--primary)"
					strokeWidth={2}
					dot={false}
				/>
			</LineChart>
		</ResponsiveContainer>
	);
}
