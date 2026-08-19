"use client";

import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { LoaderIcon } from "lucide-react";
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
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { FormField } from "@/components/ui/form-field";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
		control,
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

	// Anlık indirim yüzdesi — sunucudaki "indirimli fiyat < fiyat" kuralının
	// bir tekrarı değil, salt görsel geri bildirim; gerçek doğrulama zod'da.
	// `watch()` yerine `useWatch({ control })` — ikisi işlevsel olarak eşdeğer,
	// ama `watch()` React Compiler eslint eklentisinin "memoize edilemez"
	// listesinde (react-hook-form'a özel), `useWatch` değil.
	const priceValue = Number(useWatch({ control, name: "price" }));
	const discountedPriceValue = Number(
		useWatch({ control, name: "discountedPrice" }),
	);
	const discountPercent =
		priceValue > 0 &&
		discountedPriceValue > 0 &&
		discountedPriceValue < priceValue
			? Math.round((1 - discountedPriceValue / priceValue) * 100)
			: null;

	// Base UI `Switch` native bir `<input>` değil, `register()`'ın `onChange`/
	// `ref`'i doğru elemana bağlanmıyor (bkz. plan tuzakları) — `Controller`
	// ile `checked`/`onCheckedChange` üzerinden bağlanıyor.
	const isActiveValue = useWatch({ control, name: "isActive" });

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
			className="space-y-6"
		>
			<Card>
				<CardHeader>
					<CardTitle>Temel Bilgiler</CardTitle>
				</CardHeader>
				<CardContent>
					<FormField label="Ad" htmlFor="name" error={errors.name?.message}>
						<Input id="name" {...register("name")} />
					</FormField>

					<div className="grid gap-4 sm:grid-cols-2">
						<FormField
							label="SKU (opsiyonel)"
							htmlFor="sku"
							hint={
								isEdit
									? "SKU oluşturulduktan sonra değiştirilemez."
									: undefined
							}
						>
							<Input id="sku" {...register("sku")} disabled={isEdit} />
						</FormField>

						<FormField
							label="Kategori"
							htmlFor="categoryId"
							error={errors.categoryId?.message}
						>
							<NativeSelect id="categoryId" {...register("categoryId")}>
								<option value="">Seç…</option>
								{categories?.map((c) => (
									<option key={c.id} value={c.id}>
										{"— ".repeat(categoryDepth(c, categories))}
										{c.name}
									</option>
								))}
							</NativeSelect>
						</FormField>
					</div>

					<FormField label="Açıklama" htmlFor="description">
						<Textarea id="description" rows={6} {...register("description")} />
					</FormField>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle>Fiyat &amp; Stok</CardTitle>
				</CardHeader>
				<CardContent>
					<div className="grid gap-4 sm:grid-cols-3">
						<FormField
							label="Fiyat"
							htmlFor="price"
							error={errors.price?.message}
						>
							<div className="relative">
								<span className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-sm text-muted-foreground">
									₺
								</span>
								<Input
									id="price"
									type="number"
									step="0.01"
									className="pl-7"
									{...register("price")}
								/>
							</div>
						</FormField>

						<FormField
							label="İndirimli Fiyat"
							htmlFor="discountedPrice"
							error={errors.discountedPrice?.message}
						>
							<div className="relative">
								<span className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-sm text-muted-foreground">
									₺
								</span>
								<Input
									id="discountedPrice"
									type="number"
									step="0.01"
									className="pl-7"
									{...register("discountedPrice")}
								/>
							</div>
							{discountPercent != null ? (
								<p className="text-xs text-success">
									%{discountPercent} indirim
								</p>
							) : null}
						</FormField>

						{!isEdit ? (
							<FormField
								label="Stok"
								htmlFor="stock"
								hint="Oluşturduktan sonra stok, ürün tablosundan satır içi düzenlenir."
							>
								<Input id="stock" type="number" {...register("stock")} />
							</FormField>
						) : null}
					</div>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle>Sınıflandırma</CardTitle>
				</CardHeader>
				<CardContent>
					<div className="grid gap-4 sm:grid-cols-2">
						<FormField label="Yayınevi (opsiyonel)" htmlFor="publisherId">
							<NativeSelect id="publisherId" {...register("publisherId")}>
								<option value="">Seç…</option>
								{publishers?.map((p) => (
									<option key={p.id} value={p.id}>
										{p.name}
									</option>
								))}
							</NativeSelect>
						</FormField>

						<FormField label="Marka (opsiyonel)" htmlFor="brandId">
							<NativeSelect id="brandId" {...register("brandId")}>
								<option value="">Seç…</option>
								{brands?.map((b) => (
									<option key={b.id} value={b.id}>
										{b.name}
									</option>
								))}
							</NativeSelect>
						</FormField>
					</div>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle>Yayın Durumu</CardTitle>
				</CardHeader>
				<CardContent>
					<div className="flex items-center gap-2">
						<Controller
							control={control}
							name="isActive"
							render={({ field: { value, onChange, ...field } }) => (
								<Switch
									{...field}
									id="isActive"
									checked={!!value}
									onCheckedChange={onChange}
								/>
							)}
						/>
						<Label htmlFor="isActive">
							{isActiveValue ? "Yayında" : "Yayında değil"}
						</Label>
					</div>
					<p className="text-xs text-muted-foreground">
						Yayında değilse ürün sitede görünmez.
					</p>
				</CardContent>
			</Card>

			<div className="sticky bottom-0 -mx-4 flex items-center justify-end gap-2 border-t bg-background/95 px-4 py-3 backdrop-blur">
				<Button
					type="button"
					variant="outline"
					render={<Link href="/admin/urunler" />}
					nativeButton={false}
				>
					İptal
				</Button>
				<Button type="submit" disabled={isSubmitting || mutation.isPending}>
					{mutation.isPending ? <LoaderIcon className="animate-spin" /> : null}
					{mutation.isPending
						? "Kaydediliyor…"
						: isEdit
							? "Güncelle"
							: "Oluştur"}
				</Button>
			</div>
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
