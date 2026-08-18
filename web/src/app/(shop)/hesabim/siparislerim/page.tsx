import Link from "next/link";
import { serverApiFetch } from "@/lib/api/server";
import { ORDER_STATUS_LABELS } from "@/lib/enums";
import type { components } from "@/types/api";

type PagedResultOfOrderListDto =
	components["schemas"]["PagedResultOfOrderListDto"];

export default async function OrderHistoryPage({
	searchParams,
}: {
	searchParams: Promise<{ page?: string }>;
}) {
	const { page } = await searchParams;
	const result = await serverApiFetch<PagedResultOfOrderListDto>(
		`orders?page=${page ?? "1"}`,
	);

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">Siparişlerim</h1>

			{!result || result.items.length === 0 ? (
				<p className="text-sm text-muted-foreground">
					Henüz hiç sipariş vermedin.
				</p>
			) : (
				<div className="space-y-3">
					{result.items.map((order) => (
						<Link
							key={order.orderNumber}
							href={`/hesabim/siparislerim/${order.orderNumber}`}
							className="flex items-center justify-between rounded-lg border p-4 hover:bg-muted/30"
						>
							<div>
								<p className="font-medium">
									{order.orderNumber}
								</p>
								<p className="text-xs text-muted-foreground">
									{new Date(
										order.createdAt,
									).toLocaleDateString("tr-TR")}{" "}
									· {order.totalQuantity} ürün
								</p>
							</div>
							<div className="text-right">
								<p className="font-medium">{order.total} ₺</p>
								<p className="text-xs text-muted-foreground">
									{
										ORDER_STATUS_LABELS[
											order.status as keyof typeof ORDER_STATUS_LABELS
										]
									}
								</p>
							</div>
						</Link>
					))}
				</div>
			)}
		</div>
	);
}
