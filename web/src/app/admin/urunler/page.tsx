"use client";

import { useState } from "react";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { PackageIcon, PencilIcon, PlusIcon, RotateCcwIcon, SearchIcon, TrashIcon, XIcon } from "lucide-react";
import {
	useAdminProducts,
	useUpdateStock,
	useDeleteProduct,
	useRestoreProduct,
	useBulkUpdatePrice,
} from "@/hooks/use-admin-products";
import { PageHeader } from "@/components/admin/page-header";
import { RowAction, RowActions } from "@/components/admin/row-actions";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { NativeSelect } from "@/components/ui/native-select";
import { Price } from "@/components/ui/price";
import { Pagination } from "@/components/ui/pagination";
import { TableSkeleton } from "@/components/ui/data-state";
import { EmptyState } from "@/components/ui/empty-state";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { toast } from "@/components/ui/toast";

const SORT_OPTIONS: { value: string; label: string }[] = [
	{ value: "newest", label: "En Yeni" },
	{ value: "name", label: "İsme Göre" },
	{ value: "price", label: "Fiyata Göre" },
];

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
		<div className="space-y-6">
			<PageHeader
				title="Ürünler"
				description={data ? `${data.totalCount} ürün` : undefined}
				actions={
					<Button render={<Link href="/admin/urunler/yeni" />} nativeButton={false}>
						<PlusIcon />
						Yeni Ürün
					</Button>
				}
			/>

			<Card size="sm" className="flex-row flex-wrap items-center gap-3">
				<div className="relative max-w-sm flex-1">
					<SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						placeholder="Ad veya SKU ara…"
						value={searchValue}
						className="pl-9"
						onChange={(e) => {
							setSearchValue(e.target.value);
							setParam("q", e.target.value || null);
						}}
					/>
					{searchValue ? (
						<button
							type="button"
							aria-label="Aramayı temizle"
							className="absolute top-1/2 right-2.5 -translate-y-1/2 text-muted-foreground hover:text-foreground"
							onClick={() => {
								setSearchValue("");
								setParam("q", null);
							}}
						>
							<XIcon className="size-4" />
						</button>
					) : null}
				</div>

				<label className="flex items-center gap-2 text-sm whitespace-nowrap">
					<Checkbox
						checked={includeDeleted}
						onCheckedChange={(checked) =>
							setParam("includeDeleted", checked ? "true" : null)
						}
					/>
					Silinenleri de göster
				</label>

				<NativeSelect
					className="w-auto"
					value={sortBy}
					onChange={(e) => setParam("sortBy", e.target.value)}
				>
					{SORT_OPTIONS.map((option) => (
						<option key={option.value} value={option.value}>
							{option.label}
						</option>
					))}
				</NativeSelect>
			</Card>

			{selectedIds.size > 0 ? (
				<div className="flex flex-wrap items-center gap-3 rounded-xl border bg-primary-soft px-4 py-3 text-primary">
					<span className="text-sm font-medium">
						{selectedIds.size} ürün seçili
					</span>
					<Input
						type="number"
						placeholder="% artış (negatif = indirim)"
						className="w-56 bg-background"
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

			<Card className="overflow-hidden p-0">
				{isLoading ? (
					<TableSkeleton rows={10} cols={7} />
				) : !data || data.items.length === 0 ? (
					<EmptyState
						icon={PackageIcon}
						title="Ürün bulunamadı"
						description="Arama kriterlerine uyan ürün yok."
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead className="w-8">
									<Checkbox
										aria-label="Tümünü seç"
										checked={
											!!data.items.length &&
											data.items.every((p) =>
												selectedIds.has(p.id as number),
											)
										}
										onCheckedChange={(checked) =>
											setSelectedIds(
												checked
													? new Set(
															data.items.map(
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
							{data.items.map((product) => {
								const stock = Number(product.stock);
								return (
									<TableRow key={product.id as number} className="group/row">
										<TableCell>
											<Checkbox
												aria-label={`${product.name} seç`}
												checked={selectedIds.has(product.id as number)}
												onCheckedChange={() =>
													toggleSelected(product.id as number)
												}
											/>
										</TableCell>
										<TableCell>
											<p className="font-medium">{product.name}</p>
											<p className="text-xs text-muted-foreground">
												{product.slug}
											</p>
										</TableCell>
										<TableCell className="text-muted-foreground">
											{product.sku ?? "—"}
										</TableCell>
										<TableCell>
											<Price
												size="sm"
												price={Number(product.price)}
												discountedPrice={
													product.discountedPrice != null
														? Number(product.discountedPrice)
														: null
												}
											/>
										</TableCell>
										<TableCell>
											{editingStock?.id ===
											(product.id as number) ? (
												<Input
													type="number"
													autoFocus
													className="h-8 w-20"
													value={editingStock.value}
													onChange={(e) =>
														setEditingStock({
															id: product.id as number,
															value: e.target.value,
														})
													}
													onBlur={() => {
														const next = Number(
															editingStock.value,
														);
														if (
															!Number.isNaN(next) &&
															next >= 0 &&
															next !== stock
														) {
															updateStock.mutate({
																id: product.id as number,
																stock: next,
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
												<Button
													variant="ghost"
													size="sm"
													className={
														stock === 0
															? "gap-1 tabular-nums text-destructive"
															: stock < 10
																? "gap-1 tabular-nums text-warning"
																: "gap-1 tabular-nums"
													}
													onClick={() =>
														setEditingStock({
															id: product.id as number,
															value: String(stock),
														})
													}
												>
													{stock}
													<PencilIcon className="size-3 opacity-0 transition-opacity group-hover/row:opacity-60" />
												</Button>
											)}
										</TableCell>
										<TableCell>
											{product.deletedAt ? (
												<Badge variant="destructive">
													Silindi
												</Badge>
											) : product.isActive ? (
												<Badge variant="success">Aktif</Badge>
											) : (
												<Badge variant="secondary">Pasif</Badge>
											)}
										</TableCell>
										<TableCell className="text-right">
											<RowActions>
												<RowAction
													icon={PencilIcon}
													label="Düzenle"
													href={`/admin/urunler/${product.id as number}`}
												/>
												{product.deletedAt ? (
													<RowAction
														icon={RotateCcwIcon}
														label="Geri Al"
														onClick={() =>
															restoreProduct.mutate(
																product.id as number,
															)
														}
													/>
												) : (
													<RowAction
														icon={TrashIcon}
														label="Sil"
														tone="danger"
														confirm={{
															title: `"${product.name}" silinsin mi?`,
															description:
																"Ürün listede pasif görünecek, geri alınabilir.",
															confirmLabel: "Sil",
														}}
														onClick={() =>
															deleteProduct.mutate(
																product.id as number,
															)
														}
													/>
												)}
											</RowActions>
										</TableCell>
									</TableRow>
								);
							})}
						</TableBody>
					</Table>
				)}
			</Card>

			{data ? (
				<Pagination
					page={page}
					totalPages={Number(data.totalPages ?? 1)}
					hasPrevious={!!data.hasPrevious}
					hasNext={!!data.hasNext}
					onPageChange={(target) => setParam("page", String(target))}
				/>
			) : null}
		</div>
	);
}
