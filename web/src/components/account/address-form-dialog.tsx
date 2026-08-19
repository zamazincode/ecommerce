"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
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
import { Label } from "@/components/ui/label";
import { FormField } from "@/components/ui/form-field";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { useCreateAddress, useUpdateAddress } from "@/hooks/use-addresses";
import {
	addressFormSchema,
	type AddressFormInput,
} from "@/lib/validations/address";
import type { components } from "@/types/api";

type AddressDto = components["schemas"]["AddressDto"];

export function AddressFormDialog({
	address,
	trigger,
}: {
	address?: AddressDto;
	trigger: React.ReactNode;
}) {
	const [open, setOpen] = useState(false);
	const isEdit = !!address;
	const create = useCreateAddress();
	const update = useUpdateAddress();

	const {
		register,
		handleSubmit,
		reset,
		formState: { errors, isSubmitting },
	} = useForm<AddressFormInput>({
		resolver: zodResolver(addressFormSchema),
		defaultValues: address ?? { isDefault: false },
	});

	async function onSubmit(input: AddressFormInput) {
		try {
			if (isEdit)
				await update.mutateAsync({ id: address.id as number, input });
			else await create.mutateAsync(input);
			toast.add({
				title: isEdit ? "Adres güncellendi" : "Adres eklendi",
				type: "success",
			});
			setOpen(false);
			reset();
		} catch {
			toast.add({ title: "Adres kaydedilemedi", type: "error" });
		}
	}

	return (
		<Dialog open={open} onOpenChange={setOpen}>
			<DialogTrigger render={trigger as React.ReactElement} />
			<DialogContent className="sm:max-w-lg">
				<DialogHeader>
					<DialogTitle>
						{isEdit ? "Adresi Düzenle" : "Yeni Adres"}
					</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
					<FormField label="Başlık" htmlFor="title" error={errors.title?.message}>
						<Input id="title" placeholder="Ev, İş…" {...register("title")} />
					</FormField>

					<FormField
						label="Ad Soyad"
						htmlFor="fullName"
						error={errors.fullName?.message}
					>
						<Input id="fullName" {...register("fullName")} />
					</FormField>

					<FormField label="Telefon" htmlFor="phone" error={errors.phone?.message}>
						<Input id="phone" {...register("phone")} />
					</FormField>

					<div className="grid grid-cols-2 gap-4">
						<FormField label="İl" htmlFor="city" error={errors.city?.message}>
							<Input id="city" {...register("city")} />
						</FormField>
						<FormField
							label="İlçe"
							htmlFor="district"
							error={errors.district?.message}
						>
							<Input id="district" {...register("district")} />
						</FormField>
					</div>

					<FormField
						label="Açık Adres"
						htmlFor="fullAddress"
						error={errors.fullAddress?.message}
					>
						<Input id="fullAddress" {...register("fullAddress")} />
					</FormField>

					<div className="flex items-center gap-2">
						<Switch id="isDefault" {...register("isDefault")} />
						<Label htmlFor="isDefault">Varsayılan adres yap</Label>
					</div>

					<DialogFooter>
						<DialogClose render={<Button type="button" variant="outline" />}>
							İptal
						</DialogClose>
						<Button type="submit" disabled={isSubmitting}>
							{isEdit ? "Güncelle" : "Ekle"}
						</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}
