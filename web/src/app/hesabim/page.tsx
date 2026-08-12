"use client";

export default function HesabimPage() {
	return (
		<div>
			<h1>hesabim</h1>

			<button
				onClick={() => {
					fetch("/api/auth/logout", { method: "POST" });
				}}
			>
				Çıkış Yap
			</button>
		</div>
	);
}
