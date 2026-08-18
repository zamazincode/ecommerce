"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { UserIcon, PackageIcon, MapPinIcon } from "lucide-react";

const LINKS = [
	{ href: "/hesabim", label: "Genel Bakış", icon: UserIcon },
	{ href: "/hesabim/siparislerim", label: "Siparişlerim", icon: PackageIcon },
	{ href: "/hesabim/adreslerim", label: "Adreslerim", icon: MapPinIcon },
];

export function AccountSidebar() {
	const pathname = usePathname();

	return (
		<aside className="w-48 shrink-0 space-y-1 text-sm">
			{LINKS.map(({ href, label, icon: Icon }) => {
				const isActive =
					href === "/hesabim"
						? pathname === href
						: pathname.startsWith(href);
				return (
					<Link
						key={href}
						href={href}
						className={cn(
							"flex items-center gap-2 rounded-md px-3 py-2",
							isActive
								? "bg-muted font-medium"
								: "hover:bg-muted/50",
						)}
					>
						<Icon className="size-4" />
						{label}
					</Link>
				);
			})}
		</aside>
	);
}
