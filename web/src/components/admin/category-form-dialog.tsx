"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
	Dialog,
	DialogContent,
	DialogHeader,
	DialogTitle,
	DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";
import {
	useCreateCategory,
	useUpdateCategory,
} from "@/hooks/use-admin-categories";
import { categoryDepth, selfAndDescendantIds } from "@/lib/admin/category-tree";
import type { components } from "@/types/api";

type AdminCategoryDto = components["schemas"]["AdminCategoryDto"];

const schema = z.object({
	name: z.string().min(1, "Ad gerekli.").max(150),
	parentId: z.coerce.number().int().positive().optional().or(z.literal("")),
	displayOrder: z.coerce.number().int().min(0).optional().or(z.literal("")),
	isActive: z.boolean(),
});
type FormInput = z.infer<typeof schema>;
// zod'un `z.coerce` alanları RAW (girdi) ile PARSED (çıktı) tipini ayırıyor —
// bkz. product-form.tsx'teki aynı not.
type FormRawValues = z.input<typeof schema>;

export function CategoryFormDialog({
	category,
	categories,
	trigger,
}: {
	category?: AdminCategoryDto;
	categories: AdminCategoryDto[];
	trigger: React.ReactNode;
}) {
	const isEdit = !!category;
	const create = useCreateCategory();
	const update = useUpdateCategory();

	const {
		register,
		handleSubmit,
		setError,
		formState: { errors, isSubmitting },
	} = useForm<FormRawValues, unknown, FormInput>({
		resolver: zodResolver(schema),
		defaultValues: category
			? {
					name: category.name,
					parentId:
						category.parentId != null ? Number(category.parentId) : "",
					displayOrder: Number(category.displayOrder),
					isActive: category.isActive,
				}
			: { isActive: true, displayOrder: 0 },
	});

	// Kendisi ve alt kategorileri — üst kategori seçiminden ÇIKARILIYOR
	// (yalnızca UX, gerçek kontrol sunucuda).
	const disabledIds = category
		? selfAndDescendantIds(category.id as number, categories)
		: new Set<number>();

	async function onSubmit(input: FormInput) {
		const parentId = input.parentId === "" ? null : (input.parentId ?? null);
		const displayOrder =
			input.displayOrder === "" ? null : (input.displayOrder ?? null);

		try {
			if (isEdit) {
				await update.mutateAsync({
					id: category.id as number,
					input: {
						name: input.name,
						parentId,
						displayOrder,
						isActive: input.isActive,
					},
				});
			} else {
				await create.mutateAsync({ name: input.name, parentId, displayOrder });
			}
			toast.add({
				title: isEdit ? "Kategori güncellendi" : "Kategori oluşturuldu",
				type: "success",
			});
		} catch (error) {
			// Backend'in iki hata mesajı da FIELD-SPESİFİK değil (BusinessRuleException,
			// genel bir 400) — setError yerine tek bir form-üstü hata gösteriyoruz.
			if (
				error instanceof ApiError &&
				error.body &&
				typeof error.body === "object" &&
				"detail" in error.body
			) {
				setError("parentId", { message: String(error.body.detail) });
				return;
			}
			toast.add({ title: "Beklenmeyen bir hata oluştu", type: "error" });
		}
	}

	return (
		<Dialog>
			<DialogTrigger render={trigger as React.ReactElement} />
			<DialogContent>
				<DialogHeader>
					<DialogTitle>
						{isEdit ? "Kategoriyi Düzenle" : "Yeni Kategori"}
					</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
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
						<Label htmlFor="parentId">Üst Kategori (opsiyonel)</Label>
						<select
							id="parentId"
							{...register("parentId")}
							className="w-full rounded-md border px-3 py-2"
						>
							<option value="">Kök kategori</option>
							{categories
								.filter((c) => !disabledIds.has(c.id as number))
								.map((c) => (
									<option key={c.id} value={c.id}>
										{"— ".repeat(categoryDepth(c, categories))}
										{c.name}
									</option>
								))}
						</select>
						{errors.parentId ? (
							<p className="text-sm text-destructive">
								{errors.parentId.message}
							</p>
						) : null}
					</div>
					<div>
						<Label htmlFor="displayOrder">Sıra</Label>
						<Input
							id="displayOrder"
							type="number"
							{...register("displayOrder")}
						/>
					</div>
					{isEdit ? (
						<div className="flex items-center gap-2">
							<Switch id="isActive" {...register("isActive")} />
							<Label htmlFor="isActive">Aktif</Label>
						</div>
					) : null}
					<Button type="submit" disabled={isSubmitting}>
						{isEdit ? "Güncelle" : "Oluştur"}
					</Button>
				</form>
			</DialogContent>
		</Dialog>
	);
}
