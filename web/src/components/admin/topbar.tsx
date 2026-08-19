"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { ExternalLinkIcon, LogOutIcon, MenuIcon } from "lucide-react";

import type { components } from "@/types/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import {
	Popover,
	PopoverTrigger,
	PopoverContent,
	PopoverClose,
} from "@/components/ui/popover";
import { AdminSidebar, ADMIN_NAV_LINKS } from "@/components/admin/sidebar";

type UserDto = components["schemas"]["UserDto"];

const menuItemClass =
	"flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm hover:bg-primary-soft hover:text-primary";

function currentPageLabel(pathname: string): string {
	const match = ADMIN_NAV_LINKS.find((link) =>
		link.href === "/admin" ? pathname === link.href : pathname.startsWith(link.href),
	);
	return match?.label ?? "Yönetim";
}

/** `lg:hidden` hamburger + mobil sayfa başlığı + sağda hızlı eylemler — sidebar `hidden lg:flex` olduğu için mobilde tek gezinme yolu bu. */
export function AdminTopbar({ user }: { user: UserDto }) {
	const pathname = usePathname();
	const router = useRouter();
	const queryClient = useQueryClient();
	const [isSidebarOpen, setIsSidebarOpen] = useState(false);

	// Sayfa değişince mobil menü kendiliğinden kapansın. `useEffect` içinde
	// `setState` React Compiler'ın `set-state-in-effect` kuralını tetikliyor
	// (basamaklı render riski) — bunun yerine `urunler/page.tsx`'teki
	// "render sırasında senkronize et" deseni: URL DIŞARIDAN değiştiğinde
	// (link tıklama) bir sonraki render'da yakalanıp kapatılıyor.
	const [prevPathname, setPrevPathname] = useState(pathname);
	if (pathname !== prevPathname) {
		setPrevPathname(pathname);
		setIsSidebarOpen(false);
	}

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	const initial = user.firstName.charAt(0).toUpperCase();

	return (
		<header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/90 px-4 backdrop-blur supports-backdrop-filter:bg-background/75 sm:px-6 lg:px-8">
			<Button
				variant="ghost"
				size="icon-sm"
				className="lg:hidden"
				aria-label="Menüyü aç"
				onClick={() => setIsSidebarOpen(true)}
			>
				<MenuIcon />
			</Button>

			<p className="min-w-0 truncate text-sm font-semibold lg:hidden">
				{currentPageLabel(pathname)}
			</p>

			<div className="ml-auto flex items-center gap-2">
				<Button
					variant="ghost"
					size="sm"
					render={<Link href="/" target="_blank" />}
					nativeButton={false}
				>
					<ExternalLinkIcon />
					<span className="hidden sm:inline">Siteyi Gör</span>
				</Button>

				<Popover>
					<PopoverTrigger
						render={<Button variant="ghost" size="sm" className="gap-2 pl-2" />}
					>
						<span className="grid size-6 shrink-0 place-items-center rounded-full bg-primary-soft text-xs font-semibold text-primary">
							{initial}
						</span>
						<span className="hidden text-foreground sm:inline">
							{user.firstName}
						</span>
					</PopoverTrigger>
					<PopoverContent>
						<div className="px-2.5 py-1.5">
							<p className="truncate text-sm font-medium">
								{user.firstName} {user.lastName}
							</p>
							<p className="truncate text-xs text-muted-foreground">
								{user.email}
							</p>
						</div>
						<Separator className="my-1.5" />
						<PopoverClose
							render={
								<button
									type="button"
									onClick={handleLogout}
									className={cn(
										menuItemClass,
										"text-destructive hover:bg-destructive/10 hover:text-destructive",
									)}
								/>
							}
						>
							<LogOutIcon className="size-4" />
							Çıkış
						</PopoverClose>
					</PopoverContent>
				</Popover>
			</div>

			<Sheet open={isSidebarOpen} onOpenChange={setIsSidebarOpen}>
				<SheetContent side="left" className="w-72 gap-0 p-0">
					<SheetHeader className="sr-only">
						<SheetTitle>Yönetim menüsü</SheetTitle>
					</SheetHeader>
					<AdminSidebar className="flex h-full w-full border-r-0" />
				</SheetContent>
			</Sheet>
		</header>
	);
}
