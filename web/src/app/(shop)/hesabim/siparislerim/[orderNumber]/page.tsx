import Link from "next/link";
import { notFound } from "next/navigation";
import { MapPinIcon, PackageIcon } from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TONES } from "@/lib/enums";
import { formatPrice } from "@/lib/format";
import type { components } from "@/types/api";
import { CancelOrderButton } from "@/components/account/cancel-order-button";
import { OrderStatusTimeline } from "@/components/account/order-status-timeline";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";

type OrderDetailDto = components["schemas"]["OrderDetailDto"];
type OrderStatus = components["schemas"]["OrderStatus"];

// Bu iki durumda akış "bitmiş" sayılır — normal zaman çizelgesi yerine tek rozet gösteriliyor.
const TERMINAL_STATUSES: OrderStatus[] = [5, 6];

export default async function OrderDetailPage({
	params,
}: {
	params: Promise<{ orderNumber: string }>;
}) {
	const { orderNumber } = await params;
	const order = await serverApiFetch<OrderDetailDto>(`orders/${orderNumber}`);
	if (!order) notFound();

	const status = order.status as OrderStatus;
	const isTerminal = TERMINAL_STATUSES.includes(status);

	return (
		<div>
			<div className="mb-6 flex flex-wrap items-center justify-between gap-3">
				<div>
					<h1 className="font-heading text-xl font-semibold">
						{order.orderNumber}
					</h1>
					<p className="text-sm text-muted-foreground">
						{new Date(order.createdAt).toLocaleDateString("tr-TR", {
							day: "numeric",
							month: "long",
							year: "numeric",
						})}
					</p>
				</div>
				<div className="flex gap-2">
					{status === 0 ? (
						<Button
							size="sm"
							render={
								<Link
									href={`/odeme?step=payment&siparis=${order.orderNumber}`}
								/>
							}
							nativeButton={false}
						>
							Tekrar Öde
						</Button>
					) : null}
					{order.canBeCancelled ? (
						<CancelOrderButton orderNumber={order.orderNumber} />
					) : null}
				</div>
			</div>

			<Card className="mb-6 p-5">
				{isTerminal ? (
					<Badge variant={ORDER_STATUS_TONES[status]}>
						{ORDER_STATUS_LABELS[status]}
					</Badge>
				) : (
					<OrderStatusTimeline status={status} />
				)}
			</Card>

			<Card className="mb-6 p-5">
				<h2 className="mb-2 flex items-center gap-2 font-heading text-base font-semibold">
					<MapPinIcon className="size-4 text-muted-foreground" />
					Teslimat Adresi
				</h2>
				<p className="text-sm text-muted-foreground">
					{order.shippingAddress.fullName} —{" "}
					{order.shippingAddress.fullAddress},{" "}
					{order.shippingAddress.district}/
					{order.shippingAddress.city}
				</p>
			</Card>

			<Card className="p-5">
				<h2 className="mb-3 flex items-center gap-2 font-heading text-base font-semibold">
					<PackageIcon className="size-4 text-muted-foreground" />
					Ürünler
				</h2>
				<ul className="space-y-3">
					{order.items.map((item) => (
						<li
							key={item.productId}
							className="flex justify-between text-sm"
						>
							<span>
								{item.productName} × {item.quantity}
							</span>
							<span className="font-medium">
								{formatPrice(item.lineTotal as number)}
							</span>
						</li>
					))}
				</ul>
				<div className="mt-4 space-y-1 border-t pt-4 text-sm">
					<div className="flex justify-between">
						<span className="text-muted-foreground">
							Ara Toplam
						</span>
						<span>{formatPrice(order.subTotal as number)}</span>
					</div>
					{(order.discountAmount as number) > 0 ? (
						<div className="flex justify-between text-muted-foreground">
							<span>
								İndirim{" "}
								{order.couponCode
									? `(${order.couponCode})`
									: ""}
							</span>
							<span>
								-{formatPrice(order.discountAmount as number)}
							</span>
						</div>
					) : null}
					<div className="flex justify-between text-muted-foreground">
						<span>Kargo</span>
						<span>
							{order.shippingCost === 0
								? "Ücretsiz"
								: formatPrice(order.shippingCost as number)}
						</span>
					</div>
					<div className="flex justify-between border-t pt-2 text-base font-semibold">
						<span>Toplam</span>
						<span>{formatPrice(order.total as number)}</span>
					</div>
				</div>
			</Card>
		</div>
	);
}
