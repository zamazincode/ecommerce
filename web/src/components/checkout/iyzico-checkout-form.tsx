"use client";

import { useEffect, useRef } from "react";

export function IyzicoCheckoutForm({
	checkoutContent,
}: {
	checkoutContent: string;
}) {
	const scriptContainerRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		const container = scriptContainerRef.current;
		if (!container) return;

		// checkoutContent tek bir <script> etiketi — içeriğini çıkar.
		const parsed = new DOMParser().parseFromString(
			checkoutContent,
			"text/html",
		);
		const sourceScript = parsed.querySelector("script");
		if (!sourceScript) return;

		// Tarayıcı SADECE document.createElement('script') ile document'a
		// EKLENEN script'leri çalıştırıyor — innerHTML ile geleni değil.
		const executableScript = document.createElement("script");
		executableScript.type = "text/javascript";
		executableScript.text = sourceScript.textContent ?? "";
		container.appendChild(executableScript);

		return () => {
			// Sayfadan ayrılınca / checkoutContent değişince temizle —
			// aksi hâlde `iyziInit` ikinci kez tanımlanmaya çalışılıp
			// "already defined" hatası verebilir.
			container.replaceChildren();
		};
	}, [checkoutContent]);

	return (
		<div>
			{/* iyzico'nun bundle.js'i formu BURAYA render ediyor — id sabit,
			    iyzico'nun kendi script'i bu id'yi arıyor. */}
			<div id="iyzipay-checkout-form" className="responsive" />
			<div ref={scriptContainerRef} />
		</div>
	);
}
