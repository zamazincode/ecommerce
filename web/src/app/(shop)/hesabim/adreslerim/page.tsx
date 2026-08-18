"use client";

import { useAddresses, useDeleteAddress } from "@/hooks/use-addresses";
import { AddressFormDialog } from "@/components/account/address-form-dialog";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";

export default function AddressesPage() {
	const { data: addresses, isLoading } = useAddresses();
	const deleteAddress = useDeleteAddress();

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<h1 className="text-xl font-semibold">Adreslerim</h1>
				<AddressFormDialog trigger={<Button>Yeni Adres</Button>} />
			</div>

			{isLoading ? null : addresses?.length === 0 ? (
				<p className="text-sm text-muted-foreground">
					Henüz kayıtlı adresin yok — sipariş verebilmek için en az
					bir adres eklemelisin.
				</p>
			) : (
				<div className="grid gap-4 sm:grid-cols-2">
					{addresses?.map((address) => (
						<Card key={address.id} className="p-4">
							<div className="mb-2 flex items-center justify-between">
								<p className="font-medium">{address.title}</p>
								{address.isDefault ? (
									<Badge>Varsayılan</Badge>
								) : null}
							</div>
							<p className="text-sm text-muted-foreground">
								{address.fullName} · {address.phone}
							</p>
							<p className="text-sm text-muted-foreground">
								{address.fullAddress}, {address.district}/
								{address.city}
							</p>
							<div className="mt-3 flex gap-2">
								<AddressFormDialog
									address={address}
									trigger={
										<Button variant="ghost" size="sm">
											Düzenle
										</Button>
									}
								/>
								<Button
									variant="ghost"
									size="sm"
									onClick={() => {
										if (
											confirm(
												`"${address.title}" adresi silinsin mi?`,
											)
										)
											deleteAddress.mutate(
												address.id as number,
											);
									}}
								>
									Sil
								</Button>
							</div>
						</Card>
					))}
				</div>
			)}
		</div>
	);
}
