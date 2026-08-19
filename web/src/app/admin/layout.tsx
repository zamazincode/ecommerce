import { redirect } from "next/navigation";
import { serverApiFetch } from "@/lib/api/server";
import type { components } from "@/types/api";
import { AdminSidebar } from "@/components/admin/sidebar";
import { AdminTopbar } from "@/components/admin/topbar";

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
		<div className="flex min-h-screen bg-surface">
			<AdminSidebar />
			<div className="flex min-w-0 flex-1 flex-col">
				<AdminTopbar user={user} />
				{/* `container-x` (max-w-7xl mx-auto) BİLEREK kullanılmıyor — sidebar'ın
				    yanında ortalama yapıp 1920px'te sağda ölü alan bırakıyordu (sorun #1). */}
				<main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
					<div className="mx-auto w-full max-w-[1400px] space-y-6">
						{children}
					</div>
				</main>
			</div>
		</div>
	);
}
