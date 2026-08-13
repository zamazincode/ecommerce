"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

export default function HesabimPage() {
	const queryClient = useQueryClient();
	const router = useRouter();

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	return (
		<div>
			<h1>hesabim</h1>

			<button onClick={() => handleLogout()}>Çıkış Yap</button>
		</div>
	);
}
