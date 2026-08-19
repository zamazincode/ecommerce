import { AccountSidebar } from "@/components/account/account-sidebar";

export default function AccountLayout({
	children,
}: {
	children: React.ReactNode;
}) {
	return (
		<main className="container-x grid gap-6 py-6 md:py-8 lg:grid-cols-[240px_1fr]">
			<AccountSidebar />
			<div className="min-w-0">{children}</div>
		</main>
	);
}
