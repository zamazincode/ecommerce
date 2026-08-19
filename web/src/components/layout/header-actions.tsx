"use client";

import Link from "next/link";
import { HeartIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { CartButton } from "@/components/cart/cart-button";
import { useFavoriteIds } from "@/hooks/use-favorites";
import { AccountMenu } from "./account-menu";

/**
 * Header'ın sağ tarafı — hesap/favoriler/sepet. Üçü de aynı görsel dili
 * paylaşıyor (dr.com.tr'deki "ikon üstte, yazı altta" düzeni), o yüzden tek
 * client bileşende toplandı.
 */
export function HeaderActions() {
	const { data: favoriteIds } = useFavoriteIds();
	const favoriteCount = favoriteIds?.length ?? 0;

	return (
		<div className="ml-auto flex shrink-0 items-center gap-1">
			<AccountMenu />
			<Button
				variant="ghost"
				size="lg"
				className="relative flex h-auto flex-col gap-0.5 py-1.5 text-[11px] font-normal"
				render={<Link href="/hesabim/favorilerim" />}
				nativeButton={false}
			>
				<HeartIcon className="size-5" />
				<span className="hidden text-foreground xl:inline">Favorilerim</span>
				{favoriteCount > 0 ? (
					<span className="absolute -top-0.5 -right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-red px-1 text-[11px] font-semibold text-red-foreground ring-2 ring-background">
						{favoriteCount > 99 ? "99+" : favoriteCount}
					</span>
				) : null}
			</Button>
			<CartButton />
		</div>
	);
}
