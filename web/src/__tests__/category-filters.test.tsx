import { afterEach, expect, test, vi } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";

const push = vi.fn();
let params = new URLSearchParams();
vi.mock("next/navigation", () => ({
	useRouter: () => ({ push }),
	usePathname: () => "/kategori/roman",
	useSearchParams: () => params,
}));

import { CategoryFilters } from "@/components/product/category-filters";

// vitest.config.mts `test.globals`'ı açmıyor — @testing-library/react'in
// otomatik `afterEach(cleanup)` algılaması bu yüzden devreye girmiyor ve
// önceki testin DOM'u sonrakine sızıp "found multiple elements" hatası
// veriyor. Elle temizle.
afterEach(cleanup);

test("varsayılan hâlde sadece stoktaki ürünler gösterilir — checkbox işaretsizdir", () => {
	params = new URLSearchParams();
	render(<CategoryFilters />);

	const checkbox = screen.getByRole("checkbox", {
		name: "Stokta olmayanları da göster",
	});
	expect(checkbox.getAttribute("aria-checked")).toBe("false");
});

test("\"stokta olmayanları da göster\" işaretlenince URL'e showOutOfStock=true eklenir", () => {
	params = new URLSearchParams();
	render(<CategoryFilters />);

	fireEvent.click(
		screen.getByRole("checkbox", { name: "Stokta olmayanları da göster" }),
	);

	expect(push).toHaveBeenCalledWith("/kategori/roman?showOutOfStock=true");
});

test("checkbox zaten işaretliyken kaldırılınca showOutOfStock URL'den silinir", () => {
	params = new URLSearchParams("showOutOfStock=true");
	render(<CategoryFilters />);

	const checkbox = screen.getByRole("checkbox", {
		name: "Stokta olmayanları da göster",
	});
	expect(checkbox.getAttribute("aria-checked")).toBe("true");

	fireEvent.click(checkbox);

	expect(push).toHaveBeenCalledWith("/kategori/roman?");
});

test("fiyat aralığı girilip \"Uygula\"ya basılınca minPrice/maxPrice URL'e eklenir", () => {
	params = new URLSearchParams();
	render(<CategoryFilters />);

	fireEvent.change(screen.getByLabelText("Min ₺"), {
		target: { value: "50" },
	});
	fireEvent.change(screen.getByLabelText("Maks ₺"), {
		target: { value: "200" },
	});
	fireEvent.click(screen.getByRole("button", { name: "Uygula" }));

	expect(push).toHaveBeenCalledWith(
		"/kategori/roman?minPrice=50&maxPrice=200",
	);
});

test("showSort=false iken Sıralama bölümü hiç render edilmez (arama sayfası)", () => {
	params = new URLSearchParams();
	render(<CategoryFilters showSort={false} />);

	expect(screen.queryByText("Sıralama")).toBeNull();
});
