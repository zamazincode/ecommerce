import { Suspense } from "react";
import { serverApiFetch } from "@/lib/api/server";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/error-boundary";
import { SalesChartSection } from "@/components/admin/sales-chart-section";
import { CategoryChartSection } from "@/components/admin/category-chart-section";
import type { components } from "@/types/api";

type DashboardSummaryDto = components["schemas"]["DashboardSummaryDto"];
type TopSearchDto = components["schemas"]["TopSearchDto"];

export default async function AdminDashboardPage() {
	// İkisi PARALEL — grafik verisi (Suspense'e sarılı) ayrıca, kendi
	// bileşenlerinde çekiliyor; KPI kartları onu beklemeden render olsun diye.
	const [summary, topSearches] = await Promise.all([
		serverApiFetch<DashboardSummaryDto>("admin/dashboard"),
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
					<ErrorBoundary
						fallback={
							<p className="text-sm text-muted-foreground">
								Grafik yüklenemedi.
							</p>
						}
					>
						<Suspense fallback={<Skeleton className="h-60 w-full" />}>
							<SalesChartSection />
						</Suspense>
					</ErrorBoundary>
				</div>
				<div>
					<h2 className="mb-2 font-medium">
						Kategoriye Göre Ürün Dağılımı
					</h2>
					<ErrorBoundary
						fallback={
							<p className="text-sm text-muted-foreground">
								Grafik yüklenemedi.
							</p>
						}
					>
						<Suspense fallback={<Skeleton className="h-60 w-full" />}>
							<CategoryChartSection />
						</Suspense>
					</ErrorBoundary>
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
