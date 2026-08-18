"use client";

import { useCart } from "@/hooks/use-cart";
import { useUiStore } from "@/stores/ui-store";
import { Button } from "@/components/ui/button";
import { ShoppingCartIcon } from "lucide-react";

export function CartButton() {
	const { data: cart } = useCart();
	const openCartPanel = useUiStore((s) => s.openCartPanel);

	return (
		<Button
			variant="ghost"
			size="sm"
			onClick={openCartPanel}
			aria-label="Sepeti aç"
			className="relative"
		>
			<ShoppingCartIcon className="size-5" />
			{cart && (cart.totalQuantity as number) > 0 ? (
				<span className="absolute -right-1 -top-1 flex size-4 items-center justify-center rounded-full bg-primary text-[10px] text-primary-foreground">
					{cart.totalQuantity}
				</span>
			) : null}
		</Button>
	);
}
