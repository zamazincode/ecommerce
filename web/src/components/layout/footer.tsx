import Link from "next/link";
import { PhoneIcon, MailIcon, MapPinIcon } from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import Logo from "../common/logo";
import { TrustBadges } from "./trust-badges";
import {
	InstagramIcon,
	FacebookIcon,
	TwitterIcon,
	YoutubeIcon,
} from "@/components/common/social-icons";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

const socialLinks = [
	{ href: "https://instagram.com", label: "Instagram", icon: InstagramIcon },
	{ href: "https://facebook.com", label: "Facebook", icon: FacebookIcon },
	{ href: "https://twitter.com", label: "Twitter", icon: TwitterIcon },
	{ href: "https://youtube.com", label: "Youtube", icon: YoutubeIcon },
];

const socialIconClass =
	"grid size-9 place-items-center rounded-full bg-background text-foreground ring-1 ring-border hover:bg-primary hover:text-primary-foreground";

export async function Footer() {
	const tree =
		(await serverApiFetch<CategoryTreeDto[]>("categories/tree")) ?? [];

	return (
		<footer className="mt-16 border-t bg-surface">
			<TrustBadges />

			<div className="container-x grid gap-8 border-t py-12 text-sm sm:grid-cols-2 lg:grid-cols-5">
				<div className="lg:col-span-1">
					<Logo />
					<p className="mt-3 text-muted-foreground">
						Kitap, kırtasiye ve daha fazlası tek adreste.
					</p>
					<div className="mt-4 flex gap-2">
						{socialLinks.map(({ href, label, icon: Icon }) => (
							<a
								key={label}
								href={href}
								target="_blank"
								rel="noreferrer noopener"
								aria-label={label}
								className={socialIconClass}
							>
								<Icon className="size-4" />
							</a>
						))}
					</div>
				</div>

				<div>
					<h3 className="mb-3 font-semibold">Kategoriler</h3>
					<ul className="space-y-2 text-muted-foreground">
						{tree.slice(0, 7).map((category) => (
							<li key={category.id}>
								<Link
									href={`/kategori/${category.slug}`}
									className="hover:underline"
								>
									{category.name}
								</Link>
							</li>
						))}
					</ul>
				</div>

				<div>
					<h3 className="mb-3 font-semibold">Hesabım</h3>
					<ul className="space-y-2 text-muted-foreground">
						<li>
							<Link href="/hesabim/siparislerim" className="hover:underline">
								Siparişlerim
							</Link>
						</li>
						<li>
							<Link href="/hesabim/adreslerim" className="hover:underline">
								Adreslerim
							</Link>
						</li>
						<li>
							<Link href="/hesabim/favorilerim" className="hover:underline">
								Favorilerim
							</Link>
						</li>
						<li>
							<Link href="/giris" className="hover:underline">
								Giriş Yap
							</Link>
						</li>
					</ul>
				</div>

				<div>
					<h3 className="mb-3 font-semibold">Yardım</h3>
					<ul className="space-y-2 text-muted-foreground">
						<li>
							<Link href="/hesabim/siparislerim" className="hover:underline">
								Sipariş Takibi
							</Link>
						</li>
						<li>
							<Link href="/hesabim/siparislerim" className="hover:underline">
								İade &amp; Değişim
							</Link>
						</li>
						<li>
							<Link href="/hesabim/adreslerim" className="hover:underline">
								Kargo
							</Link>
						</li>
						<li>
							<Link href="/" className="hover:underline">
								Sık Sorulan Sorular
							</Link>
						</li>
					</ul>
				</div>

				<div>
					<h3 className="mb-3 font-semibold">İletişim</h3>
					<ul className="space-y-2.5 text-muted-foreground">
						<li className="flex items-center gap-2">
							<PhoneIcon className="size-4 shrink-0" />
							0850 000 00 00
						</li>
						<li className="flex items-center gap-2">
							<MailIcon className="size-4 shrink-0" />
							destek@commerce.local
						</li>
						<li className="flex items-start gap-2">
							<MapPinIcon className="size-4 shrink-0" />
							İstanbul, Türkiye
						</li>
					</ul>
				</div>
			</div>

			<div className="container-x flex flex-col justify-between gap-3 border-t py-5 text-xs text-muted-foreground sm:flex-row sm:items-center">
				<p>© {new Date().getFullYear()} — Tüm hakları saklıdır.</p>
				<div className="flex items-center gap-2 font-medium">
					<span className="rounded-md border px-2 py-1">Visa</span>
					<span className="rounded-md border px-2 py-1">Mastercard</span>
					<span className="rounded-md border px-2 py-1">Troy</span>
				</div>
			</div>
		</footer>
	);
}
