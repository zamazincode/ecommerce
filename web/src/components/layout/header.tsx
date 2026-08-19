import Link from "next/link";
import Logo from "../common/logo";
import { serverApiFetch } from "@/lib/api/server";
import { AnnouncementBar } from "./announcement-bar";
import { CategoryNav } from "./category-nav";
import { HeaderActions } from "./header-actions";
import { MobileMenu, MobileMenuButton } from "./mobile-menu";
import { SearchBar } from "./search-bar";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

export default async function Header() {
	const categories =
		(await serverApiFetch<CategoryTreeDto[]>("categories/tree")) ?? [];

	return (
		<>
			<AnnouncementBar />
			<header className="sticky top-0 z-40 border-b bg-background pt-2.5">
				<div className="container-x flex h-16 items-center gap-3 lg:gap-6">
					<MobileMenuButton />
					<Link href="/" className="shrink-0">
						<Logo />
					</Link>
					<div className="mx-auto hidden max-w-2xl flex-1 md:block">
						<SearchBar />
					</div>
					<HeaderActions />
				</div>
				<CategoryNav />
			</header>
			<div className="border-b bg-background md:hidden">
				<div className="container-x py-2.5">
					<SearchBar />
				</div>
			</div>
			<MobileMenu categories={categories} />
		</>
	);
}
