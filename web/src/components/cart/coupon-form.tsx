"use client";

import { useState, type FormEvent } from "react";
import { XIcon } from "lucide-react";
import { useApplyCoupon, useRemoveCoupon } from "@/hooks/use-cart";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";

/**
 * Sepet panelindeki kupon alanı — `useApplyCoupon`/`useRemoveCoupon` daha önce
 * ölü kodtu (hiçbir bileşen çağırmıyordu), burada ilk kez kullanılıyor.
 */
export function CouponForm({ couponCode }: { couponCode: string | null }) {
	const [code, setCode] = useState("");
	const applyCoupon = useApplyCoupon();
	const removeCoupon = useRemoveCoupon();

	async function handleSubmit(event: FormEvent<HTMLFormElement>) {
		event.preventDefault();
		const trimmed = code.trim();
		if (!trimmed) return;

		try {
			await applyCoupon.mutateAsync(trimmed);
			setCode("");
		} catch (error) {
			toast.add({
				title:
					error instanceof ApiError &&
					error.body &&
					typeof error.body === "object" &&
					"detail" in error.body
						? String(error.body.detail)
						: "Kupon uygulanamadı.",
				type: "error",
			});
		}
	}

	if (couponCode) {
		return (
			<div className="border-t px-5 py-3">
				<Badge variant="success" className="gap-1.5">
					{couponCode}
					<button
						type="button"
						onClick={() => removeCoupon.mutate()}
						disabled={removeCoupon.isPending}
						aria-label="Kuponu kaldır"
					>
						<XIcon className="size-3.5" />
					</button>
				</Badge>
			</div>
		);
	}

	return (
		<form
			onSubmit={handleSubmit}
			className="flex gap-2 border-t px-5 py-3"
		>
			<Input
				value={code}
				onChange={(e) => setCode(e.target.value)}
				placeholder="Kupon kodu"
				aria-label="Kupon kodu"
				className="flex-1"
			/>
			<Button type="submit" variant="soft" disabled={applyCoupon.isPending}>
				Uygula
			</Button>
		</form>
	);
}
