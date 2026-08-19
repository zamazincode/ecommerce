"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { toast } from "@/components/ui/toast";

export function CancelOrderButton({ orderNumber }: { orderNumber: string }) {
	const router = useRouter();
	const [isPending, setIsPending] = useState(false);

	async function handleCancel() {
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
		<ConfirmDialog
			trigger={
				<Button variant="destructive" size="sm" disabled={isPending}>
					{isPending ? "İptal ediliyor…" : "Siparişi İptal Et"}
				</Button>
			}
			title="Bu siparişi iptal etmek istediğine emin misin?"
			description="Bu işlem geri alınamaz, stok iadesi otomatik yapılır."
			confirmLabel="Siparişi İptal Et"
			tone="danger"
			onConfirm={handleCancel}
		/>
	);
}
