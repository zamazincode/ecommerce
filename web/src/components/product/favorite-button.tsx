"use client";

import { useFavoriteIds, useToggleFavorite } from "@/hooks/use-favorites";
import useSession from "@/hooks/use-session";
import { useRouter } from "next/navigation";
import { HeartIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

export function FavoriteButton({
	productId,
	variant = "icon",
	className,
}: {
	productId: number;
	/** `icon` — ürün kartındaki mutlak konumlu simge (varsayılan). `expanded` — ürün detayı satın alma kartındaki tam genişlik buton. */
	variant?: "icon" | "expanded";
	className?: string;
}) {
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

	if (variant === "expanded") {
		return (
			<Button
				type="button"
				variant="outline"
				className={cn("w-full", className)}
				onClick={handleClick}
			>
				<HeartIcon
					className={cn(
						"size-4",
						isFavorited && "fill-red-500 text-red-500",
					)}
				/>
				{isFavorited ? "Favorilerden Çıkar" : "Favorilere Ekle"}
			</Button>
		);
	}

	return (
		<button
			onClick={handleClick}
			aria-label={isFavorited ? "Favorilerden çıkar" : "Favorilere ekle"}
			className={cn(
				"absolute top-2 right-2 z-10 rounded-full bg-background/80 p-1.5 backdrop-blur",
				className,
			)}
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
