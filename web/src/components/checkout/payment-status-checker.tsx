"use client";

import { useState } from "react";
import { apiFetch } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import type { components } from "@/types/api";

type PaymentStatusDto = components["schemas"]["PaymentStatusDto"];

const PAYMENT_STATUS_LABELS: Record<number, string> = {
	0: "Bekliyor",
	1: "Başarılı",
	2: "Başarısız",
	3: "İade Edildi",
};

export function PaymentStatusChecker({ orderNumber }: { orderNumber: string }) {
	const [status, setStatus] = useState<PaymentStatusDto | null>(null);
	const [isChecking, setIsChecking] = useState(false);

	async function check() {
		setIsChecking(true);
		try {
			const result = await apiFetch<PaymentStatusDto>(
				`payments/${orderNumber}/status`,
			);
			setStatus(result);
		} catch {
			toast.add({ title: "Durum kontrol edilemedi", type: "error" });
		} finally {
			setIsChecking(false);
		}
	}

	return (
		<div className="mt-4">
			<Button variant="outline" onClick={check} disabled={isChecking}>
				{isChecking ? "Kontrol ediliyor…" : "Durumu Kontrol Et"}
			</Button>
			{status ? (
				<p className="mt-2 text-sm">
					Ödeme durumu:{" "}
					<strong>
						{status.paymentStatus != null
							? PAYMENT_STATUS_LABELS[status.paymentStatus]
							: "Kayıt yok"}
					</strong>
				</p>
			) : null}
		</div>
	);
}
