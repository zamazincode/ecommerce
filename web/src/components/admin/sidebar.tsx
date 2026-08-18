"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
	LayoutDashboardIcon,
	PackageIcon,
	ShoppingCartIcon,
	FolderTreeIcon,
	TicketPercentIcon,
	MessageSquareIcon,
	HistoryIcon,
} from "lucide-react";

const LINKS = [
	{ href: "/admin", label: "Dashboard", icon: LayoutDashboardIcon },
	{ href: "/admin/urunler", label: "Ürünler", icon: PackageIcon },
	{ href: "/admin/siparisler", label: "Siparişler", icon: ShoppingCartIcon },
	{ href: "/admin/kategoriler", label: "Kategoriler", icon: FolderTreeIcon },
	{ href: "/admin/kuponlar", label: "Kuponlar", icon: TicketPercentIcon },
	{ href: "/admin/yorumlar", label: "Yorumlar", icon: MessageSquareIcon },
	{
		href: "/admin/denetim-kaydi",
		label: "Denetim Kaydı",
		icon: HistoryIcon,
	},
];

export function AdminSidebar() {
	const pathname = usePathname();

	return (
		<aside className="w-56 shrink-0 border-r bg-muted/30 p-4">
			<p className="mb-4 px-2 text-sm font-semibold text-muted-foreground">
				Yönetim Paneli
			</p>
			<nav className="space-y-1">
				{LINKS.map(({ href, label, icon: Icon }) => {
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
								"flex items-center gap-2 rounded-md px-2 py-2 text-sm",
								isActive
									? "bg-primary text-primary-foreground"
									: "hover:bg-muted",
							)}
						>
							<Icon className="size-4" />
							{label}
						</Link>
					);
				})}
			</nav>
		</aside>
	);
}
