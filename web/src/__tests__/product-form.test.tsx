import { describe, expect, test, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

vi.mock("next/navigation", () => ({
	useRouter: () => ({ push: vi.fn() }),
}));

import { ProductForm } from "@/components/admin/product-form";

function renderWithProviders(ui: React.ReactElement) {
	const queryClient = new QueryClient({
		defaultOptions: { queries: { retry: false } },
	});
	queryClient.setQueryData(
		["admin", "categories-flat"],
		[{ id: 1, name: "Roman", slug: "roman", parentId: null, displayOrder: 0 }],
	);
	queryClient.setQueryData(["admin", "publishers"], []);
	return render(
		<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
	);
}

describe("ProductForm", () => {
	test("sunucunun 400 döndürdüğü alan hatası, ilgili input'un altında görünür", async () => {
		global.fetch = vi.fn().mockResolvedValue({
			ok: false,
			status: 400,
			json: () =>
				Promise.resolve({
					errors: {
						DiscountedPrice: [
							"İndirimli fiyat, normal fiyattan düşük olmalı.",
						],
					},
				}),
		});

		renderWithProviders(<ProductForm />);

		await userEvent.type(screen.getByLabelText("Ad"), "Test Kitap");
		await userEvent.type(screen.getByLabelText("Fiyat"), "50");
		await userEvent.selectOptions(screen.getByLabelText("Kategori"), "1");
		await userEvent.click(screen.getByRole("button", { name: /oluştur/i }));

		await waitFor(() => {
			expect(
				screen.getByText(
					"İndirimli fiyat, normal fiyattan düşük olmalı.",
				),
			).toBeDefined();
		});
	});
});
