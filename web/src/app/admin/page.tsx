import { serverApiFetch } from "@/lib/api/server";
import { Card } from "@/components/ui/card";
import { SalesChart } from "@/components/admin/sales-chart";
import { CategoryChart } from "@/components/admin/category-chart";
import type { components } from "@/types/api";

type DashboardSummaryDto = components["schemas"]["DashboardSummaryDto"];
type SalesReportItemDto = components["schemas"]["SalesReportItemDto"];
type CategoryDto = components["schemas"]["CategoryDto"];
type TopSearchDto = components["schemas"]["TopSearchDto"];

export default async function AdminDashboardPage() {
	const today = new Date();
	const thirtyDaysAgo = new Date(today);
	thirtyDaysAgo.setDate(today.getDate() - 29);
	const from = thirtyDaysAgo.toISOString().slice(0, 10);
	const to = today.toISOString().slice(0, 10);

	// Dördü PARALEL — sırayla await etmek 4 kat gecikme demek.
	const [summary, sales, categories, topSearches] = await Promise.all([
		serverApiFetch<DashboardSummaryDto>("admin/dashboard"),
		serverApiFetch<SalesReportItemDto[]>(
			`admin/reports/sales?from=${from}&to=${to}&groupBy=day`,
		),
		serverApiFetch<CategoryDto[]>("categories"),
		serverApiFetch<TopSearchDto[]>("admin/reports/top-searches"),
	]);

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">Dashboard</h1>

			<div className="mb-8 grid grid-cols-2 gap-4 md:grid-cols-4">
				<Card className="p-4">
					<p className="text-sm text-muted-foreground">Toplam Ürün</p>
					<p className="text-2xl font-semibold">
						{summary?.totalProducts ?? "—"}
					</p>
					<p className="text-xs text-muted-foreground">
						{summary?.outOfStockProducts ?? 0} tükendi ·{" "}
						{summary?.lowStockProducts ?? 0} azaldı
					</p>
				</Card>
				<Card className="p-4">
					<p className="text-sm text-muted-foreground">
						Toplam Sipariş
					</p>
					<p className="text-2xl font-semibold">
						{summary?.totalOrders ?? "—"}
					</p>
					<p className="text-xs text-muted-foreground">
						{summary?.pendingOrders ?? 0} beklemede
					</p>
				</Card>
				<Card className="p-4">
					<p className="text-sm text-muted-foreground">Toplam Ciro</p>
					<p className="text-2xl font-semibold">
						{summary?.totalRevenue ?? "—"} ₺
					</p>
					<p className="text-xs text-muted-foreground">
						Son 30 gün: {summary?.last30DaysRevenue ?? 0} ₺
					</p>
				</Card>
				<Card className="p-4">
					<p className="text-sm text-muted-foreground">Müşteri</p>
					<p className="text-2xl font-semibold">
						{summary?.totalCustomers ?? "—"}
					</p>
				</Card>
			</div>

			<div className="grid gap-8 md:grid-cols-2">
				<div>
					<h2 className="mb-2 font-medium">
						Son 30 Gün — Günlük Satış
					</h2>
					<SalesChart data={sales ?? []} />
				</div>
				<div>
					<h2 className="mb-2 font-medium">
						Kategoriye Göre Ürün Dağılımı
					</h2>
					<CategoryChart categories={categories ?? []} />
				</div>
			</div>

			<div className="mt-8 rounded-lg border p-4">
				<h2 className="mb-3 font-medium">
					En Çok Aranan Terimler (Son 30 Gün)
				</h2>
				{topSearches && topSearches.length > 0 ? (
					<ul className="space-y-2 text-sm">
						{topSearches.map((s) => (
							<li
								key={s.term}
								className="flex items-center justify-between"
							>
								<span>{s.term}</span>
								<span className="text-muted-foreground">
									{s.searchCount} arama
									{Number(s.minResultCount) === 0 ? (
										<span className="ml-2 text-amber-600">
											(bazen 0 sonuç)
										</span>
									) : null}
								</span>
							</li>
						))}
					</ul>
				) : (
					<p className="text-sm text-muted-foreground">
						Henüz arama kaydı yok.
					</p>
				)}
			</div>
		</div>
	);
}
