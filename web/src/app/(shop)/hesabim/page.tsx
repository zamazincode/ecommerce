import Link from "next/link";
import {
	PackageIcon,
	MapPinIcon,
	HeartIcon,
	ChevronRightIcon,
} from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import { PageHeader } from "@/components/admin/page-header";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Price } from "@/components/ui/price";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TONES } from "@/lib/enums";
import type { components } from "@/types/api";

type UserDto = components["schemas"]["UserDto"];
type PagedResultOfOrderListDto =
	components["schemas"]["PagedResultOfOrderListDto"];
type AddressDto = components["schemas"]["AddressDto"];
type ProductListDto = components["schemas"]["ProductListDto"];
type OrderStatus = components["schemas"]["OrderStatus"];

export default async function AccountOverviewPage() {
	const [user, orders, addresses, favorites] = await Promise.all([
		serverApiFetch<UserDto>("auth/me"),
		serverApiFetch<PagedResultOfOrderListDto>("orders?pageSize=3"),
		serverApiFetch<AddressDto[]>("addresses"),
		serverApiFetch<ProductListDto[]>("favorites"),
	]);

	// orders?pageSize=3'ün totalCount'u zaten TÜM siparişleri sayıyor —
	// özet kartı için ayrı bir istek gerekmiyor.
	const summary = [
		{
			label: "Toplam Sipariş",
			value: Number(orders?.totalCount ?? 0),
			href: "/hesabim/siparislerim",
			icon: PackageIcon,
		},
		{
			label: "Kayıtlı Adres",
			value: addresses?.length ?? 0,
			href: "/hesabim/adreslerim",
			icon: MapPinIcon,
		},
		{
			label: "Favori",
			value: favorites?.length ?? 0,
			href: "/hesabim/favorilerim",
			icon: HeartIcon,
		},
	];

	return (
		<div>
			<PageHeader
				title={`Merhaba, ${user?.firstName ?? ""}`}
				description="Hesabına genel bir bakış."
				className="mb-6"
			/>

			<div className="mb-6 grid gap-4 sm:grid-cols-3">
				{summary.map(({ label, value, href, icon: Icon }) => (
					<Link
						key={label}
						href={href}
						className="flex items-center gap-3 rounded-2xl border bg-card p-4 transition-colors hover:border-primary/40 hover:shadow-card"
					>
						<div className="grid size-10 shrink-0 place-items-center rounded-full bg-primary-soft text-primary">
							<Icon className="size-5" />
						</div>
						<div>
							<p className="text-xl font-semibold">{value}</p>
							<p className="text-xs text-muted-foreground">
								{label}
							</p>
						</div>
					</Link>
				))}
			</div>

			<Card className="p-5">
				<div className="mb-3 flex items-center justify-between">
					<h2 className="font-heading text-base font-semibold">
						Son Siparişlerin
					</h2>
					<Link
						href="/hesabim/siparislerim"
						className="text-sm text-primary hover:underline"
					>
						Tümünü gör
					</Link>
				</div>

				{orders && orders.items.length > 0 ? (
					<ul className="divide-y">
						{orders.items.map((order) => (
							<li key={order.orderNumber}>
								<Link
									href={`/hesabim/siparislerim/${order.orderNumber}`}
									className="flex items-center justify-between gap-3 py-3 text-sm hover:text-primary"
								>
									<div className="min-w-0">
										<p className="font-mono font-medium">
											{order.orderNumber}
										</p>
										<p className="text-xs text-muted-foreground">
											{new Date(
												order.createdAt,
											).toLocaleDateString("tr-TR")}
										</p>
									</div>
									<div className="flex items-center gap-3">
										<Badge
											variant={
												ORDER_STATUS_TONES[
													order.status as OrderStatus
												]
											}
										>
											{
												ORDER_STATUS_LABELS[
													order.status as OrderStatus
												]
											}
										</Badge>
										<Price
											size="sm"
											price={Number(order.total)}
										/>
										<ChevronRightIcon className="size-4 text-muted-foreground" />
									</div>
								</Link>
							</li>
						))}
					</ul>
				) : (
					<EmptyState
						icon={PackageIcon}
						title="Henüz siparişin yok"
						description="Alışverişe başlamak için ürünleri keşfet."
						action={
							<Button
								render={<Link href="/" />}
								nativeButton={false}
							>
								Alışverişe Başla
							</Button>
						}
					/>
				)}
			</Card>
		</div>
	);
}
