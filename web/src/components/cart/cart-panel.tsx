"use client";

import Link from "next/link";
import { useUiStore } from "@/stores/ui-store";
import {
	useCart,
	useUpdateCartItem,
	useRemoveCartItem,
	useClearCart,
} from "@/hooks/use-cart";
import {
	Sheet,
	SheetContent,
	SheetHeader,
	SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { TrashIcon } from "lucide-react";

export function CartPanel() {
	const isOpen = useUiStore((s) => s.isCartPanelOpen);
	const closeCartPanel = useUiStore((s) => s.closeCartPanel);
	const { data: cart, isLoading } = useCart();
	const updateItem = useUpdateCartItem();
	const removeItem = useRemoveCartItem();
	const clearCart = useClearCart();

	return (
		<Sheet open={isOpen} onOpenChange={(open) => !open && closeCartPanel()}>
			<SheetContent
				side="right"
				className="flex w-full flex-col sm:max-w-md"
			>
				<SheetHeader>
					<SheetTitle>Sepetim</SheetTitle>
				</SheetHeader>

				{isLoading ? (
					<Skeleton className="h-64 flex-1" />
				) : !cart || cart.items.length === 0 ? (
					<div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
						<p className="text-muted-foreground">Sepetin boş.</p>
						<Button onClick={closeCartPanel}>
							<Link href={"/"}>Alışverişe Başla</Link>
						</Button>
					</div>
				) : (
					<>
						<ul className="flex-1 space-y-4 overflow-y-auto py-4">
							{cart.items.map((item) => (
								<li key={item.productId} className="flex gap-3">
									<div className="flex-1">
										<p className="text-sm font-medium">
											{item.name}
										</p>
										{item.priceChanged ? (
											<p className="text-xs text-amber-600">
												Fiyat değişti
											</p>
										) : null}
										<div className="mt-1 flex items-center gap-2">
											<input
												type="number"
												min={1}
												max={item.availableStock}
												value={item.quantity}
												onChange={(e) => {
													const quantity = Number(
														e.target.value,
													);
													if (quantity >= 1)
														updateItem.mutate({
															productId:
																item.productId as number,
															quantity,
														});
												}}
												className="w-16 rounded border px-2 py-1 text-sm"
											/>
											<button
												onClick={() =>
													removeItem.mutate(
														item.productId as number,
													)
												}
												className="text-muted-foreground hover:text-destructive"
												aria-label={`${item.name} ürününü sepetten çıkar`}
											>
												<TrashIcon className="size-4" />
											</button>
										</div>
									</div>
									<p className="text-sm font-medium">
										{item.lineTotal} ₺
									</p>
								</li>
							))}
						</ul>

						{cart.warnings.length > 0 ? (
							<div className="mb-3 rounded-md bg-amber-50 p-2 text-xs text-amber-800">
								{cart.warnings.map((w, i) => (
									<p key={i}>{w}</p>
								))}
							</div>
						) : null}

						<div className="space-y-2 border-t pt-4">
							<div className="flex justify-between text-sm">
								<span>Ara Toplam</span>
								<span>{cart.subTotal} ₺</span>
							</div>
							{(cart.discountAmount as number) > 0 ? (
								<div className="flex justify-between text-sm text-muted-foreground">
									<span>İndirim</span>
									<span>-{cart.discountAmount} ₺</span>
								</div>
							) : null}
							<div className="flex justify-between font-semibold">
								<span>Toplam</span>
								<span>{cart.total} ₺</span>
							</div>

							<Button className="w-full" onClick={closeCartPanel}>
								<Link href={"/odeme"}>Ödemeye Geç</Link>
							</Button>
							<button
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
