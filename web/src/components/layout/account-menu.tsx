"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import {
	UserIcon,
	PackageIcon,
	MapPinIcon,
	HeartIcon,
	ShieldCheckIcon,
	LogOutIcon,
} from "lucide-react";
import useSession from "@/hooks/use-session";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import {
	Popover,
	PopoverTrigger,
	PopoverContent,
	PopoverClose,
} from "@/components/ui/popover";
import { cn } from "@/lib/utils";

// dr.com.tr'deki "ikon üstte, yazı altta iki satır" düzeninin hesap sürümü —
// HeaderActions'taki diğer butonlarla aynı görsel dil.
const triggerClass =
	"flex h-auto flex-col gap-0.5 py-1.5 text-[11px] font-normal";

const menuItemClass =
	"flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm hover:bg-primary-soft hover:text-primary";

export function AccountMenu() {
	const router = useRouter();
	const queryClient = useQueryClient();
	const { data: user, isLoading } = useSession();

	async function handleLogout() {
		await fetch("/api/auth/logout", { method: "POST" });
		queryClient.clear();
		router.push("/");
		router.refresh();
	}

	// Header yüklenirken sağ taraf zıplamasın (CLS) — trigger boyutuyla aynı ölçüde iskelet.
	if (isLoading) return <Skeleton className="h-9 w-16 rounded-lg" />;

	if (!user) {
		return (
			<Button
				variant="ghost"
				size="lg"
				className={triggerClass}
				render={<Link href="/giris" />}
				nativeButton={false}
			>
				<UserIcon className="size-5" />
				<span className="flex flex-col items-center leading-tight">
					<span className="text-muted-foreground">Giriş Yap</span>
					<span className="font-medium text-foreground">Üye Ol</span>
				</span>
			</Button>
		);
	}

	return (
		<Popover>
			<PopoverTrigger
				render={
					<Button
						variant="ghost"
						size="lg"
						className={triggerClass}
					/>
				}
			>
				<UserIcon className="size-5" />
				<span className="hidden text-foreground xl:inline">
					{user.firstName}
				</span>
			</PopoverTrigger>
			<PopoverContent>
				<PopoverClose
					nativeButton={false}
					render={<Link href="/hesabim" className={menuItemClass} />}
				>
					<UserIcon className="size-4" />
					Hesabım
				</PopoverClose>
				<PopoverClose
					nativeButton={false}
					render={
						<Link
							href="/hesabim/siparislerim"
							className={menuItemClass}
						/>
					}
				>
					<PackageIcon className="size-4" />
					Siparişlerim
				</PopoverClose>
				<PopoverClose
					nativeButton={false}
					render={
						<Link
							href="/hesabim/adreslerim"
							className={menuItemClass}
						/>
					}
				>
					<MapPinIcon className="size-4" />
					Adreslerim
				</PopoverClose>
				<PopoverClose
					nativeButton={false}
					render={
						<Link
							href="/hesabim/favorilerim"
							className={menuItemClass}
						/>
					}
				>
					<HeartIcon className="size-4" />
					Favorilerim
				</PopoverClose>
				{user.roles.includes("Admin") ? (
					<PopoverClose
						nativeButton={false}
						render={
							<Link href="/admin" className={menuItemClass} />
						}
					>
						<ShieldCheckIcon className="size-4" />
						Yönetim Paneli
					</PopoverClose>
				) : null}
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
	);
}
