export const queryKeys = {
	session: ["session"] as const,
	products: (filters: Record<string, unknown>) =>
		["products", filters] as const,
	product: (slug: string) => ["product", slug] as const,
	cart: ["cart"] as const,
	orders: (page: number) => ["orders", page] as const,
};
