import { serverApiFetch } from "@/lib/api/server";
import { SalesChart } from "@/components/admin/sales-chart-lazy";
import type { components } from "@/types/api";

type SalesReportItemDto = components["schemas"]["SalesReportItemDto"];

export async function SalesChartSection() {
	const today = new Date();
	const thirtyDaysAgo = new Date(today);
	thirtyDaysAgo.setDate(today.getDate() - 29);
	const from = thirtyDaysAgo.toISOString().slice(0, 10);
	const to = today.toISOString().slice(0, 10);

	const sales = await serverApiFetch<SalesReportItemDto[]>(
		`admin/reports/sales?from=${from}&to=${to}&groupBy=day`,
	);

	return <SalesChart data={sales ?? []} />;
}
