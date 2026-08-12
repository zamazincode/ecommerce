"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import useSession from "@/hooks/use-session";

export default function AuthLayout({
	children,
}: Readonly<{
	children: React.ReactNode;
}>) {
	const { data: user, isPending } = useSession();
	const router = useRouter();

	useEffect(() => {
		if (!isPending && user) router.replace("/hesabim");
	}, [isPending, user, router]);

	if (isPending || user) return null;

	return <>{children}</>;
}
