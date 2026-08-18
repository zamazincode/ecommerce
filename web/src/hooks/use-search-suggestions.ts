"use client";

import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import type { components } from "@/types/api";

type SuggestionDto = components["schemas"]["SuggestionDto"];

export function useSearchSuggestions(term: string) {
	return useQuery({
		queryKey: ["search-suggest", term],
		queryFn: () =>
			apiFetch<SuggestionDto[]>(
				`search/suggest?q=${encodeURIComponent(term)}`,
			),
		enabled: term.trim().length >= 2,
		// Yazarken her tuş vuruşunda istek atmamak için — TanStack Query'nin
		// KENDİ debounce'u yok, `staleTime` de bunu çözmüyor (her karakter
		// FARKLI bir query key = farklı istek). Gerçek debounce SearchBar'da,
		// bu hook'un DIŞINDA yapılıyor.
	});
}
