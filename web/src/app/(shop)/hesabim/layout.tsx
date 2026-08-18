import { AccountSidebar } from "@/components/account/account-sidebar";

export default function AccountLayout({
	children,
}: {
	children: React.ReactNode;
}) {
	return (
		<main className="container-x flex gap-8 py-8">
			<AccountSidebar />
			<div className="flex-1">{children}</div>
		</main>
	);
}
