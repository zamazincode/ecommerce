import type { components } from "./api";

export type ProductListDto = components["schemas"]["ProductListDto"];
export type ProductDetailDto = components["schemas"]["ProductDetailDto"];
export type PagedResultOfProductListDto =
	components["schemas"]["PagedResultOfProductListDto"];
export type CartDto = components["schemas"]["CartDto"];
export type OrderDetailDto = components["schemas"]["OrderDetailDto"];
export type AuthResponse = components["schemas"]["AuthResponse"];

export interface AdminProductFilters {
	q?: string;
	categoryId?: number;
	isActive?: boolean;
	includeDeleted?: boolean;
	sortBy?: "price" | "name" | "newest";
	sortDir?: "asc" | "desc";
	page?: number;
	pageSize?: number;
}

export interface AdminOrderFilters {
	status?: components["schemas"]["OrderStatus"];
	dateFrom?: string; // "yyyy-MM-dd" — DateOnly, backend Kind=Utc tuzağı yüzünden ZAMAN DAMGASI DEĞİL
	dateTo?: string;
	q?: string;
	page?: number;
}

export interface AdminReviewFilters {
	onlyPending: boolean;
	page?: number;
}

export interface AdminAuditLogFilters {
	entityType?: string;
	action?: string;
	page?: number;
}
