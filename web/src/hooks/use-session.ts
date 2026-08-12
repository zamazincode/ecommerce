"use client";

import { queryKeys } from "@/lib/api/query-keys";
import { components } from "@/types/api";
import { useQuery } from "@tanstack/react-query";

type UserDto = components["schemas"]["UserDto"];

export default function useSession() {
	return useQuery({
		queryKey: queryKeys.session,
		queryFn: async () => {
			const res = await fetch("/api/auth/me");
			const data = (await res.json()) as { user: UserDto | null };
			return data.user;
		},
		// 5dk
		staleTime: 5 * 60 * 1000,
	});
}
