"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import type { z } from "zod";
import { apiFetch, ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import {
	productFormSchema,
	type ProductFormInput,
} from "@/lib/validations/admin-product";

// zod'un `z.coerce.number()` alanları RAW (girdi, HTML input string'i) ile
// PARSED (çıktı, react-hook-form'un handleSubmit'e verdiği) tipini
// ayırıyor — `useForm`'un üç jenerik parametresi de bu yüzden gerekli
// (`@hookform/resolvers` v5, zod v4).
type ProductFormRawValues = z.input<typeof productFormSchema>;
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import type { components } from "@/types/api";

type CategoryDto = components["schemas"]["CategoryDto"];
type PublisherBriefDto = components["schemas"]["PublisherBriefDto"];
type BrandBriefDto = components["schemas"]["BrandBriefDto"];
type AdminProductDetailDto = components["schemas"]["AdminProductDetailDto"];

// .NET'in ValidationProblem'ı PascalCase alan adı döner (ör. "DiscountedPrice"),
// react-hook-form camelCase bekliyor. Tek eşleştirme noktası burada.
function toFieldName(pascal: string): keyof ProductFormInput {
	return (pascal.charAt(0).toLowerCase() +
		pascal.slice(1)) as keyof ProductFormInput;
}

export function ProductForm({ product }: { product?: AdminProductDetailDto }) {
	const router = useRouter();
	const queryClient = useQueryClient();
	const isEdit = !!product;

	const { data: categories } = useQuery({
		queryKey: queryKeys.adminCategories,
		// Admin kategori listesi (pasifler dahil) YERİNE bilerek PUBLİK
		// /api/categories kullanılıyor — bu form için "hangi kategoriye
		// bağlarım" sorusuna aktif kategoriler yeterli, ayrı bir admin
		// kategori ucuna bağımlılık eklemeye gerek yok (Step 10'da kategori
		// CRUD'u gelince de bu form değişmeyecek).
		queryFn: () => apiFetch<CategoryDto[]>("categories"),
	});

	const { data: publishers } = useQuery({
		queryKey: queryKeys.adminPublishers,
		queryFn: () => apiFetch<PublisherBriefDto[]>("publishers"),
	});

	const { data: brands } = useQuery({
		queryKey: queryKeys.adminBrands,
		queryFn: () => apiFetch<BrandBriefDto[]>("brands"),
	});

	const {
		register,
		handleSubmit,
		setError,
		formState: { errors, isSubmitting },
	} = useForm<ProductFormRawValues, unknown, ProductFormInput>({
		resolver: zodResolver(productFormSchema),
		defaultValues: product
			? {
					name: product.name,
					sku: product.sku ?? "",
					description: product.description ?? "",
					price: Number(product.price),
					discountedPrice:
						product.discountedPrice != null
							? Number(product.discountedPrice)
							: "",
					stock: Number(product.stock),
					categoryId: Number(product.categoryId),
					publisherId:
						product.publisherId != null
							? Number(product.publisherId)
							: "",
					brandId:
						product.brandId != null ? Number(product.brandId) : "",
					isActive: product.isActive,
				}
			: { isActive: true },
	});

	const mutation = useMutation({
		mutationFn: (input: ProductFormInput) => {
			const body = {
				...input,
				sku: input.sku || null,
				description: input.description || null,
				discountedPrice: input.discountedPrice || null,
				publisherId: input.publisherId || null,
				brandId: input.brandId || null,
			};

			return isEdit
				? apiFetch<AdminProductDetailDto>(
						`admin/products/${product.id}`,
						{
							method: "PUT",
							body: JSON.stringify(body),
						},
					)
				: apiFetch<AdminProductDetailDto>("admin/products", {
						method: "POST",
						body: JSON.stringify(body),
					});
		},
		onSuccess: (saved) => {
			queryClient.invalidateQueries({ queryKey: ["admin", "products"] });
			toast.add({
				title: isEdit ? "Ürün güncellendi" : "Ürün oluşturuldu",
				type: "success",
			});
			router.push(`/admin/urunler/${saved.id}`);
		},
		onError: (error) => {
			if (
				error instanceof ApiError &&
				error.status === 400 &&
				isValidationBody(error.body)
			) {
				for (const [field, messages] of Object.entries(
					error.body.errors,
				)) {
					setError(toFieldName(field), { message: messages[0] });
				}
				return;
			}
			if (error instanceof ApiError && error.status === 409) {
				// SKU çakışması (CreateAsync) YA DA xmin çakışması (UpdateAsync) —
				// ikisi de aynı görsel geri bildirimi hak ediyor.
				toast.add({
					title: "Kaydedilemedi",
					description:
						error.body &&
						typeof error.body === "object" &&
						"detail" in error.body
							? String(error.body.detail)
							: "Bu SKU zaten kullanımda ya da kayıt başka biri tarafından değiştirildi.",
					type: "error",
				});
				return;
			}
			toast.add({ title: "Beklenmeyen bir hata oluştu", type: "error" });
		},
	});

	return (
		<form
			onSubmit={handleSubmit((input) => mutation.mutate(input))}
			className="max-w-xl space-y-4"
		>
			<div>
				<Label htmlFor="name">Ad</Label>
				<Input id="name" {...register("name")} />
				{errors.name ? (
					<p className="text-sm text-destructive">
						{errors.name.message}
					</p>
				) : null}
			</div>

			<div>
				<Label htmlFor="sku">SKU (opsiyonel)</Label>
				<Input id="sku" {...register("sku")} disabled={isEdit} />
				{isEdit ? (
					<p className="text-xs text-muted-foreground">
						SKU oluşturulduktan sonra değiştirilemez.
					</p>
				) : null}
			</div>

			<div>
				<Label htmlFor="description">Açıklama</Label>
				<Textarea
					id="description"
					rows={5}
					{...register("description")}
				/>
			</div>

			<div className="grid grid-cols-2 gap-4">
				<div>
					<Label htmlFor="price">Fiyat</Label>
					<Input
						id="price"
						type="number"
						step="0.01"
						{...register("price")}
					/>
					{errors.price ? (
						<p className="text-sm text-destructive">
							{errors.price.message}
						</p>
					) : null}
				</div>
				<div>
					<Label htmlFor="discountedPrice">İndirimli Fiyat</Label>
					<Input
						id="discountedPrice"
						type="number"
						step="0.01"
						{...register("discountedPrice")}
					/>
					{errors.discountedPrice ? (
						<p className="text-sm text-destructive">
							{errors.discountedPrice.message}
						</p>
					) : null}
				</div>
			</div>

			{!isEdit ? (
				<div>
					<Label htmlFor="stock">Stok</Label>
					<Input id="stock" type="number" {...register("stock")} />
					<p className="text-xs text-muted-foreground">
						Oluşturduktan sonra stok, ürün tablosundan satır içi
						düzenlenir.
					</p>
				</div>
			) : null}

			<div>
				<Label htmlFor="categoryId">Kategori</Label>
				<select
					id="categoryId"
					{...register("categoryId")}
					className="w-full rounded-md border px-3 py-2"
				>
					<option value="">Seç…</option>
					{categories?.map((c) => (
						<option key={c.id} value={c.id}>
							{"— ".repeat(categoryDepth(c, categories))}
							{c.name}
						</option>
					))}
				</select>
				{errors.categoryId ? (
					<p className="text-sm text-destructive">
						{errors.categoryId.message}
					</p>
				) : null}
			</div>

			<div>
				<Label htmlFor="publisherId">Yayınevi (opsiyonel)</Label>
				<select
					id="publisherId"
					{...register("publisherId")}
					className="w-full rounded-md border px-3 py-2"
				>
					<option value="">Seç…</option>
					{publishers?.map((p) => (
						<option key={p.id} value={p.id}>
							{p.name}
						</option>
					))}
				</select>
			</div>

			<div>
				<Label htmlFor="brandId">Marka (opsiyonel)</Label>
				<select
					id="brandId"
					{...register("brandId")}
					className="w-full rounded-md border px-3 py-2"
				>
					<option value="">Seç…</option>
					{brands?.map((b) => (
						<option key={b.id} value={b.id}>
							{b.name}
						</option>
					))}
				</select>
			</div>

			<div className="flex items-center gap-2">
				<Switch id="isActive" {...register("isActive")} />
				<Label htmlFor="isActive">Yayında</Label>
			</div>

			<Button type="submit" disabled={isSubmitting || mutation.isPending}>
				{mutation.isPending
					? "Kaydediliyor…"
					: isEdit
						? "Güncelle"
						: "Oluştur"}
			</Button>
		</form>
	);
}

function categoryDepth(
	category: CategoryDto,
	all: CategoryDto[],
	depth = 0,
): number {
	if (!category.parentId) return depth;
	const parent = all.find((c) => c.id === category.parentId);
	return parent ? categoryDepth(parent, all, depth + 1) : depth;
}

function isValidationBody(
	body: unknown,
): body is { errors: Record<string, string[]> } {
	return !!body && typeof body === "object" && "errors" in body;
}
