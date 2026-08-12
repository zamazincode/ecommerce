"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { SquareArrowRightExit, UserIcon } from "lucide-react";
import useSession from "@/hooks/use-session";

export function AccountMenu() {
	const router = useRouter();
	const queryClient = useQueryClient();
	const { data: user, isLoading } = useSession();

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	if (isLoading) return null;

	if (!user) {
		return (
			<Link href="/giris" className="text-sm hover:underline">
				Giriş Yap
			</Link>
		);
	}

	return (
		<div className="flex items-center gap-3 text-sm">
			<Link
				href="/hesabim"
				className="flex items-center gap-1 hover:underline"
			>
				<UserIcon className="size-4" />
				Hesabım
			</Link>
			<button
				onClick={handleLogout}
				className="text-muted-foreground hover:underline flex items-center gap-1"
			>
				<SquareArrowRightExit className="size-4" />
				Çıkış
			</button>
		</div>
	);
}
