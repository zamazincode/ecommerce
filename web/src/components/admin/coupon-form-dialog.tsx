"use client";

import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
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
import { NativeSelect } from "@/components/ui/native-select";
import { FormField } from "@/components/ui/form-field";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";
import { useCreateCoupon } from "@/hooks/use-admin-coupons";
import {
	couponFormSchema,
	type CouponFormInput,
	type CouponFormRawValues,
} from "@/lib/validations/admin-coupon";
import { COUPON_TYPE_LABELS } from "@/lib/enums";

export function CouponFormDialog({ trigger }: { trigger: React.ReactNode }) {
	const [open, setOpen] = useState(false);
	const create = useCreateCoupon();

	const {
		register,
		handleSubmit,
		setError,
		control,
		reset,
		formState: { errors, isSubmitting },
	} = useForm<CouponFormRawValues, unknown, CouponFormInput>({
		resolver: zodResolver(couponFormSchema),
		defaultValues: { type: 0, minCartTotal: 0 },
	});

	// `watch()` yerine `useWatch({ control })` — bkz. product-form.tsx'teki aynı not.
	const valueSuffix =
		Number(useWatch({ control, name: "type" })) === 0 ? "%" : "₺";

	async function onSubmit(input: CouponFormInput) {
		try {
			// Backend AsUtc() Kind=Unspecified/Local/Utc HEPSİNİ kabul edip
			// UTC'ye normalize ediyor — "T00:00:00" eki, çıplak "2026-09-01"
			// dizesinin System.Text.Json tarafında DateTime olarak ayrıştırılmasını
			// garanti eden en basit biçim (timestamptz tuzağı).
			await create.mutateAsync({
				code: input.code.toUpperCase(),
				type: input.type,
				value: input.value,
				minCartTotal: input.minCartTotal,
				validFrom: `${input.validFrom}T00:00:00`,
				validTo: `${input.validTo}T00:00:00`,
				usageLimit: input.usageLimit || null,
			});
			toast.add({ title: "Kupon oluşturuldu", type: "success" });
			setOpen(false);
			reset();
		} catch (error) {
			if (error instanceof ApiError && error.status === 409) {
				setError("code", { message: "Bu kupon kodu zaten kullanımda." });
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
					<DialogTitle>Yeni Kupon</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
					<FormField label="Kod" htmlFor="code" error={errors.code?.message}>
						<Input
							id="code"
							{...register("code")}
							className="uppercase"
							placeholder="HOSGELDIN10"
						/>
					</FormField>

					<FormField label="Tip" htmlFor="type">
						<NativeSelect id="type" {...register("type")}>
							{Object.entries(COUPON_TYPE_LABELS).map(
								([value, label]) => (
									<option key={value} value={value}>
										{label}
									</option>
								),
							)}
						</NativeSelect>
					</FormField>

					<div className="grid grid-cols-2 gap-4">
						<FormField
							label="Değer"
							htmlFor="value"
							hint={`Birim: ${valueSuffix}`}
							error={errors.value?.message}
						>
							<Input
								id="value"
								type="number"
								step="0.01"
								{...register("value")}
							/>
						</FormField>
						<FormField label="Min. Sepet Tutarı" htmlFor="minCartTotal">
							<Input
								id="minCartTotal"
								type="number"
								step="0.01"
								{...register("minCartTotal")}
							/>
						</FormField>
					</div>

					<div className="grid grid-cols-2 gap-4">
						<FormField label="Başlangıç" htmlFor="validFrom">
							<Input id="validFrom" type="date" {...register("validFrom")} />
						</FormField>
						<FormField
							label="Bitiş"
							htmlFor="validTo"
							error={errors.validTo?.message}
						>
							<Input id="validTo" type="date" {...register("validTo")} />
						</FormField>
					</div>

					<FormField label="Kullanım Limiti (opsiyonel)" htmlFor="usageLimit">
						<Input
							id="usageLimit"
							type="number"
							{...register("usageLimit")}
						/>
					</FormField>

					<DialogFooter>
						<DialogClose render={<Button type="button" variant="outline" />}>
							İptal
						</DialogClose>
						<Button type="submit" disabled={isSubmitting}>
							Oluştur
						</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}
