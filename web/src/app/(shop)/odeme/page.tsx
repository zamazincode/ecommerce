"use client";

import { AddressStep } from "@/components/checkout/address-step";
import { PaymentStep } from "@/components/checkout/payment-step";
import { useSearchParams } from "next/navigation";

export default function CheckoutPage() {
	const step = useSearchParams().get("step") ?? "address";

	return (
		<main className="container-x py-8">
			{step === "address" ? <AddressStep /> : null}
			{step === "payment" ? <PaymentStep /> : null}
		</main>
	);
}
