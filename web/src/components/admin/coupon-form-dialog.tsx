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
	const create = useCreateCoupon();

	const {
		register,
		handleSubmit,
		setError,
		formState: { errors, isSubmitting },
	} = useForm<CouponFormRawValues, unknown, CouponFormInput>({
		resolver: zodResolver(couponFormSchema),
		defaultValues: { type: 0, minCartTotal: 0 },
	});

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
		} catch (error) {
			if (error instanceof ApiError && error.status === 409) {
				setError("code", { message: "Bu kupon kodu zaten kullanımda." });
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
					<DialogTitle>Yeni Kupon</DialogTitle>
				</DialogHeader>
				<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
					<div>
						<Label htmlFor="code">Kod</Label>
						<Input
							id="code"
							{...register("code")}
							className="uppercase"
							placeholder="HOSGELDIN10"
						/>
						{errors.code ? (
							<p className="text-sm text-destructive">
								{errors.code.message}
							</p>
						) : null}
					</div>
					<div>
						<Label htmlFor="type">Tip</Label>
						<select
							id="type"
							{...register("type")}
							className="w-full rounded-md border px-3 py-2"
						>
							{Object.entries(COUPON_TYPE_LABELS).map(
								([value, label]) => (
									<option key={value} value={value}>
										{label}
									</option>
								),
							)}
						</select>
					</div>
					<div className="grid grid-cols-2 gap-4">
						<div>
							<Label htmlFor="value">Değer</Label>
							<Input
								id="value"
								type="number"
								step="0.01"
								{...register("value")}
							/>
							{errors.value ? (
								<p className="text-sm text-destructive">
									{errors.value.message}
								</p>
							) : null}
						</div>
						<div>
							<Label htmlFor="minCartTotal">Min. Sepet Tutarı</Label>
							<Input
								id="minCartTotal"
								type="number"
								step="0.01"
								{...register("minCartTotal")}
							/>
						</div>
					</div>
					<div className="grid grid-cols-2 gap-4">
						<div>
							<Label htmlFor="validFrom">Başlangıç</Label>
							<Input
								id="validFrom"
								type="date"
								{...register("validFrom")}
							/>
						</div>
						<div>
							<Label htmlFor="validTo">Bitiş</Label>
							<Input id="validTo" type="date" {...register("validTo")} />
							{errors.validTo ? (
								<p className="text-sm text-destructive">
									{errors.validTo.message}
								</p>
							) : null}
						</div>
					</div>
					<div>
						<Label htmlFor="usageLimit">
							Kullanım Limiti (opsiyonel)
						</Label>
						<Input
							id="usageLimit"
							type="number"
							{...register("usageLimit")}
						/>
					</div>
					<Button type="submit" disabled={isSubmitting}>
						Oluştur
					</Button>
				</form>
			</DialogContent>
		</Dialog>
	);
}
