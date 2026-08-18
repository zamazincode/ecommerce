"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";

export function CancelOrderButton({ orderNumber }: { orderNumber: string }) {
	const router = useRouter();
	const [isPending, setIsPending] = useState(false);

	async function handleCancel() {
		if (!confirm("Bu siparişi iptal etmek istediğine emin misin?")) return;

		setIsPending(true);
		try {
			await apiFetch(`orders/${orderNumber}/cancel`, { method: "POST" });
			toast.add({ title: "Sipariş iptal edildi", type: "success" });
			router.refresh();
		} catch (error) {
			toast.add({
				title:
					error instanceof ApiError &&
					error.body &&
					typeof error.body === "object" &&
					"detail" in error.body
						? String(error.body.detail)
						: "İptal edilemedi.",
				type: "error",
			});
		} finally {
			setIsPending(false);
		}
	}

	return (
		<Button
			variant="destructive"
			size="sm"
			onClick={handleCancel}
			disabled={isPending}
		>
			{isPending ? "İptal ediliyor…" : "Siparişi İptal Et"}
		</Button>
	);
}
