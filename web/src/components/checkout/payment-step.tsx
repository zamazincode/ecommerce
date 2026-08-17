"use client";

import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { IyzicoCheckoutForm } from "./iyzico-checkout-form";
import type { components } from "@/types/api";

type PaymentInitializedDto = components["schemas"]["PaymentInitializedDto"];

export function PaymentStep() {
	const orderNumber = useSearchParams().get("siparis");

	const { data, isLoading, error } = useQuery({
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

	if (!orderNumber) return <p>Geçersiz sipariş.</p>;
	if (isLoading) return <p>Ödeme formu hazırlanıyor…</p>;
	if (error || !data)
		return <p>Ödeme başlatılamadı. Lütfen tekrar deneyin.</p>;

	return <IyzicoCheckoutForm checkoutContent={data.checkoutContent} />;
}
