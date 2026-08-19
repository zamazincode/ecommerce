"use client";

import { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import {
	MenuIcon,
	UserIcon,
	HeartIcon,
	PackageIcon,
	LogOutIcon,
} from "lucide-react";
import useSession from "@/hooks/use-session";
import { useUiStore } from "@/stores/ui-store";
import { Button } from "@/components/ui/button";
import {
	Sheet,
	SheetContent,
	SheetHeader,
	SheetTitle,
} from "@/components/ui/sheet";
import {
	Accordion,
	AccordionItem,
	AccordionTrigger,
	AccordionContent,
} from "@/components/ui/accordion";
import { Separator } from "@/components/ui/separator";
import { getCategoryIcon } from "@/lib/category-icons";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

/** Header'ın sol ucundaki hamburger — Sheet'in kendisi `MobileMenu` içinde. */
export function MobileMenuButton() {
	const toggleMobileMenu = useUiStore((s) => s.toggleMobileMenu);

	return (
		<Button
			variant="ghost"
			size="icon"
			className="lg:hidden"
			onClick={toggleMobileMenu}
			aria-label="Menüyü aç"
		>
			<MenuIcon />
		</Button>
	);
}

export function MobileMenu({
	categories,
}: {
	categories: CategoryTreeDto[];
}) {
	const isOpen = useUiStore((s) => s.isMobileMenuOpen);
	const closeMobileMenu = useUiStore((s) => s.closeMobileMenu);
	const pathname = usePathname();
	const router = useRouter();
	const queryClient = useQueryClient();
	const { data: user } = useSession();

	// Sayfa değişince menü kendiliğinden kapansın — kullanıcı bir kategoriye
	// tıkladığında Sheet açık kalırsa yeni sayfanın üstünde asılı görünür.
	useEffect(() => {
		closeMobileMenu();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [pathname]);

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		closeMobileMenu();
		router.push("/");
		router.refresh();
	}

	return (
		<Sheet open={isOpen} onOpenChange={(open) => !open && closeMobileMenu()}>
			<SheetContent side="left" className="w-[86vw] max-w-sm gap-0 p-0">
				<SheetHeader className="border-b">
					<SheetTitle className="sr-only">Menü</SheetTitle>
					{user ? (
						<Link
							href="/hesabim"
							onClick={closeMobileMenu}
							className="flex items-center gap-2 text-sm font-medium"
						>
							<UserIcon className="size-5 text-primary" />
							{user.firstName} {user.lastName}
						</Link>
					) : (
						<Button
							className="w-full"
							render={<Link href="/giris" onClick={closeMobileMenu} />}
							nativeButton={false}
						>
							Giriş Yap / Üye Ol
						</Button>
					)}
				</SheetHeader>

				<div className="flex-1 overflow-y-auto px-4">
					<Accordion multiple>
						{categories.map((category) => {
							const Icon = getCategoryIcon(category.slug);
							const children = category.children ?? [];

							return (
								<AccordionItem key={category.id} value={category.id}>
									<div className="flex items-center">
										<Link
											href={`/kategori/${category.slug}`}
											onClick={closeMobileMenu}
											className="flex flex-1 items-center gap-3 py-3 text-sm font-medium"
										>
											<Icon className="size-4 text-primary" />
											{category.name}
										</Link>
										{children.length > 0 ? (
											<AccordionTrigger className="w-auto flex-none py-3" />
										) : null}
									</div>
									{children.length > 0 ? (
										<AccordionContent>
											<ul className="space-y-1 pl-7">
												{children.map((child) => (
													<li key={child.id}>
														<Link
															href={`/kategori/${child.slug}`}
															onClick={closeMobileMenu}
															className="block py-1.5 text-sm text-muted-foreground hover:text-primary"
														>
															{child.name}
														</Link>
													</li>
												))}
											</ul>
										</AccordionContent>
									) : null}
								</AccordionItem>
							);
						})}
					</Accordion>
				</div>

				<Separator />

				<div className="space-y-1 p-4">
					<Link
						href="/hesabim/favorilerim"
						onClick={closeMobileMenu}
						className="flex items-center gap-2.5 rounded-lg px-2 py-2 text-sm hover:bg-primary-soft hover:text-primary"
					>
						<HeartIcon className="size-4" />
						Favorilerim
					</Link>
					<Link
						href="/hesabim/siparislerim"
						onClick={closeMobileMenu}
						className="flex items-center gap-2.5 rounded-lg px-2 py-2 text-sm hover:bg-primary-soft hover:text-primary"
					>
						<PackageIcon className="size-4" />
						Siparişlerim
					</Link>
					{user ? (
						<button
							type="button"
							onClick={handleLogout}
							className="flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-sm text-destructive hover:bg-destructive/10"
						>
							<LogOutIcon className="size-4" />
							Çıkış
						</button>
					) : null}
				</div>
			</SheetContent>
		</Sheet>
	);
}
