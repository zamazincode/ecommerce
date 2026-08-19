"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import {
	UserIcon,
	PackageIcon,
	MapPinIcon,
	HeartIcon,
	LogOutIcon,
} from "lucide-react";
import useSession from "@/hooks/use-session";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";

const LINKS = [
	{ href: "/hesabim", label: "Genel Bakış", icon: UserIcon },
	{ href: "/hesabim/siparislerim", label: "Siparişlerim", icon: PackageIcon },
	{ href: "/hesabim/adreslerim", label: "Adreslerim", icon: MapPinIcon },
	{ href: "/hesabim/favorilerim", label: "Favorilerim", icon: HeartIcon },
];

export function AccountSidebar() {
	const pathname = usePathname();
	const router = useRouter();
	const queryClient = useQueryClient();
	const { data: user } = useSession();

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	return (
		<aside className="rounded-2xl border bg-card p-2 lg:sticky lg:top-24 lg:self-start">
			{/* Kullanıcı bloğu — dar ekranda sekme şeridine yer açmak için gizli. */}
			<div className="hidden items-center gap-3 border-b p-3 pb-4 lg:flex">
				<div className="grid size-10 shrink-0 place-items-center rounded-full bg-primary-soft text-sm font-semibold text-primary">
					{user?.firstName?.[0]?.toUpperCase() ?? "?"}
				</div>
				<div className="min-w-0">
					<p className="truncate text-sm font-medium">
						{user
							? `${user.firstName} ${user.lastName}`
							: "Hesabım"}
					</p>
					<p className="truncate text-xs text-muted-foreground">
						{user?.email}
					</p>
				</div>
			</div>

			<nav className="flex gap-1 overflow-x-auto p-1 lg:mt-2 lg:flex-col lg:overflow-visible lg:p-0">
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
								"relative flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-sm whitespace-nowrap transition-colors",
								isActive
									? "bg-primary-soft font-medium text-primary before:absolute before:inset-y-1 before:left-0 before:hidden before:w-0.75 before:rounded-full before:bg-primary lg:before:block"
									: "text-muted-foreground hover:bg-muted hover:text-foreground",
							)}
						>
							<Icon className="size-4" />
							{label}
						</Link>
					);
				})}
			</nav>

			<Separator className="my-2 hidden lg:block" />

			<button
				type="button"
				onClick={handleLogout}
				className="hidden w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-destructive hover:bg-destructive/10 lg:flex"
			>
				<LogOutIcon className="size-4" />
				Çıkış Yap
			</button>
		</aside>
	);
}
