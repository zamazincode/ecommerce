import { Suspense } from "react";
import {
	PackageIcon,
	ShoppingCartIcon,
	TrendingUpIcon,
	UsersIcon,
} from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import { formatPrice } from "@/lib/format";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/error-boundary";
import { PageHeader } from "@/components/admin/page-header";
import { StatCard } from "@/components/admin/stat-card";
import { SalesChartSection } from "@/components/admin/sales-chart-section";
import { CategoryChartSection } from "@/components/admin/category-chart-section";
import type { components } from "@/types/api";

type DashboardSummaryDto = components["schemas"]["DashboardSummaryDto"];
type TopSearchDto = components["schemas"]["TopSearchDto"];

const chartFallback = <Skeleton className="h-60 w-full rounded-xl" />;

const chartError = (
	<p className="text-sm text-muted-foreground">Grafik yüklenemedi.</p>
);

export default async function AdminDashboardPage() {
	// İkisi PARALEL — grafik verisi (Suspense'e sarılı) ayrıca, kendi
	// bileşenlerinde çekiliyor; KPI kartları onu beklemeden render olsun diye.
	const [summary, topSearches] = await Promise.all([
		serverApiFetch<DashboardSummaryDto>("admin/dashboard"),
		serverApiFetch<TopSearchDto[]>("admin/reports/top-searches"),
	]);

	const outOfStock = Number(summary?.outOfStockProducts ?? 0);

	return (
		<div className="space-y-6">
			<PageHeader title="Dashboard" description="Son 30 günün özeti" />

			<div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
				<StatCard
					label="Toplam Ürün"
					value={summary?.totalProducts ?? "—"}
					icon={PackageIcon}
					tone={outOfStock > 0 ? "warning" : "default"}
					hint={`${summary?.outOfStockProducts ?? 0} tükendi · ${summary?.lowStockProducts ?? 0} azaldı`}
				/>
				<StatCard
					label="Toplam Sipariş"
					value={summary?.totalOrders ?? "—"}
					icon={ShoppingCartIcon}
					hint={`${summary?.pendingOrders ?? 0} beklemede`}
				/>
				<StatCard
					label="Toplam Ciro"
					value={
						summary ? formatPrice(Number(summary.totalRevenue)) : "—"
					}
					icon={TrendingUpIcon}
					tone="success"
					hint={`Son 30 gün: ${formatPrice(Number(summary?.last30DaysRevenue ?? 0))}`}
				/>
				<StatCard
					label="Müşteri"
					value={summary?.totalCustomers ?? "—"}
					icon={UsersIcon}
				/>
			</div>

			<div className="grid gap-4 lg:grid-cols-2">
				<Card>
					<CardHeader>
						<CardTitle>Son 30 Gün — Günlük Satış</CardTitle>
					</CardHeader>
					<CardContent>
						<ErrorBoundary fallback={chartError}>
							<Suspense fallback={chartFallback}>
								<SalesChartSection />
							</Suspense>
						</ErrorBoundary>
					</CardContent>
				</Card>
				<Card>
					<CardHeader>
						<CardTitle>Kategoriye Göre Ürün Dağılımı</CardTitle>
					</CardHeader>
					<CardContent>
						<ErrorBoundary fallback={chartError}>
							<Suspense fallback={chartFallback}>
								<CategoryChartSection />
							</Suspense>
						</ErrorBoundary>
					</CardContent>
				</Card>
			</div>

			<Card>
				<CardHeader>
					<CardTitle>En Çok Aranan Terimler (Son 30 Gün)</CardTitle>
				</CardHeader>
				<CardContent>
					{topSearches && topSearches.length > 0 ? (
						<ul className="space-y-1">
							{topSearches.map((s, index) => (
								<li
									key={s.term}
									className="flex items-center justify-between rounded-lg px-2 py-2 text-sm hover:bg-muted"
								>
									<div className="flex min-w-0 items-center gap-3">
										<span className="grid size-6 shrink-0 place-items-center rounded-md bg-muted text-xs font-medium text-muted-foreground">
											{index + 1}
										</span>
										<span className="truncate">{s.term}</span>
									</div>
									<div className="flex shrink-0 items-center gap-2">
										{Number(s.minResultCount) === 0 ? (
											<Badge variant="warning">sonuçsuz</Badge>
										) : null}
										<Badge
											variant="secondary"
											className="tabular-nums"
										>
											{s.searchCount} arama
										</Badge>
									</div>
								</li>
							))}
						</ul>
					) : (
						<p className="text-sm text-muted-foreground">
							Henüz arama kaydı yok.
						</p>
					)}
				</CardContent>
			</Card>
		</div>
	);
}
