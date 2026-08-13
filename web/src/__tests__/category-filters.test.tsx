import { expect, test, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const push = vi.fn();
vi.mock("next/navigation", () => ({
	useRouter: () => ({ push }),
	usePathname: () => "/kategori/roman",
	useSearchParams: () => new URLSearchParams(),
}));

import { CategoryFilters } from "@/components/product/category-filters";

test("stok filtresine tıklayınca doğru URL'e yönlendirir", () => {
	render(<CategoryFilters />);

	fireEvent.click(screen.getByText("Sadece stoktakiler"));

	expect(push).toHaveBeenCalledWith("/kategori/roman?inStock=true");
});
