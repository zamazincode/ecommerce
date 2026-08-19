"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
	Dialog,
	DialogClose,
	DialogContent,
	DialogFooter,
	DialogHeader,
	DialogTitle,
	DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { FormField } from "@/components/ui/form-field";
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
	const [open, setOpen] = useState(false);
	const isEdit = !!category;
	const create = useCreateCategory();
	const update = useUpdateCategory();

	const {
		register,
		handleSubmit,
		setError,
		reset,
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
			setOpen(false);
			reset();
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
		<Dialog open={open} onOpenChange={setOpen}>
			<DialogTrigger render={trigger as React.ReactElement} />
			<DialogContent className="sm:max-w-lg">
				<DialogHeader>
					<DialogTitle>
						{isEdit ? "Kategoriyi Düzenle" : "Yeni Kategori"}
					</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
					{errors.parentId ? (
						<p className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">
							{errors.parentId.message}
						</p>
					) : null}

					<FormField label="Ad" htmlFor="name" error={errors.name?.message}>
						<Input id="name" {...register("name")} />
					</FormField>

					<FormField label="Üst Kategori (opsiyonel)" htmlFor="parentId">
						<NativeSelect id="parentId" {...register("parentId")}>
							<option value="">Kök kategori</option>
							{categories
								.filter((c) => !disabledIds.has(c.id as number))
								.map((c) => (
									<option key={c.id} value={c.id}>
										{"— ".repeat(categoryDepth(c, categories))}
										{c.name}
									</option>
								))}
						</NativeSelect>
					</FormField>

					<FormField label="Sıra" htmlFor="displayOrder">
						<Input
							id="displayOrder"
							type="number"
							{...register("displayOrder")}
						/>
					</FormField>

					{isEdit ? (
						<div className="flex items-center gap-2">
							<Switch id="isActive" {...register("isActive")} />
							<Label htmlFor="isActive">Aktif</Label>
						</div>
					) : null}

					<DialogFooter>
						<DialogClose render={<Button type="button" variant="outline" />}>
							İptal
						</DialogClose>
						<Button type="submit" disabled={isSubmitting}>
							{isEdit ? "Güncelle" : "Oluştur"}
						</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}
