"use client";

import Image from "next/image";
import { PackageIcon } from "lucide-react";
import { useCart } from "@/hooks/use-cart";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatPrice } from "@/lib/format";

/** Ödeme sayfasının sağ sütunundaki mini sepet özeti — `CartPanel`'in sadeleştirilmiş hâli. */
export function OrderSummary() {
	const { data: cart, isLoading } = useCart();

	return (
		<Card className="gap-4 rounded-2xl p-5 shadow-card lg:sticky lg:top-24">
			<h2 className="font-heading text-base font-semibold">
				Sipariş Özeti
			</h2>

			{isLoading || !cart ? (
				<div className="space-y-3">
					{[0, 1].map((i) => (
						<div key={i} className="flex gap-3">
							<Skeleton className="size-12 shrink-0 rounded-lg" />
							<div className="flex-1 space-y-2 pt-1">
								<Skeleton className="h-3 w-3/4" />
								<Skeleton className="h-3 w-1/2" />
							</div>
						</div>
					))}
				</div>
			) : (
				<>
					<ul className="max-h-64 space-y-3 overflow-y-auto">
						{cart.items.map((item) => (
							<li key={item.productId} className="flex gap-3">
								<div className="relative aspect-3/4 w-12 shrink-0 overflow-hidden rounded-lg bg-muted/40">
									{item.imageUrl ? (
										<Image
											src={item.imageUrl}
											alt={item.name}
											fill
											sizes="48px"
											className="object-contain"
										/>
									) : (
										<div className="grid h-full place-items-center text-muted-foreground">
											<PackageIcon className="size-4" />
										</div>
									)}
								</div>
								<div className="min-w-0 flex-1">
									<p className="line-clamp-2 text-sm font-medium">
										{item.name}
									</p>
									<p className="text-xs text-muted-foreground">
										{item.quantity} adet
									</p>
								</div>
								<span className="shrink-0 text-sm font-semibold">
									{formatPrice(item.lineTotal as number)}
								</span>
							</li>
						))}
					</ul>

					<div className="space-y-2 border-t pt-4 text-sm">
						<div className="flex justify-between">
							<span className="text-muted-foreground">
								Ara Toplam
							</span>
							<span>{formatPrice(cart.subTotal as number)}</span>
						</div>
						{(cart.discountAmount as number) > 0 ? (
							<div className="flex justify-between text-muted-foreground">
								<span>İndirim</span>
								<span>
									-{formatPrice(cart.discountAmount as number)}
								</span>
							</div>
						) : null}
						<div className="flex justify-between text-muted-foreground">
							<span>Kargo</span>
							<span>
								{(cart.shippingCost as number) === 0
									? "Ücretsiz"
									: formatPrice(cart.shippingCost as number)}
							</span>
						</div>
						<div className="flex justify-between border-t pt-2 text-base font-semibold">
							<span>Toplam</span>
							<span>{formatPrice(cart.total as number)}</span>
						</div>
					</div>
				</>
			)}
		</Card>
	);
}
