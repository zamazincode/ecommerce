"use client";

import { apiFetch, ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import { components } from "@/types/api";
import { useQuery } from "@tanstack/react-query";

type UserDto = components["schemas"]["UserDto"];

export default function useSession() {
	return useQuery({
		queryKey: queryKeys.session,
		queryFn: async () => {
			try {
				return await apiFetch<UserDto>("auth/me");
			} catch (error) {
				// 401: giriş yapılmamış, bu bir hata değil
				if (error instanceof ApiError && error.status === 401) {
					return null;
				}
				throw error;
			}
		},
		// 5dk
		staleTime: 5 * 60 * 1000,
	});
}
