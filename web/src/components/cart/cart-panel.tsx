"use client";

import Image from "next/image";
import Link from "next/link";
import {
	ArrowRightIcon,
	CheckIcon,
	PackageIcon,
	ShoppingCartIcon,
	TrashIcon,
	TruckIcon,
} from "lucide-react";
import { useUiStore } from "@/stores/ui-store";
import {
	useCart,
	useUpdateCartItem,
	useRemoveCartItem,
	useClearCart,
} from "@/hooks/use-cart";
import { FREE_SHIPPING_THRESHOLD } from "@/lib/shipping";
import { CouponForm } from "@/components/cart/coupon-form";
import {
	Sheet,
	SheetContent,
	SheetHeader,
	SheetTitle,
} from "@/components/ui/sheet";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { QuantityStepper } from "@/components/ui/quantity-stepper";
import { formatPrice } from "@/lib/format";
import { cn } from "@/lib/utils";

export function CartPanel() {
	const isOpen = useUiStore((s) => s.isCartPanelOpen);
	const closeCartPanel = useUiStore((s) => s.closeCartPanel);
	const { data: cart, isLoading } = useCart();
	const updateItem = useUpdateCartItem();
	const removeItem = useRemoveCartItem();
	const clearCart = useClearCart();

	const hasItems = !!cart && cart.items.length > 0;
	const freeShippingRemaining = (cart?.freeShippingRemaining as number) ?? 0;
	const shippingProgress = Math.min(
		100,
		Math.max(
			0,
			((FREE_SHIPPING_THRESHOLD - freeShippingRemaining) /
				FREE_SHIPPING_THRESHOLD) *
				100,
		),
	);

	return (
		<Sheet open={isOpen} onOpenChange={(open) => !open && closeCartPanel()}>
			<SheetContent
				side="right"
				className="flex w-full flex-col gap-0 p-0 sm:max-w-md"
			>
				<SheetHeader className="flex-row items-center justify-between gap-2 border-b px-5 py-4">
					<SheetTitle>Sepetim</SheetTitle>
					{hasItems ? (
						<Badge variant="secondary">
							{(cart.totalQuantity as number) ?? cart.items.length} ürün
						</Badge>
					) : null}
				</SheetHeader>

				{isLoading ? (
					<div className="flex-1 space-y-4 px-5 py-4">
						{[0, 1, 2].map((i) => (
							<div key={i} className="flex gap-3">
								<Skeleton className="size-14 shrink-0 rounded-lg" />
								<div className="flex-1 space-y-2 pt-1">
									<Skeleton className="h-4 w-3/4" />
									<Skeleton className="h-4 w-1/2" />
								</div>
							</div>
						))}
					</div>
				) : !hasItems ? (
					<EmptyState
						icon={ShoppingCartIcon}
						title="Sepetin boş"
						description="Alışverişe başlamak için ürünleri keşfetmeye ne dersin?"
						className="flex-1"
						action={
							<Button
								render={<Link href="/" />}
								nativeButton={false}
								onClick={closeCartPanel}
							>
								Alışverişe Başla
							</Button>
						}
					/>
				) : (
					<>
						{freeShippingRemaining > 0 ? (
							<div className="bg-primary-soft px-5 py-3 text-xs text-primary">
								<p className="flex items-center gap-1.5">
									<TruckIcon className="size-3.5" />
									Kargo bedava için{" "}
									{formatPrice(freeShippingRemaining)} daha ekle
								</p>
								<div className="mt-1.5 h-1.5 rounded-full bg-primary/15">
									<div
										className="h-full rounded-full bg-primary transition-all"
										style={{ width: `${shippingProgress}%` }}
									/>
								</div>
							</div>
						) : (
							<div className="flex items-center gap-1.5 bg-success-soft px-5 py-3 text-xs text-success">
								<CheckIcon className="size-3.5" />
								Kargo bedava!
							</div>
						)}

						<ul className="flex-1 divide-y overflow-y-auto">
							{cart.items.map((item) => (
								<li key={item.productId} className="flex gap-3 px-5 py-4">
									<div className="relative aspect-3/4 w-14 shrink-0 overflow-hidden rounded-lg bg-muted/40">
										{item.imageUrl ? (
											<Image
												src={item.imageUrl}
												alt={item.name}
												fill
												sizes="56px"
												className="object-contain"
											/>
										) : (
											<div className="grid h-full place-items-center text-muted-foreground">
												<PackageIcon className="size-5" />
											</div>
										)}
									</div>

									<div className="min-w-0 flex-1">
										<p className="line-clamp-2 text-sm font-medium">
											{item.name}
										</p>
										{item.priceChanged ? (
											<Badge
												variant="warning"
												className="mt-1 text-[10px]"
											>
												Fiyat değişti
											</Badge>
										) : null}
										<div className="mt-2 flex items-center justify-between gap-2">
											<QuantityStepper
												value={item.quantity as number}
												max={item.availableStock as number}
												onChange={(quantity) =>
													updateItem.mutate({
														productId: item.productId as number,
														quantity,
													})
												}
												className="h-8"
											/>
											<span className="text-sm font-semibold">
												{formatPrice(item.lineTotal as number)}
											</span>
										</div>
									</div>

									<Button
										variant="ghost"
										size="icon-sm"
										className="text-muted-foreground hover:text-destructive"
										onClick={() =>
											removeItem.mutate(item.productId as number)
										}
										aria-label={`${item.name} ürününü sepetten çıkar`}
									>
										<TrashIcon />
									</Button>
								</li>
							))}
						</ul>

						{cart.warnings.length > 0 ? (
							<div className="mx-5 mb-3 space-y-1 rounded-lg bg-warning-soft p-3 text-xs text-warning">
								{cart.warnings.map((warning, i) => (
									<p key={i}>{warning}</p>
								))}
							</div>
						) : null}

						<CouponForm couponCode={cart.couponCode} />

						<div className="space-y-2 border-t bg-surface px-5 py-4">
							<div className="flex justify-between text-sm">
								<span>Ara Toplam</span>
								<span>{formatPrice(cart.subTotal as number)}</span>
							</div>
							{(cart.discountAmount as number) > 0 ? (
								<div className="flex justify-between text-sm text-muted-foreground">
									<span>İndirim</span>
									<span>
										-{formatPrice(cart.discountAmount as number)}
									</span>
								</div>
							) : null}
							<div className="flex justify-between text-lg font-semibold">
								<span>Toplam</span>
								<span>{formatPrice(cart.total as number)}</span>
							</div>

							{/*
							 * Base UI `Button`, `nativeButton={false}` iken render edilen
							 * elemente KOŞULSUZ `role="button"` enjekte ediyor (ölçüldü:
							 * `useButton.mjs` → `isNativeButton ? {type:'button'} : {role:'button'}`).
							 * Bu, altındaki gerçek `<a>`'nın örtük "link" rolünü eziyor —
							 * E2E testi `getByRole("link", {name: /ödemeye geç/i})` beklediği
							 * için `Button` bileşenini hiç kullanmıyoruz, `buttonVariants`
							 * sınıflarını doğrudan bir `<Link>`'e uyguluyoruz.
							 */}
							<Link
								href="/odeme"
								onClick={closeCartPanel}
								className={cn(buttonVariants({ size: "lg" }), "w-full")}
							>
								Ödemeye Geç
								<ArrowRightIcon />
							</Link>
							<button
								type="button"
								onClick={() => clearCart.mutate()}
								className="w-full text-center text-xs text-muted-foreground hover:underline"
							>
								Sepeti Boşalt
							</button>
						</div>
					</>
				)}
			</SheetContent>
		</Sheet>
	);
}
