"use client";

import { useCart } from "@/hooks/use-cart";
import { useUiStore } from "@/stores/ui-store";
import { Button } from "@/components/ui/button";
import { ShoppingCartIcon } from "lucide-react";

export function CartButton() {
	const { data: cart } = useCart();
	const openCartPanel = useUiStore((s) => s.openCartPanel);

	const quantity = (cart?.totalQuantity as number) || 0;

	return (
		<Button
			variant="ghost"
			size="lg"
			onClick={openCartPanel}
			aria-label="Sepeti aç"
			className="relative flex h-auto flex-col gap-0.5 py-1.5 text-[11px] font-normal"
		>
			<ShoppingCartIcon className="size-5" />
			<span className="hidden text-foreground xl:block">Sepetim</span>
			{quantity > 0 ? (
				<span
					data-testid="cart-count"
					className="absolute -top-0.5 -right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-red px-1 text-[11px] font-semibold text-red-foreground ring-2 ring-background"
				>
					{quantity > 99 ? "99+" : quantity}
				</span>
			) : null}
		</Button>
	);
}
