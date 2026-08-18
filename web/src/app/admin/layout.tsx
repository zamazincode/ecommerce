import { redirect } from "next/navigation";
import { serverApiFetch } from "@/lib/api/server";
import { Toaster } from "@/components/ui/toast";
import type { components } from "@/types/api";
import { AdminSidebar } from "@/components/admin/sidebar";

type UserDto = components["schemas"]["UserDto"];

export default async function AdminLayout({
	children,
}: {
	children: React.ReactNode;
}) {
	// serverApiFetch 401'i de, 403'ü de null'a çeviriyor (lib/api/server.ts,
	// Step 2) — ikisini de "içeri alma" olarak ele almak burada YETERLİ,
	// çünkü tek yaptığımız "izin var mı" sorusu.
	const user = await serverApiFetch<UserDto>("auth/me");

	if (!user) redirect("/giris?returnUrl=/admin");
	if (!user.roles.includes("Admin")) redirect("/");

	return (
		<div className="flex min-h-screen">
			<AdminSidebar />
			<main className="container-x flex-1 py-8">{children}</main>
			<Toaster />
		</div>
	);
}
