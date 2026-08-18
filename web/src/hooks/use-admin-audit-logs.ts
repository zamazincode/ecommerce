"use client";

import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { AdminAuditLogFilters } from "@/types";
import type { components } from "@/types/api";

type PagedResultOfAuditLogDto =
	components["schemas"]["PagedResultOfAuditLogDto"];

export type { AdminAuditLogFilters };

export function useAdminAuditLogs(filters: AdminAuditLogFilters) {
	const params = new URLSearchParams();
	for (const [key, value] of Object.entries(filters)) {
		if (value) params.set(key, String(value));
	}

	return useQuery({
		queryKey: queryKeys.adminAuditLogs(filters),
		queryFn: () =>
			apiFetch<PagedResultOfAuditLogDto>(
				`admin/audit-logs?${params.toString()}`,
			),
	});
}
