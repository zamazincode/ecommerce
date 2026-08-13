import Link from "next/link";
import Logo from "../common/logo";
import { AccountMenu } from "./account-menu";
import { SearchBar } from "./search-bar";
import { CategoryNav } from "./category-nav";
import { CartButton } from "@/components/cart/cart-button";

export default function Header() {
	return (
		<div className="border-b">
			<header className="container-x">
				<div className="flex items-end justify-between gap-4 py-2 text-xs text-muted-foreground">
					<Link href="/yardim" className="hover:underline">
						Yardım
					</Link>
					<AccountMenu />
				</div>

				<div className="flex items-center justify-between gap-12 py-4">
					<Link href="/">
						<Logo />
					</Link>
					<SearchBar />
					<CartButton />
				</div>

				<CategoryNav />
			</header>
		</div>
	);
}
