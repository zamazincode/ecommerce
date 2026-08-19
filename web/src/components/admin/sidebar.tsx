"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import {
	LayoutDashboardIcon,
	PackageIcon,
	ShoppingCartIcon,
	FolderTreeIcon,
	TicketPercentIcon,
	MessageSquareIcon,
	HistoryIcon,
	ExternalLinkIcon,
	LogOutIcon,
	type LucideIcon,
} from "lucide-react";
import Logo from "@/components/common/logo";
import { cn } from "@/lib/utils";

interface NavLink {
	href: string;
	label: string;
	icon: LucideIcon;
}

interface NavGroup {
	/** `null` → grupsuz, sidebar'ın en üstünde tek başına gösterilir (Dashboard). */
	label: string | null;
	links: NavLink[];
}

const NAV_GROUPS: NavGroup[] = [
	{
		label: null,
		links: [{ href: "/admin", label: "Dashboard", icon: LayoutDashboardIcon }],
	},
	{
		label: "Katalog",
		links: [
			{ href: "/admin/urunler", label: "Ürünler", icon: PackageIcon },
			{
				href: "/admin/kategoriler",
				label: "Kategoriler",
				icon: FolderTreeIcon,
			},
		],
	},
	{
		label: "Satış",
		links: [
			{
				href: "/admin/siparisler",
				label: "Siparişler",
				icon: ShoppingCartIcon,
			},
			{
				href: "/admin/kuponlar",
				label: "Kuponlar",
				icon: TicketPercentIcon,
			},
		],
	},
	{
		label: "İçerik",
		links: [
			{
				href: "/admin/yorumlar",
				label: "Yorumlar",
				icon: MessageSquareIcon,
			},
		],
	},
	{
		label: "Sistem",
		links: [
			{
				href: "/admin/denetim-kaydi",
				label: "Denetim Kaydı",
				icon: HistoryIcon,
			},
		],
	},
];

// `AdminTopbar`'ın mobil başlık etiketini türetmesi için — grup yapısını
// tekrar kurmasın diye burada tek yerden dışa açılıyor.
export const ADMIN_NAV_LINKS: NavLink[] = NAV_GROUPS.flatMap((g) => g.links);

export function AdminSidebar({ className }: { className?: string }) {
	const pathname = usePathname();
	const router = useRouter();
	const queryClient = useQueryClient();

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	return (
		<aside
			className={cn(
				"hidden w-60 shrink-0 flex-col border-r bg-background lg:flex",
				className,
			)}
		>
			<div className="flex h-14 items-center gap-2 border-b px-4">
				<div className="shrink-0 origin-left scale-[0.55]">
					<Logo />
				</div>
				<span className="text-sm font-semibold">Yönetim</span>
			</div>

			<nav className="flex-1 space-y-4 overflow-y-auto p-3">
				{NAV_GROUPS.map((group, groupIndex) => (
					<div key={group.label ?? `top-${groupIndex}`}>
						{group.label ? (
							<p className="px-3 pt-4 pb-1 text-[11px] font-semibold tracking-wider text-muted-foreground uppercase">
								{group.label}
							</p>
						) : null}
						<div className="space-y-0.5">
							{group.links.map(({ href, label, icon: Icon }) => {
								// "/admin" tam eşleşme, alt sayfalar prefix eşleşmesi — aksi
								// hâlde "/admin" linki her admin sayfasında aktif görünür.
								const isActive =
									href === "/admin"
										? pathname === href
										: pathname.startsWith(href);

								return (
									<Link
										key={href}
										href={href}
										className={cn(
											"relative flex h-10 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors",
											isActive
												? "before:absolute before:top-1.5 before:bottom-1.5 before:left-0 before:w-1 before:rounded-r-full before:bg-primary bg-primary-soft font-medium text-primary"
												: "text-muted-foreground hover:bg-muted hover:text-foreground",
										)}
									>
										<Icon className="size-4" />
										{label}
									</Link>
								);
							})}
						</div>
					</div>
				))}
			</nav>

			<div className="mt-auto space-y-0.5 border-t p-3">
				<Link
					href="/"
					target="_blank"
					className="flex h-9 items-center gap-2.5 rounded-lg px-3 text-sm text-muted-foreground hover:bg-muted hover:text-foreground"
				>
					<ExternalLinkIcon className="size-4" />
					Siteyi Gör
				</Link>
				<button
					type="button"
					onClick={handleLogout}
					className="flex h-9 w-full items-center gap-2.5 rounded-lg px-3 text-sm text-destructive hover:bg-destructive/10"
				>
					<LogOutIcon className="size-4" />
					Çıkış
				</button>
			</div>
		</aside>
	);
}
