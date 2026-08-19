import Link from "next/link";
import { ChevronRightIcon, PackageIcon } from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TONES } from "@/lib/enums";
import { PageHeader } from "@/components/admin/page-header";
import { Badge } from "@/components/ui/badge";
import { Price } from "@/components/ui/price";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ProductListPagination } from "@/components/product/product-list-pagination";
import type { components } from "@/types/api";

type PagedResultOfOrderListDto =
	components["schemas"]["PagedResultOfOrderListDto"];
type OrderStatus = components["schemas"]["OrderStatus"];

export default async function OrderHistoryPage({
	searchParams,
}: {
	searchParams: Promise<{ page?: string }>;
}) {
	const { page } = await searchParams;
	const currentPage = Number(page ?? "1");
	const result = await serverApiFetch<PagedResultOfOrderListDto>(
		`orders?page=${currentPage}`,
	);

	return (
		<div>
			<PageHeader title="Siparişlerim" className="mb-6" />

			{!result || result.items.length === 0 ? (
				<EmptyState
					icon={PackageIcon}
					title="Henüz hiç sipariş vermedin"
					description="Alışverişe başlamak için ürünleri keşfet."
					action={
						<Button render={<Link href="/" />} nativeButton={false}>
							Alışverişe Başla
						</Button>
					}
				/>
			) : (
				<>
					<div className="space-y-3">
						{result.items.map((order) => (
							<Link
								key={order.orderNumber}
								href={`/hesabim/siparislerim/${order.orderNumber}`}
								className="flex items-center justify-between gap-3 rounded-2xl border p-4 transition-all hover:border-primary/40 hover:shadow-card"
							>
								<div className="min-w-0">
									<p className="font-mono font-semibold">
										{order.orderNumber}
									</p>
									<p className="text-xs text-muted-foreground">
										{new Date(
											order.createdAt,
										).toLocaleDateString("tr-TR")}{" "}
										· {order.totalQuantity} ürün
									</p>
								</div>
								<div className="flex items-center gap-3">
									<div className="text-right">
										<Price
											size="sm"
											price={Number(order.total)}
										/>
										<Badge
											variant={
												ORDER_STATUS_TONES[
													order.status as OrderStatus
												]
											}
											className="mt-1"
										>
											{
												ORDER_STATUS_LABELS[
													order.status as OrderStatus
												]
											}
										</Badge>
									</div>
									<ChevronRightIcon className="size-4 shrink-0 text-muted-foreground" />
								</div>
							</Link>
						))}
					</div>

					<ProductListPagination
						className="mt-6"
						basePath="/hesabim/siparislerim"
						params={{}}
						page={currentPage}
						totalPages={Number(result.totalPages ?? 1)}
						hasPrevious={!!result.hasPrevious}
						hasNext={!!result.hasNext}
					/>
				</>
			)}
		</div>
	);
}
