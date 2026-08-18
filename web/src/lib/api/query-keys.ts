import {
	AdminProductFilters,
	AdminOrderFilters,
	AdminReviewFilters,
	AdminAuditLogFilters,
} from "@/types";

export const queryKeys = {
	session: ["session"] as const,
	products: (filters: Record<string, unknown>) =>
		["products", filters] as const,
	product: (slug: string) => ["product", slug] as const,
	cart: ["cart"] as const,
	orders: (page: number) => ["orders", page] as const,

	adminProducts: (filters: AdminProductFilters) =>
		["admin", "products", filters] as const,
	adminProduct: (id: number) => ["admin", "product", id] as const,
	adminOrders: (filters: AdminOrderFilters) =>
		["admin", "orders", filters] as const,
	adminCategories: ["admin", "categories-flat"] as const,
	adminPublishers: ["admin", "publishers"] as const,
	adminDashboard: ["admin", "dashboard"] as const,
	adminSales: (from: string, to: string, groupBy: string) =>
		["admin", "sales", from, to, groupBy] as const,

	// YENİ — Step 10
	adminBrands: ["admin", "brands"] as const,
	// `adminCategories`'ten KASITLI olarak ayrı bir key: o public
	// /api/categories'i (ProductCount YOK) çağırıyor, bu ise
	// /api/admin/categories'i (ProductCount VAR). Aynı key paylaşılırsa
	// TanStack Query ikisini aynı veri sanır, biri diğerinin cache'ini kirletir.
	adminCategoriesFull: ["admin", "categories-full"] as const,
	adminCoupons: ["admin", "coupons"] as const,
	adminReviews: (filters: AdminReviewFilters) =>
		["admin", "reviews", filters] as const,
	adminAuditLogs: (filters: AdminAuditLogFilters) =>
		["admin", "audit-logs", filters] as const,
};
