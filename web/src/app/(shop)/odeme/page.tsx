"use client";

import { useSearchParams } from "next/navigation";
import { AddressStep } from "@/components/checkout/address-step";
import { PaymentStep } from "@/components/checkout/payment-step";
import { CheckoutSteps } from "@/components/checkout/checkout-steps";
import { OrderSummary } from "@/components/checkout/order-summary";

export default function CheckoutPage() {
	const step = useSearchParams().get("step") ?? "address";
	const current = step === "payment" ? "payment" : "address";

	return (
		<main className="container-x py-8">
			<CheckoutSteps current={current} />

			<div className="grid gap-8 lg:grid-cols-[1fr_360px]">
				<div>
					{current === "address" ? <AddressStep /> : null}
					{current === "payment" ? <PaymentStep /> : null}
				</div>
				<OrderSummary />
			</div>
		</main>
	);
}
