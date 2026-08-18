"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import useSession from "@/hooks/use-session";
import type { components } from "@/types/api";

type ProductListDto = components["schemas"]["ProductListDto"];

const listKey = ["favorites"] as const;
const idsKey = ["favorites", "ids"] as const;

export function useFavorites() {
	const { data: user } = useSession();
	return useQuery({
		queryKey: listKey,
		queryFn: () => apiFetch<ProductListDto[]>("favorites"),
		// `/api/favorites` 401 istiyor — GİRİŞSİZ kullanıcı için bu bir hata
		// değil, beklenen durum. `enabled` ile baştan hiç çağırmıyoruz;
		// aksi hâlde her istek 401 ile reddedilir ve TanStack Query'nin
		// merkezi `onError`'ı (Step 13) "Veri yüklenemedi." toast'ı atardı.
		enabled: !!user,
	});
}

/** Ürün kartlarında "dolu mu boş mu kalp" göstermek için — hafif, ayrı sorgu. */
export function useFavoriteIds() {
	const { data: user } = useSession();
	return useQuery({
		queryKey: idsKey,
		queryFn: () => apiFetch<number[]>("favorites/ids"),
		// bkz. useFavorites'teki aynı not — bu hook HER ürün kartında
		// (FavoriteButton üzerinden) çalışıyor, girişsiz ziyaretçiler dahil.
		enabled: !!user,
	});
}

export function useToggleFavorite() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: ({
			productId,
			isFavorited,
		}: {
			productId: number;
			isFavorited: boolean;
		}) =>
			isFavorited
				? apiFetch(`favorites/${productId}`, { method: "DELETE" })
				: apiFetch(`favorites/${productId}`, { method: "POST" }),

		// OPTIMISTIC: kalp ikonu anında değişsin.
		onMutate: async ({ productId, isFavorited }) => {
			await queryClient.cancelQueries({ queryKey: idsKey });
			const previous = queryClient.getQueryData<number[]>(idsKey);

			queryClient.setQueryData<number[]>(idsKey, (old = []) =>
				isFavorited
					? old.filter((id) => id !== productId)
					: [...old, productId],
			);

			return { previous };
		},
		onError: (_err, _vars, context) => {
			if (context?.previous) queryClient.setQueryData(idsKey, context.previous);
		},
		onSettled: () => {
			queryClient.invalidateQueries({ queryKey: idsKey });
			queryClient.invalidateQueries({ queryKey: listKey });
		},
	});
}
