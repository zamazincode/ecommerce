"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
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
	const isEdit = !!address;
	const create = useCreateAddress();
	const update = useUpdateAddress();

	const {
		register,
		handleSubmit,
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
		} catch {
			toast.add({ title: "Adres kaydedilemedi", type: "error" });
		}
	}

	return (
		<Dialog>
			<DialogTrigger render={trigger as React.ReactElement} />
			<DialogContent>
				<DialogHeader>
					<DialogTitle>
						{isEdit ? "Adresi Düzenle" : "Yeni Adres"}
					</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
					<div>
						<Label htmlFor="title">Başlık</Label>
						<Input
							id="title"
							placeholder="Ev, İş…"
							{...register("title")}
						/>
						{errors.title ? (
							<p className="text-sm text-destructive">
								{errors.title.message}
							</p>
						) : null}
					</div>
					<div>
						<Label htmlFor="fullName">Ad Soyad</Label>
						<Input id="fullName" {...register("fullName")} />
						{errors.fullName ? (
							<p className="text-sm text-destructive">
								{errors.fullName.message}
							</p>
						) : null}
					</div>
					<div>
						<Label htmlFor="phone">Telefon</Label>
						<Input id="phone" {...register("phone")} />
						{errors.phone ? (
							<p className="text-sm text-destructive">
								{errors.phone.message}
							</p>
						) : null}
					</div>
					<div className="grid grid-cols-2 gap-4">
						<div>
							<Label htmlFor="city">İl</Label>
							<Input id="city" {...register("city")} />
							{errors.city ? (
								<p className="text-sm text-destructive">
									{errors.city.message}
								</p>
							) : null}
						</div>
						<div>
							<Label htmlFor="district">İlçe</Label>
							<Input id="district" {...register("district")} />
							{errors.district ? (
								<p className="text-sm text-destructive">
									{errors.district.message}
								</p>
							) : null}
						</div>
					</div>
					<div>
						<Label htmlFor="fullAddress">Açık Adres</Label>
						<Input id="fullAddress" {...register("fullAddress")} />
						{errors.fullAddress ? (
							<p className="text-sm text-destructive">
								{errors.fullAddress.message}
							</p>
						) : null}
					</div>
					<div className="flex items-center gap-2">
						<Switch id="isDefault" {...register("isDefault")} />
						<Label htmlFor="isDefault">Varsayılan adres yap</Label>
					</div>
					<Button type="submit" disabled={isSubmitting}>
						{isEdit ? "Güncelle" : "Ekle"}
					</Button>
				</form>
			</DialogContent>
		</Dialog>
	);
}
