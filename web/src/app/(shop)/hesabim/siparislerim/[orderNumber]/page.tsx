import Link from "next/link";
import { notFound } from "next/navigation";
import { serverApiFetch } from "@/lib/api/server";
import { ORDER_STATUS_LABELS } from "@/lib/enums";
import type { components } from "@/types/api";
import { CancelOrderButton } from "@/components/account/cancel-order-button";
import { Button } from "@/components/ui/button";

type OrderDetailDto = components["schemas"]["OrderDetailDto"];

export default async function OrderDetailPage({
	params,
}: {
	params: Promise<{ orderNumber: string }>;
}) {
	const { orderNumber } = await params;
	const order = await serverApiFetch<OrderDetailDto>(`orders/${orderNumber}`);
	if (!order) notFound();

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<div>
					<h1 className="text-xl font-semibold">
						{order.orderNumber}
					</h1>
					<p className="text-sm text-muted-foreground">
						{
							ORDER_STATUS_LABELS[
								order.status as keyof typeof ORDER_STATUS_LABELS
							]
						}
					</p>
				</div>
				<div className="flex gap-2">
					{order.status === 0 ? (
						<Button
							size="sm"
							render={
								<Link
									href={`/odeme?step=payment&siparis=${order.orderNumber}`}
								/>
							}
						>
							Tekrar Öde
						</Button>
					) : null}
					{order.canBeCancelled ? (
						<CancelOrderButton orderNumber={order.orderNumber} />
					) : null}
				</div>
			</div>

			<section className="mb-6 rounded-lg border p-4">
				<h2 className="mb-2 font-medium">Teslimat Adresi</h2>
				<p className="text-sm text-muted-foreground">
					{order.shippingAddress.fullName} —{" "}
					{order.shippingAddress.fullAddress},{" "}
					{order.shippingAddress.district}/
					{order.shippingAddress.city}
				</p>
			</section>

			<section className="rounded-lg border p-4">
				<h2 className="mb-3 font-medium">Ürünler</h2>
				<ul className="space-y-3">
					{order.items.map((item) => (
						<li
							key={item.productId}
							className="flex justify-between text-sm"
						>
							<span>
								{item.productName} × {item.quantity}
							</span>
							<span>{item.lineTotal} ₺</span>
						</li>
					))}
				</ul>
				<div className="mt-4 space-y-1 border-t pt-4 text-sm">
					<div className="flex justify-between">
						<span>Ara Toplam</span>
						<span>{order.subTotal} ₺</span>
					</div>
					{(order.discountAmount as number) > 0 ? (
						<div className="flex justify-between text-muted-foreground">
							<span>
								İndirim{" "}
								{order.couponCode
									? `(${order.couponCode})`
									: ""}
							</span>
							<span>-{order.discountAmount} ₺</span>
						</div>
					) : null}
					<div className="flex justify-between text-muted-foreground">
						<span>Kargo</span>
						<span>
							{order.shippingCost === 0
								? "Ücretsiz"
								: `${order.shippingCost} ₺`}
						</span>
					</div>
					<div className="flex justify-between text-base font-semibold">
						<span>Toplam</span>
						<span>{order.total} ₺</span>
					</div>
				</div>
			</section>
		</div>
	);
}
