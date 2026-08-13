"use client";

import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { Button } from "@/components/ui/button";

export function CategoryFilters() {
	const router = useRouter();
	const pathname = usePathname();
	const searchParams = useSearchParams();

	function setFilter(key: string, value: string | null) {
		const params = new URLSearchParams(searchParams.toString());
		if (value === null) {
			params.delete(key);
		} else {
			params.set(key, value);
		}
		// Filtre değişince sayfa 1'e dön — 3. sayfadayken fiyat filtresi
		// değiştirip "sayfa bulunamadı" görmesin.
		params.delete("page");
		router.push(`${pathname}?${params.toString()}`);
	}

	const inStockOnly = searchParams.get("inStock") === "true";

	return (
		<aside className="space-y-4">
			<div>
				<h3 className="text-sm font-medium">Stok</h3>
				<Button
					variant={inStockOnly ? "default" : "outline"}
					size="sm"
					className="mt-2"
					onClick={() =>
						setFilter("inStock", inStockOnly ? null : "true")
					}
				>
					Sadece stoktakiler
				</Button>
			</div>

			<div>
				<h3 className="text-sm font-medium">Sıralama</h3>
				<select
					className="mt-2 w-full rounded-md border border-input bg-background px-2 py-1 text-sm"
					value={searchParams.get("sortBy") ?? ""}
					onChange={(e) =>
						setFilter("sortBy", e.target.value || null)
					}
				>
					<option value="">Varsayılan</option>
					<option value="price">Fiyat</option>
					<option value="newest">En Yeni</option>
				</select>
			</div>
		</aside>
	);
}
