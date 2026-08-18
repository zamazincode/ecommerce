import Link from "next/link";
import { serverApiFetch } from "@/lib/api/server";
import { Card } from "@/components/ui/card";
import type { components } from "@/types/api";

type UserDto = components["schemas"]["UserDto"];
type PagedResultOfOrderListDto =
	components["schemas"]["PagedResultOfOrderListDto"];

export default async function AccountOverviewPage() {
	const [user, orders] = await Promise.all([
		serverApiFetch<UserDto>("auth/me"),
		serverApiFetch<PagedResultOfOrderListDto>("orders?pageSize=3"),
	]);

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">
				Merhaba, {user?.firstName}
			</h1>

			<Card className="p-4">
				<div className="mb-3 flex items-center justify-between">
					<h2 className="font-medium">Son Siparişlerin</h2>
					<Link
						href="/hesabim/siparislerim"
						className="text-sm underline"
					>
						Tümünü gör
					</Link>
				</div>
				{orders && orders.items.length > 0 ? (
					<ul className="space-y-2 text-sm">
						{orders.items.map((order) => (
							<li
								key={order.orderNumber}
								className="flex justify-between"
							>
								<Link
									href={`/hesabim/siparislerim/${order.orderNumber}`}
									className="hover:underline"
								>
									{order.orderNumber}
								</Link>
								<span className="text-muted-foreground">
									{order.total} ₺
								</span>
							</li>
						))}
					</ul>
				) : (
					<p className="text-sm text-muted-foreground">
						Henüz siparişin yok.
					</p>
				)}
			</Card>
		</div>
	);
}
