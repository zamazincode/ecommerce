"use client";

import { useState, type FormEvent } from "react";
import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { SlidersHorizontalIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { NativeSelect } from "@/components/ui/native-select";
import {
	Accordion,
	AccordionItem,
	AccordionTrigger,
	AccordionContent,
} from "@/components/ui/accordion";
import {
	Sheet,
	SheetTrigger,
	SheetContent,
	SheetHeader,
	SheetTitle,
	SheetFooter,
} from "@/components/ui/sheet";
import { cn } from "@/lib/utils";

interface CategoryFiltersProps {
	className?: string;
	/**
	 * `/api/search` (Features/Search/SearchContracts.cs) `sortBy`/`sortDir`
	 * almıyor — yalnızca `/api/categories/{slug}/products` ve `/api/products`
	 * (ProductFilterRequest) destekliyor. Arama sayfası bu yüzden Sıralama
	 * bölümünü hiç göstermez; çalışmayan bir kontrol sahte olurdu.
	 */
	showSort?: boolean;
}

const SORT_LABELS: Record<string, string> = {
	price: "Fiyata göre",
	newest: "En yeni",
};

/**
 * Kategori ve arama sayfalarının ortak filtre paneli. Durumu tamamen URL'de
 * tutar (`useSearchParams`) — bileşen kendi state'ini taşımaz, bu sayede geri/
 * ileri tuşları ve paylaşılan linkler filtreleri korur.
 */
export function CategoryFilters({
	className,
	showSort = true,
}: CategoryFiltersProps) {
	const router = useRouter();
	const pathname = usePathname();
	const searchParams = useSearchParams();
	const [mobileOpen, setMobileOpen] = useState(false);

	function applyParams(updates: Record<string, string | null>) {
		const params = new URLSearchParams(searchParams.toString());
		for (const [key, value] of Object.entries(updates)) {
			if (value === null || value === "") {
				params.delete(key);
			} else {
				params.set(key, value);
			}
		}
		// Filtre değişince sayfa 1'e dön — 3. sayfadayken fiyat filtresi
		// değiştirip "sayfa bulunamadı" görmesin.
		params.delete("page");
		router.push(`${pathname}?${params.toString()}`);
		setMobileOpen(false);
	}

	function setFilter(key: string, value: string | null) {
		applyParams({ [key]: value });
	}

	function handlePriceSubmit(event: FormEvent<HTMLFormElement>) {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const minPrice = String(form.get("minPrice") ?? "").trim();
		const maxPrice = String(form.get("maxPrice") ?? "").trim();
		applyParams({
			minPrice: minPrice || null,
			maxPrice: maxPrice || null,
		});
	}

	function clearAll() {
		applyParams({
			minPrice: null,
			maxPrice: null,
			showOutOfStock: null,
			sortBy: null,
		});
	}

	const minPrice = searchParams.get("minPrice") ?? "";
	const maxPrice = searchParams.get("maxPrice") ?? "";
	const showOutOfStock = searchParams.get("showOutOfStock") === "true";
	const sortBy = searchParams.get("sortBy") ?? "";

	const chips: { key: string; label: string }[] = [];
	if (minPrice) chips.push({ key: "minPrice", label: `Min ${minPrice} ₺` });
	if (maxPrice) chips.push({ key: "maxPrice", label: `Maks ${maxPrice} ₺` });
	if (showOutOfStock) {
		chips.push({ key: "showOutOfStock", label: "Stokta olmayanlar dahil" });
	}
	if (showSort && sortBy) {
		chips.push({ key: "sortBy", label: SORT_LABELS[sortBy] ?? sortBy });
	}

	function renderGroups(idPrefix: string) {
		return (
			<Accordion
				multiple
				defaultValue={showSort ? ["price", "stock", "sort"] : ["price", "stock"]}
			>
				<AccordionItem value="price">
					<AccordionTrigger>Fiyat</AccordionTrigger>
					<AccordionContent>
						<form
							onSubmit={handlePriceSubmit}
							className="flex items-end gap-2"
						>
							<div className="flex-1 space-y-1">
								<label
									htmlFor={`${idPrefix}-min-price`}
									className="text-xs text-muted-foreground"
								>
									Min ₺
								</label>
								<Input
									id={`${idPrefix}-min-price`}
									name="minPrice"
									type="number"
									min={0}
									inputMode="decimal"
									defaultValue={minPrice}
									placeholder="0"
								/>
							</div>
							<div className="flex-1 space-y-1">
								<label
									htmlFor={`${idPrefix}-max-price`}
									className="text-xs text-muted-foreground"
								>
									Maks ₺
								</label>
								<Input
									id={`${idPrefix}-max-price`}
									name="maxPrice"
									type="number"
									min={0}
									inputMode="decimal"
									defaultValue={maxPrice}
									placeholder="1000"
								/>
							</div>
							<Button type="submit" variant="soft" size="sm">
								Uygula
							</Button>
						</form>
					</AccordionContent>
				</AccordionItem>

				<AccordionItem value="stock">
					<AccordionTrigger>Stok</AccordionTrigger>
					<AccordionContent>
						<div className="flex items-center gap-2">
							<Checkbox
								id={`${idPrefix}-show-out-of-stock`}
								checked={showOutOfStock}
								onCheckedChange={(checked) =>
									setFilter("showOutOfStock", checked ? "true" : null)
								}
							/>
							<label
								htmlFor={`${idPrefix}-show-out-of-stock`}
								className="text-sm"
							>
								Stokta olmayanları da göster
							</label>
						</div>
					</AccordionContent>
				</AccordionItem>

				{showSort ? (
					<AccordionItem value="sort">
						<AccordionTrigger>Sıralama</AccordionTrigger>
						<AccordionContent>
							<NativeSelect
								value={sortBy}
								onChange={(e) => setFilter("sortBy", e.target.value || null)}
							>
								<option value="">Varsayılan</option>
								<option value="price">Fiyat</option>
								<option value="newest">En Yeni</option>
							</NativeSelect>
						</AccordionContent>
					</AccordionItem>
				) : null}
			</Accordion>
		);
	}

	return (
		<div className={cn("space-y-3", className)}>
			{chips.length > 0 ? (
				<div className="flex flex-wrap items-center gap-2">
					{chips.map((chip) => (
						<button
							key={chip.key}
							type="button"
							onClick={() => setFilter(chip.key, null)}
							className="inline-flex items-center gap-1 rounded-full bg-primary-soft px-3 py-1 text-xs font-medium text-primary hover:bg-primary-soft-strong"
						>
							{chip.label}
							<XIcon className="size-3.5" />
						</button>
					))}
					<button
						type="button"
						onClick={clearAll}
						className="text-xs font-medium text-muted-foreground underline underline-offset-2 hover:text-foreground"
					>
						Tümünü temizle
					</button>
				</div>
			) : null}

			{/* Masaüstü: sticky kart */}
			<aside className="sticky top-24 hidden w-64 shrink-0 rounded-2xl border border-border p-4 lg:block">
				{renderGroups("desktop")}
			</aside>

			{/* Mobil/tablet: alttan açılan Sheet */}
			<div className="lg:hidden">
				<Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
					<SheetTrigger
						render={<Button variant="outline" className="w-full" />}
					>
						<SlidersHorizontalIcon />
						Filtrele &amp; Sırala
					</SheetTrigger>
					<SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto">
						<SheetHeader>
							<SheetTitle>Filtrele &amp; Sırala</SheetTitle>
						</SheetHeader>
						<div className="px-4">{renderGroups("mobile")}</div>
						<SheetFooter>
							<Button variant="ghost" className="w-full" onClick={clearAll}>
								Filtreleri temizle
							</Button>
						</SheetFooter>
					</SheetContent>
				</Sheet>
			</div>
		</div>
	);
}
