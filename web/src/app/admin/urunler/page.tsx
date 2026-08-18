"use client";

import { useState } from "react";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import {
	useAdminProducts,
	useUpdateStock,
	useDeleteProduct,
	useRestoreProduct,
	useBulkUpdatePrice,
} from "@/hooks/use-admin-products";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/toast";

export default function AdminProductsPage() {
	const router = useRouter();
	const pathname = usePathname();
	const searchParams = useSearchParams();

	const q = searchParams.get("q") ?? "";
	const page = Number(searchParams.get("page") ?? "1");
	const sortBy =
		(searchParams.get("sortBy") as "price" | "name" | "newest" | null) ??
		"newest";
	const includeDeleted = searchParams.get("includeDeleted") === "true";

	const { data, isLoading } = useAdminProducts({
		q: q || undefined,
		page,
		sortBy,
		includeDeleted,
	});
	const updateStock = useUpdateStock();
	const deleteProduct = useDeleteProduct();
	const restoreProduct = useRestoreProduct();
	const bulkUpdate = useBulkUpdatePrice();

	const [editingStock, setEditingStock] = useState<{
		id: number;
		value: string;
	} | null>(null);

	const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
	const [bulkPercentage, setBulkPercentage] = useState("");

	function toggleSelected(id: number) {
		setSelectedIds((prev) => {
			const next = new Set(prev);
			if (next.has(id)) next.delete(id);
			else next.add(id);
			return next;
		});
	}

	async function handleBulkPriceIncrease() {
		const percentage = Number(bulkPercentage);
		if (selectedIds.size === 0 || !Number.isFinite(percentage) || percentage === 0)
			return;

		const items = (data?.items ?? [])
			.filter((p) => selectedIds.has(p.id as number))
			.map((p) => {
				const price = Number(p.price);
				const discountedPrice =
					p.discountedPrice != null ? Number(p.discountedPrice) : null;
				return {
					productId: p.id as number,
					price: Math.round(price * (1 + percentage / 100) * 100) / 100,
					discountedPrice:
						discountedPrice != null
							? Math.round(
									discountedPrice * (1 + percentage / 100) * 100,
								) / 100
							: null,
				};
			});

		try {
			const result = await bulkUpdate.mutateAsync(items);
			toast.add({
				title: `${result.updated} ürün güncellendi`,
				type: "success",
			});
			setSelectedIds(new Set());
			setBulkPercentage("");
		} catch {
			// Backend'in kilidi: bir ürün bu sırada başka biri tarafından değiştiyse
			// (xmin çakışması) TÜM istek 409 ile reddedilir, HİÇBİR ürün değişmez.
			toast.add({
				title:
					"Güncelleme başarısız — hiçbir ürün değişmedi, bir ürün başka biri tarafından değiştirilmiş olabilir",
				type: "error",
			});
		}
	}

	// KONTROLLÜ input — `q` URL'den geliyor ve her tuş vuruşunda değişiyor;
	// `defaultValue` kullanmak Base UI'ın "uncontrolled alanın varsayılan
	// değeri sonradan değişti" uyarısını tetikliyordu. Senkron, render
	// sırasında yapılıyor (React'in önerdiği "adjusting state during
	// rendering" deseni) — URL DIŞARIDAN değiştiğinde (geri/ileri tuşu) bir
	// `useEffect`'e gerek kalmadan yakalanıyor.
	const [searchValue, setSearchValue] = useState(q);
	const [prevQ, setPrevQ] = useState(q);
	if (q !== prevQ) {
		setPrevQ(q);
		setSearchValue(q);
	}

	function setParam(key: string, value: string | null) {
		const params = new URLSearchParams(searchParams.toString());
		if (value) params.set(key, value);
		else params.delete(key);
		// filtre değişince sayfa 1'e dön — ama "page"in KENDİSİ değişiyorsa
		// (pagination butonları) burada silme, aksi hâlde "Sonraki" hiçbir şey
		// yapmıyormuş gibi görünür (set edip hemen siliyordu).
		if (key !== "page") params.delete("page");
		router.push(`${pathname}?${params.toString()}`);
	}

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<h1 className="text-xl font-semibold">Ürünler</h1>
				<Button>
					<Link href="/admin/urunler/yeni">Yeni Ürün</Link>
				</Button>
			</div>

			<div className="mb-4 flex gap-2">
				<Input
					placeholder="Ad veya SKU ara…"
					value={searchValue}
					onChange={(e) => {
						setSearchValue(e.target.value);
						setParam("q", e.target.value || null);
					}}
				/>
				<label className="flex items-center gap-2 text-sm whitespace-nowrap">
					<input
						type="checkbox"
						checked={includeDeleted}
						onChange={(e) =>
							setParam(
								"includeDeleted",
								e.target.checked ? "true" : null,
							)
						}
					/>
					Silinenleri de göster
				</label>
			</div>

			{selectedIds.size > 0 ? (
				<div className="mb-4 flex items-center gap-2 rounded-md border bg-muted/30 p-3 text-sm">
					<span>{selectedIds.size} ürün seçili</span>
					<Input
						type="number"
						placeholder="% artış (negatif = indirim)"
						className="w-56"
						value={bulkPercentage}
						onChange={(e) => setBulkPercentage(e.target.value)}
					/>
					<Button
						size="sm"
						disabled={bulkUpdate.isPending || !bulkPercentage}
						onClick={handleBulkPriceIncrease}
					>
						Uygula
					</Button>
					<Button
						variant="ghost"
						size="sm"
						onClick={() => setSelectedIds(new Set())}
					>
						Seçimi Temizle
					</Button>
				</div>
			) : null}

			{isLoading ? (
				<Skeleton className="h-64 w-full" />
			) : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead className="w-8">
								<input
									type="checkbox"
									aria-label="Tümünü seç"
									checked={
										!!data?.items.length &&
										data.items.every((p) =>
											selectedIds.has(p.id as number),
										)
									}
									onChange={(e) =>
										setSelectedIds(
											e.target.checked
												? new Set(
														data?.items.map(
															(p) => p.id as number,
														),
													)
												: new Set(),
										)
									}
								/>
							</TableHead>
							<TableHead>Ad</TableHead>
							<TableHead>SKU</TableHead>
							<TableHead>Fiyat</TableHead>
							<TableHead>Stok</TableHead>
							<TableHead>Durum</TableHead>
							<TableHead className="text-right">
								İşlemler
							</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{data?.items.map((product) => (
							<TableRow key={product.id as number}>
								<TableCell>
									<input
										type="checkbox"
										aria-label={`${product.name} seç`}
										checked={selectedIds.has(product.id as number)}
										onChange={() =>
											toggleSelected(product.id as number)
										}
									/>
								</TableCell>
								<TableCell>{product.name}</TableCell>
								<TableCell className="text-muted-foreground">
									{product.sku ?? "—"}
								</TableCell>
								<TableCell>
									{product.discountedPrice ?? product.price} ₺
								</TableCell>
								<TableCell>
									{editingStock?.id ===
									(product.id as number) ? (
										<Input
											type="number"
											autoFocus
											className="w-20"
											value={editingStock.value}
											onChange={(e) =>
												setEditingStock({
													id: product.id as number,
													value: e.target.value,
												})
											}
											onBlur={() => {
												const stock = Number(
													editingStock.value,
												);
												if (
													!Number.isNaN(stock) &&
													stock >= 0 &&
													stock !== product.stock
												) {
													updateStock.mutate({
														id: product.id as number,
														stock,
													});
												}
												setEditingStock(null);
											}}
											onKeyDown={(e) =>
												e.key === "Enter" &&
												e.currentTarget.blur()
											}
										/>
									) : (
										<button
											className="underline decoration-dotted"
											onClick={() =>
												setEditingStock({
													id: product.id as number,
													value: String(
														product.stock,
													),
												})
											}
										>
											{product.stock}
										</button>
									)}
								</TableCell>
								<TableCell>
									{product.deletedAt ? (
										<Badge variant="destructive">
											Silindi
										</Badge>
									) : product.isActive ? (
										<Badge>Aktif</Badge>
									) : (
										<Badge variant="secondary">Pasif</Badge>
									)}
								</TableCell>
								<TableCell className="text-right space-x-2">
									<Button variant="ghost" size="sm">
										<Link
											href={`/admin/urunler/${product.id as number}`}
										>
											Düzenle
										</Link>
									</Button>
									{product.deletedAt ? (
										<Button
											variant="ghost"
											size="sm"
											onClick={() =>
												restoreProduct.mutate(
													product.id as number,
												)
											}
										>
											Geri Al
										</Button>
									) : (
										<Button
											variant="ghost"
											size="sm"
											onClick={() => {
												if (
													confirm(
														`"${product.name}" silinsin mi?`,
													)
												)
													deleteProduct.mutate(
														product.id as number,
													);
											}}
										>
											Sil
										</Button>
									)}
								</TableCell>
							</TableRow>
						))}
					</TableBody>
				</Table>
			)}

			{data ? (
				<div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
					<span>
						Toplam {data.totalCount} ürün — sayfa {data.page}/
						{data.totalPages}
					</span>
					<div className="space-x-2">
						<Button
							variant="outline"
							size="sm"
							disabled={!data.hasPrevious}
							onClick={() => setParam("page", String(page - 1))}
						>
							Önceki
						</Button>
						<Button
							variant="outline"
							size="sm"
							disabled={!data.hasNext}
							onClick={() => setParam("page", String(page + 1))}
						>
							Sonraki
						</Button>
					</div>
				</div>
			) : null}
		</div>
	);
}
