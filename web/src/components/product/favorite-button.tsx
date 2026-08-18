"use client";

import { useFavoriteIds, useToggleFavorite } from "@/hooks/use-favorites";
import useSession from "@/hooks/use-session";
import { useRouter } from "next/navigation";
import { HeartIcon } from "lucide-react";
import { cn } from "@/lib/utils";

export function FavoriteButton({ productId }: { productId: number }) {
	const router = useRouter();
	const { data: user } = useSession();
	const { data: favoriteIds } = useFavoriteIds();
	const toggle = useToggleFavorite();

	const isFavorited = favoriteIds?.includes(productId) ?? false;

	function handleClick(e: React.MouseEvent) {
		e.preventDefault(); // ürün kartının içindeki <Link>'e tıklamayı engelle
		e.stopPropagation();

		if (!user) {
			router.push(
				`/giris?returnUrl=${encodeURIComponent(window.location.pathname)}`,
			);
			return;
		}

		toggle.mutate({ productId, isFavorited });
	}

	return (
		<button
			onClick={handleClick}
			aria-label={isFavorited ? "Favorilerden çıkar" : "Favorilere ekle"}
			className="absolute top-2 right-2 z-10 rounded-full bg-background/80 p-1.5 backdrop-blur"
		>
			<HeartIcon
				className={cn(
					"size-4",
					isFavorited ? "fill-red-500 text-red-500" : "text-muted-foreground",
				)}
			/>
		</button>
	);
}
