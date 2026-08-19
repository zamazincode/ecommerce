"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { AlertCircleIcon, LoaderIcon } from "lucide-react";
import { apiFetch } from "@/lib/api/client";
import { IyzicoCheckoutForm } from "./iyzico-checkout-form";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import type { components } from "@/types/api";

type PaymentInitializedDto = components["schemas"]["PaymentInitializedDto"];

export function PaymentStep() {
	const orderNumber = useSearchParams().get("siparis");

	const { data, isLoading, error, refetch } = useQuery({
		queryKey: ["payment-init", orderNumber],
		queryFn: () =>
			apiFetch<PaymentInitializedDto>("payments/initialize", {
				method: "POST",
				body: JSON.stringify({ orderNumber }),
			}),
		enabled: !!orderNumber,
		// Ödeme başlatma İDEMPOTENT DEĞİL — her çağrı .NET tarafında yeni bir
		// Payments satırı açıyor. Sayfa yeniden render olunca TEKRAR
		// çağrılmasın diye cache'i "taze" say.
		staleTime: Infinity,
		retry: false,
	});

	if (!orderNumber) {
		return (
			<EmptyState
				icon={AlertCircleIcon}
				tone="danger"
				title="Geçersiz sipariş"
				description="Bu bağlantı geçerli değil."
				action={
					<Button
						render={<Link href="/hesabim/siparislerim" />}
						nativeButton={false}
					>
						Siparişlerime Dön
					</Button>
				}
			/>
		);
	}

	if (isLoading) {
		return (
			<div className="flex flex-col items-center gap-3 py-16 text-center">
				<LoaderIcon className="size-8 animate-spin text-primary" />
				<p className="text-sm text-muted-foreground">
					Ödeme formu hazırlanıyor…
				</p>
			</div>
		);
	}

	if (error || !data) {
		return (
			<EmptyState
				icon={AlertCircleIcon}
				tone="danger"
				title="Ödeme başlatılamadı"
				description="Bir hata oluştu, lütfen tekrar dene."
				action={
					<div className="flex items-center justify-center gap-2">
						<Button variant="outline" onClick={() => refetch()}>
							Tekrar Dene
						</Button>
						<Button
							render={<Link href="/hesabim/siparislerim" />}
							nativeButton={false}
						>
							Siparişlerime Dön
						</Button>
					</div>
				}
			/>
		);
	}

	return <IyzicoCheckoutForm checkoutContent={data.checkoutContent} />;
}
